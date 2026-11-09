import { Component, DestroyRef, ElementRef, afterNextRender, computed, effect, inject, input, resource, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { Router, RouterLink } from '@angular/router';
import * as L from 'leaflet';

import { Api } from '../../api/api';
import { getResourceNodes } from '../../api/fn/catalog/get-resource-nodes';
import { analyseSnapshot } from '../../api/fn/world/analyse-snapshot';
import { getSnapshot } from '../../api/fn/world/get-snapshot';
import { getWorldMap } from '../../api/fn/world/get-world-map';
import { traceFactories } from '../../api/fn/world/trace-factories';
import { FootprintDocument } from '../../api/models/footprint-document';
import { PositionDocument } from '../../api/models/position-document';
import { CatalogNames } from '../../shared/catalog-names';
import { perMinute, percent, positionInMetres } from '../../shared/display';
import { problemMessage, whenAnalysed } from '../../shared/problem';
import { areaBetween } from '../area/area-model';
import {
  MachineStatus,
  conveyorColours,
  describeContents,
  footprintCorners,
  freeNodes,
  fromMapPoint,
  itemColour,
  locate,
  machineStatuses,
  mainItem,
  purityColours,
  sampleCurve,
  statusColours,
  statusLabels,
  toMapPoint,
} from './map-model';
import { LimitFacts, limitText } from '../limit-text';

/** Every kilometre a grid line, over the whole game world (about −3.3 to 4.3 km east, −3.8 to 3.8 km north). */
const GridStep = 1000;
const GridExtent = { from: -5000, to: 5000 };

/**
 * The world map of a snapshot: its machines by how they run, its belts and pipes as built with the bottlenecks red,
 * its providers, the resource nodes still free, and a factory highlighted. A plain grid in metres behind it (ADR-0020).
 */
@Component({
  selector: 'app-map-page',
  imports: [
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
  ],
  templateUrl: './map-page.html',
  styleUrl: './map-page.scss',
})
export class MapPage {
  private readonly api = inject(Api);
  private readonly names = inject(CatalogNames);
  private readonly router = inject(Router);

  /** From the route: /snapshots/:id/map. */
  readonly id = input.required<string>();
  /** From the query, ?focus=<buildable id>: the buildable a finding links to. The map zooms to it and rings it. */
  readonly focus = input<string>();

  protected readonly snapshot = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => this.api.invoke(getSnapshot, params),
  });
  protected readonly world = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => this.api.invoke(getWorldMap, params),
  });
  /** The machines' status: the map shows them grey until the analysis is ready, then colours them. */
  protected readonly analysis = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params, abortSignal }) => whenAnalysed(() => this.api.invoke$Response(analyseSnapshot, params), abortSignal),
  });
  protected readonly nodes = resource({ loader: () => this.api.invoke(getResourceNodes) });
  protected readonly factories = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => this.api.invoke(traceFactories, params),
  });

  protected readonly factoryId = signal<string | null>(null);
  protected readonly showFreeNodes = signal(true);
  /** Colour belts by what they carry most of, instead of grey (pipes stay teal, bottlenecks red). */
  protected readonly colourByItem = signal(false);
  protected readonly pointer = signal<string>('');
  /** Drawing a rectangle to open in 3D: the map stops panning, and a drag draws instead. */
  protected readonly drawing = signal(false);

  private readonly statuses = computed(() => machineStatuses(this.analysis.value()?.machines));
  /** Generators burning a known fuel, by id (BR-A15): coloured by how they run, like machines. */
  private readonly generators = computed(() => new Map((this.analysis.value()?.generators ?? []).map((generator) => [generator.id, generator])));
  private readonly utilisations = computed(
    () => new Map((this.analysis.value()?.machines ?? []).map((machine) => [machine.id, machine.utilisation])),
  );
  /** Each machine's finding, by id: its tooltip says what it's short of or can't get rid of (BR-A11). */
  private readonly machineFindings = computed(() => new Map((this.analysis.value()?.machines ?? []).map((machine) => [machine.id, machine])));
  private readonly bottlenecks = computed(
    () => new Map((this.analysis.value()?.bottlenecks ?? []).map((bottleneck) => [bottleneck.id, bottleneck])),
  );
  /** Each building class's footprint, by class name; a class without one is drawn as a dot. */
  private readonly footprints = computed(
    () => new Map((this.world.value()?.footprints ?? []).map((footprint) => [footprint.id, footprint] as [string, FootprintDocument])),
  );
  private readonly factoryMachines = computed(() => {
    const factory = this.factories.value()?.find((trace) => trace.factoryId === this.factoryId());
    return factory ? new Set(factory.machines.map((machine) => machine.id)) : null;
  });

  protected readonly legend = (['none', 'starved', 'outputBlocked', 'noRecipe', 'unknown'] as MachineStatus[]).map((status) => ({
    status,
    label: statusLabels[status],
    colour: statusColours[status],
  }));
  protected readonly problem = problemMessage;

  /** The items on the belts, most belts first, with their colour: the legend while belts are coloured by item. */
  protected readonly beltItems = computed(() => {
    const belts = new Map<string, number>();
    for (const conveyor of this.world.value()?.conveyors ?? []) {
      const item = mainItem(conveyor.contents);
      if (conveyor.kind === 'belt' && item) belts.set(item, (belts.get(item) ?? 0) + 1);
    }
    return [...belts.entries()]
      .sort(([, a], [, b]) => b - a)
      .map(([item]) => ({ item, name: this.names.item(item), colour: itemColour(item) }));
  });

  private readonly container = viewChild.required<ElementRef<HTMLElement>>('map');
  private map?: L.Map;
  private readonly machineLayer = L.layerGroup();
  private readonly conveyorLayer = L.layerGroup();
  private readonly providerLayer = L.layerGroup();
  private readonly otherLayer = L.layerGroup();
  private readonly nodeLayer = L.layerGroup();
  private readonly focusLayer = L.layerGroup();
  /** Every drawn buildable's shape, by id: to open the tooltip of the one in focus. */
  private readonly shapes = new Map<string, L.Path>();
  private fitted = false;

  constructor() {
    afterNextRender(() => this.createMap());
    inject(DestroyRef).onDestroy(() => this.map?.remove());

    // Redraw a layer when what it shows changes; Leaflet itself knows nothing about signals.
    effect(() => this.drawConveyors());
    effect(() => this.drawMachines());
    effect(() => this.drawProviders());
    effect(() => this.drawOthers());
    effect(() => this.drawFreeNodes());
    effect(() => this.fitToFactory());
    // Last, so the shapes it opens a tooltip on are drawn.
    effect(() => this.focusOnBuildable());
  }

  private createMap(): void {
    this.map = L.map(this.container().nativeElement, {
      crs: L.CRS.Simple,
      preferCanvas: true,
      minZoom: -4,
      maxZoom: 3,
      zoomSnap: 0.25,
      attributionControl: false,
    });
    this.gridLayer().addTo(this.map);
    this.nodeLayer.addTo(this.map);
    this.otherLayer.addTo(this.map);
    this.conveyorLayer.addTo(this.map);
    this.providerLayer.addTo(this.map);
    this.machineLayer.addTo(this.map);
    this.focusLayer.addTo(this.map);
    L.control
      .layers(
        undefined,
        {
          Machines: this.machineLayer,
          'Belts and pipes': this.conveyorLayer,
          Providers: this.providerLayer,
          'Splitters, lifts, storage, consumers': this.otherLayer,
        },
        { collapsed: false, position: 'topright' },
      )
      .addTo(this.map);
    this.map.setView([0, 0], -3);
    this.listenForArea(this.map);
    this.map.on('mousemove', (event: L.LeafletMouseEvent) =>
      this.pointer.set(positionInMetres({ x: event.latlng.lng * 100, y: -event.latlng.lat * 100, z: 0 }).replace(/, 0 m$/, ' m')),
    );
    this.drawConveyors();
    this.drawMachines();
    this.drawProviders();
    this.drawOthers();
    this.drawFreeNodes();
    this.focusOnBuildable();
  }

  protected toggleDrawing(): void {
    this.drawing.update((on) => !on);
    if (this.drawing()) this.map?.dragging.disable();
    else this.map?.dragging.enable();
  }

  /** While drawing, a drag draws a rectangle; letting go opens what's inside it in 3D. */
  private listenForArea(map: L.Map): void {
    let start: L.LatLng | null = null;
    let rectangle: L.Rectangle | null = null;
    map.on('mousedown', (event: L.LeafletMouseEvent) => {
      if (!this.drawing()) return;
      start = event.latlng;
      rectangle = L.rectangle(L.latLngBounds(start, start), { color: '#ffeb3b', weight: 2, fillOpacity: 0.1, interactive: false }).addTo(map);
    });
    map.on('mousemove', (event: L.LeafletMouseEvent) => {
      if (start && rectangle) rectangle.setBounds(L.latLngBounds(start, event.latlng));
    });
    map.on('mouseup', (event: L.LeafletMouseEvent) => {
      if (!start) return;
      const area = areaBetween(fromMapPoint(start.lat, start.lng), fromMapPoint(event.latlng.lat, event.latlng.lng));
      rectangle?.remove();
      [start, rectangle] = [null, null];
      // A click without a drag draws nothing.
      if (area.x1 - area.x0 < 200 || area.y1 - area.y0 < 200) return;
      this.toggleDrawing();
      void this.router.navigate(['/snapshots', this.id(), 'area'], { queryParams: area });
    });
  }

  /**
   * A building as the ground it covers, turned the way it faces (ADR-0020); a dot when its class has no footprint, as a
   * lift, or when the snapshot is older than its footprints.
   */
  private shape(position: PositionDocument, yaw: number, className: string, style: L.PathOptions, dot = 4): L.Path {
    const footprint = this.footprints().get(className);
    return footprint
      ? L.polygon(footprintCorners(position, yaw, footprint), style)
      : L.circleMarker(toMapPoint(position), { ...style, radius: dot });
  }

  private gridLayer(): L.LayerGroup {
    const grid = L.layerGroup();
    const style: L.PolylineOptions = { color: '#ffffff', opacity: 0.08, weight: 1, interactive: false };
    for (let metres = GridExtent.from; metres <= GridExtent.to; metres += GridStep) {
      L.polyline([[GridExtent.from, metres], [GridExtent.to, metres]], style).addTo(grid);
      L.polyline([[metres, GridExtent.from], [metres, GridExtent.to]], style).addTo(grid);
    }
    return grid;
  }

  private drawMachines(): void {
    const machines = this.world.value()?.machines ?? [];
    const statuses = this.statuses();
    const utilisations = this.utilisations();
    const findings = this.machineFindings();
    const highlighted = this.factoryMachines();
    if (!this.map) return;

    this.machineLayer.clearLayers();
    const points: L.LatLngExpression[] = [];
    for (const machine of machines) {
      const status: MachineStatus = statuses.get(machine.id) ?? 'unknown';
      const inFactory = highlighted?.has(machine.id) ?? false;
      const point = toMapPoint(machine.position);
      points.push(point);
      const utilisation = utilisations.get(machine.id);
      const shape = this.shape(machine.position, machine.yaw, machine.className, {
        color: inFactory ? '#ffffff' : statusColours[status],
        weight: inFactory ? 2 : 1,
        fillColor: statusColours[status],
        fillOpacity: highlighted && !inFactory ? 0.15 : 0.75,
        opacity: highlighted && !inFactory ? 0.15 : 1,
      })
        .bindTooltip(
          `<b>${this.names.building(machine.className)}</b> · ${this.names.recipe(machine.recipe)}<br>` +
            `${statusLabels[status]}${utilisation !== undefined ? ` · ${percent(utilisation)}` : ''}<br>` +
            (findings.get(machine.id) ? this.limitLine(findings.get(machine.id)!) : '') +
            (machine.inputContents ? `In: ${this.contents(machine.inputContents)}<br>` : '') +
            (machine.outputContents ? `Out: ${this.contents(machine.outputContents)}<br>` : '') +
            positionInMetres(machine.position),
        )
        .addTo(this.machineLayer);
      this.shapes.set(machine.id, shape);
    }

    // The first time there's something to show, zoom to it.
    if (!this.fitted && points.length > 0) {
      this.map.fitBounds(L.latLngBounds(points), { padding: [24, 24] });
      this.fitted = true;
    }
  }

  /** Belts and pipes as built (their saved curves); a bottleneck red, with what it carries and what's offered on top. */
  private drawConveyors(): void {
    const conveyors = this.world.value()?.conveyors ?? [];
    const bottlenecks = this.bottlenecks();
    const byItem = this.colourByItem();
    if (!this.map) return;

    this.conveyorLayer.clearLayers();
    for (const conveyor of conveyors) {
      const bottleneck = bottlenecks.get(conveyor.id);
      const item = conveyor.kind === 'belt' ? mainItem(conveyor.contents) : null;
      const colour = bottleneck
        ? conveyorColours.bottleneck
        : byItem && conveyor.kind === 'belt'
          ? item
            ? itemColour(item)
            : '#455a64'
          : conveyorColours[conveyor.kind];
      const line = L.polyline(sampleCurve(conveyor.curve), {
        color: colour,
        weight: bottleneck ? 4 : conveyor.kind === 'pipe' ? 2.5 : byItem && item ? 2.5 : 1.5,
        opacity: bottleneck || (byItem && item) ? 1 : 0.6,
      });
      const carries = this.contents(conveyor.contents);
      line.bindTooltip(
        (bottleneck
          ? `<b>Bottleneck</b> · ${this.names.building(conveyor.className)}<br>` +
            `carries ${perMinute(bottleneck.flow)} of ${perMinute(bottleneck.capacity)}, ${perMinute(bottleneck.offeredMore)} more offered`
          : `<b>${this.names.building(conveyor.className)}</b>`) + (carries ? `<br>On it: ${carries}` : ''),
      );
      line.addTo(this.conveyorLayer);
      this.shapes.set(conveyor.id, line);
    }
  }

  private drawProviders(): void {
    const providers = this.world.value()?.providers ?? [];
    if (!this.map) return;

    this.providerLayer.clearLayers();
    for (const provider of providers) {
      const shape = this.shape(provider.position, provider.yaw, provider.className, {
        color: provider.purity ? purityColours[provider.purity] : '#42a5f5',
        weight: 2,
        fillColor: '#42a5f5',
        fillOpacity: 0.6,
      }, 5)
        .bindTooltip(
          `<b>${this.names.building(provider.className)}</b>` +
            (provider.resource ? ` · ${this.names.item(provider.resource)}` : '') +
            (provider.purity ? ` · ${provider.purity}` : '') +
            `<br>${positionInMetres(provider.position)}`,
        )
        .addTo(this.providerLayer);
      this.shapes.set(provider.id, shape);
    }
  }

  /** Splitters, mergers, lifts, storage, and the consumers belts end in (BR-W8): so belts don't stop in mid-air. */
  private drawOthers(): void {
    const world = this.world.value();
    const generators = this.generators();
    if (!this.map) return;

    this.otherLayer.clearLayers();
    const style = (consumer: boolean): L.PathOptions => ({
      color: consumer ? '#ab47bc' : '#78909c',
      weight: 1,
      fillColor: consumer ? '#ab47bc' : '#546e7a',
      fillOpacity: 0.5,
    });
    for (const [buildings, consumer] of [[world?.logistics ?? [], false], [world?.consumers ?? [], true]] as const) {
      for (const building of buildings) {
        // A generator burning a known fuel shows how it runs (BR-A15), in the machines' colours.
        const generator = consumer ? generators.get(building.id) : undefined;
        const colour = generator ? statusColours[generator.limit] : undefined;
        const shape = this.shape(
          building.position,
          building.yaw,
          building.className,
          colour ? { color: colour, weight: 1, fillColor: colour, fillOpacity: 0.75 } : style(consumer),
          2,
        )
          .bindTooltip(
            `<b>${this.names.building(building.className)}</b><br>` +
              (generator
                ? `Burns ${this.names.item(generator.fuel)} · ${statusLabels[generator.limit]} · ${percent(generator.utilisation)}<br>` +
                  this.limitLine(generator)
                : '') +
              (building.contents ? `Holds: ${this.contents(building.contents)}<br>` : '') +
              positionInMetres(building.position),
          )
          .addTo(this.otherLayer);
        this.shapes.set(building.id, shape);
      }
    }
  }

  /** Why it runs as it does, as a tooltip line; empty when there's nothing to say. */
  private limitLine(finding: LimitFacts): string {
    const text = limitText(finding, (item) => this.names.item(item));
    return text ? `${text}<br>` : '';
  }

  /** Contents in words, with the Catalog's item names. */
  private contents(contents: Record<string, number> | null | undefined): string {
    return describeContents(contents, (item) => this.names.item(item));
  }

  private drawFreeNodes(): void {
    const free = this.showFreeNodes() ? freeNodes(this.nodes.value() ?? [], this.world.value()?.providers ?? []) : [];
    if (!this.map) return;

    this.nodeLayer.clearLayers();
    for (const node of free) {
      L.circleMarker(toMapPoint(node.position), {
        radius: 5,
        color: purityColours[node.purity] ?? '#bdbdbd',
        weight: 1.5,
        dashArray: '3 3',
        fill: false,
      })
        .bindTooltip(`Free ${node.purity} node · ${this.names.item(node.resource)}<br>${positionInMetres(node.position)}`)
        .addTo(this.nodeLayer);
    }
  }

  private fitToFactory(): void {
    const highlighted = this.factoryMachines();
    const machines = this.world.value()?.machines ?? [];
    if (!this.map || !highlighted) return;

    const points = machines.filter((machine) => highlighted.has(machine.id)).map((machine) => toMapPoint(machine.position));
    if (points.length > 0) this.map.fitBounds(L.latLngBounds(points), { padding: [48, 48], maxZoom: 1 });
  }

  /** A finding's buildable (?focus=): zoom to it, ring it, and open its tooltip. Nothing when the map doesn't have it. */
  private focusOnBuildable(): void {
    const world = this.world.value();
    const id = this.focus();
    if (!this.map || !world || !id) return;

    const point = locate(world, id);
    this.focusLayer.clearLayers();
    if (!point) return;

    L.circleMarker(point, { radius: 22, color: '#ffeb3b', weight: 3, fill: false, interactive: false }).addTo(this.focusLayer);
    this.map.setView(point, 1);
    // The first fit to all machines would otherwise zoom out again.
    this.fitted = true;
    this.shapes.get(id)?.openTooltip(point);
  }
}
