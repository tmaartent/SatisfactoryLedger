import { DatePipe, DecimalPipe } from '@angular/common';
import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  linkedSignal,
  resource,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import * as L from 'leaflet';

import { Api } from '../../api/api';

import { getResourceNodes } from '../../api/fn/catalog/get-resource-nodes';
import { analyseSnapshot } from '../../api/fn/world/analyse-snapshot';
import { getArrangements } from '../../api/fn/world/get-arrangements';
import { getSnapshot } from '../../api/fn/world/get-snapshot';
import { getWorldMap } from '../../api/fn/world/get-world-map';
import { traceFactories } from '../../api/fn/world/trace-factories';
import { DronePortFinding } from '../../api/models/drone-port-finding';
import { FootprintDocument } from '../../api/models/footprint-document';
import { PositionDocument } from '../../api/models/position-document';
import { GameIcon } from '../../shared/game-icon';
import { buildingShape } from './building-shape';
import { coveredSides } from './visible-part';
import { holdToExtent } from './hold-to-extent';
import { MapArt } from './map-art';
import { removeMap } from './remove-map';
import { HiddenNodes } from './hidden-nodes';
import { nodeKey, nodeKinds, Purities, shownNodes } from './node-filter';
import { CatalogNames } from '../../shared/catalog-names';
import { perMinute, percent, positionInMetres } from '../../shared/display';
import { problemMessage, whenAnalysed } from '../../shared/problem';
import { runSummary } from '../arrangement/arrangement-model';
import { Area, areaBetween } from '../area/area-model';
import { AreaSelection } from './area-selection';
import { OutlinedFactory, factoryColour, machineOwners } from '../area/factory-outlines';
import {
  MachineStatus,
  buildingStyle,
  conveyorColours,
  describeContents,
  dragColours,
  describeHeadLift,
  describeDronePort,
  describePipeFlow,
  shortOfHeadLift,
  escapeHtml,
  fromMapPoint,
  locate,
  buildingsShown,
  iconTag,
  nodeMarkerHtml,
  nodeMarkerSize,
  nodeTakers,
  mapExtent,
  mapArtTiles,
  machineStatuses,
  purityColours,
  sampleCurve,
  statusColours,
  statusLabels,
  toMapPoint,
} from './map-model';
import { SidebarTab } from '../../sidebar/sidebar-tab';
import { SidebarTabs } from '../../sidebar/sidebar-tabs';
import { Explainer, HeldBackMachine } from '../explain/explainer';
import { LimitFacts, limitText } from '../limit-text';
import { FindingsSummary } from '../findings-summary';
import { ShownStatus } from '../shown-status';
import { WhatItHolds } from '../what-it-holds';
import { loaded } from '../../shared/loaded';

/** Every kilometre a grid line, over the whole game world (about −3.3 to 4.3 km east, −3.8 to 3.8 km north). */
const GridStep = 1000;
const GridExtent = { from: -5000, to: 5000 };
/** How far out the map zooms without the map picture: the whole grid. */
const MinZoom = -4;

/**
 * The world map of a snapshot: its machines by how they run, its belts and pipes as built with the bottlenecks red,
 * its providers, the resource nodes still free, and a factory highlighted. A plain grid in metres behind it (ADR-0020).
 * Where the site starts, with no snapshot loaded: only the resource nodes (asked 2026-11-19).
 */
@Component({
  selector: 'app-map-page',
  imports: [
    FormsModule,
    RouterLink,
    GameIcon,
    DatePipe,
    DecimalPipe,
    FindingsSummary,
    WhatItHolds,
    SidebarTab,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './map-page.html',
  styleUrl: './map-page.scss',
})
export class MapPage {
  private readonly api = inject(Api);
  private readonly names = inject(CatalogNames);
  private readonly router = inject(Router);
  private readonly explainer = inject(Explainer);

  /** From the route: /snapshots/:id/map; none at /snapshots, where the site starts with no snapshot loaded. */
  readonly id = input<string>();
  /** The snapshot's id, for its requests; with none they don't run. */
  private readonly snapshotParams = computed(() => {
    const id = this.id();
    return id ? { id } : undefined;
  });
  /** From the query, ?focus=<buildable id>: the buildable a finding links to. The map zooms to it and rings it. */
  readonly focus = input<string>();

  protected readonly snapshot = resource({
    params: () => this.snapshotParams(),
    loader: ({ params }) => this.api.invoke(getSnapshot, params),
  });
  protected readonly world = resource({
    params: () => this.snapshotParams(),
    loader: ({ params }) => this.api.invoke(getWorldMap, params),
  });
  /** The machines' status: the map shows them grey until the analysis is ready, then colours them. */
  protected readonly analysis = resource({
    params: () => this.snapshotParams(),
    loader: ({ params, abortSignal }) =>
      whenAnalysed(() => this.api.invoke$Response(analyseSnapshot, params), abortSignal),
  });
  protected readonly nodes = resource({ loader: () => this.api.invoke(getResourceNodes) });
  /** The game's map picture under everything (ADR-0020), shared with the optimal arrangement's map. */
  protected readonly mapArt = inject(MapArt).art;
  protected readonly factories = resource({
    params: () => this.snapshotParams(),
    loader: ({ params }) => this.api.invoke(traceFactories, params),
  });
  /**
   * The world's runs of the optimal arrangement, newest first, for the Arrangements tab (asked 2026-11-25: the
   * arrangement was reached only by drawing an area). The runs are the world's, as on the arrangement page.
   */
  protected readonly arrangements = resource({
    params: () => {
      const world = loaded(this.snapshot)?.sessionName;
      return world ? { world } : undefined;
    },
    loader: ({ params }) => this.api.invoke(getArrangements, params),
  });
  protected readonly runSummary = runSummary;

  /** ?factory=…: the factory highlighted when the map opens; the list beside the map can pick another. */
  readonly factory = input<string>();
  protected readonly factoryId = linkedSignal(() => this.factory() ?? null);
  /** The kinds of node hidden, picked in the Resource nodes tab (asked 2026-11-23). */
  protected readonly hiddenNodes = inject(HiddenNodes);
  /** The tab's rows: a resource each, its nodes counted per purity. */
  protected readonly nodeKinds = computed(() =>
    nodeKinds(loaded(this.nodes) ?? [], (resource) => this.names.item(resource)),
  );
  protected readonly purities = Purities;
  protected readonly nodeKey = nodeKey;
  protected readonly pointer = signal<string>('');
  /** Whether the legend is unfolded: on a phone it's folded behind a button, as it covers the map (asked 2026-11-24). */
  protected readonly legendOpen = signal(false);
  /**
   * Drawing a rectangle, to open in 3D (shift held) or to arrange its factories (ctrl held): the map stops panning, and
   * a drag draws instead. Null while not drawing.
   */
  protected readonly drawing = signal<'3d' | 'arrangement' | null>(null);
  /** Selecting an area on a touch screen, with no keys to hold (asked 2026-11-24, ADR-0033). */
  protected readonly selection = new AreaSelection(
    ({ from, to }) => {
      const area = toArea(from, to);
      return area.x1 - area.x0 >= 200 && area.y1 - area.y0 >= 200;
    },
    { color: '#ffeb3b', weight: 2, fillOpacity: 0.1 },
  );
  /** The area selected, once let go: to open in 3D or to arrange. */
  protected readonly selected = computed(() => {
    const drawn = this.selection.drawn();
    return drawn ? toArea(drawn.from, drawn.to) : null;
  });

  private readonly statuses = computed(() => machineStatuses(loaded(this.analysis)?.machines));
  /** Generators burning a known fuel, by id (BR-A15): coloured by how they run, like machines. */
  private readonly generators = computed(
    () =>
      new Map(
        (loaded(this.analysis)?.generators ?? []).map((generator) => [generator.id, generator]),
      ),
  );
  private readonly utilisations = computed(
    () =>
      new Map(
        (loaded(this.analysis)?.machines ?? []).map((machine) => [machine.id, machine.utilisation]),
      ),
  );
  /** Each machine's finding, by id: its tooltip says what it's short of or can't get rid of (BR-A11). */
  private readonly machineFindings = computed(
    () => new Map((loaded(this.analysis)?.machines ?? []).map((machine) => [machine.id, machine])),
  );
  /** Each drone port's finding, by id: its tooltip says what its drones burn, or that none refuels there (BR-A16). */
  private readonly dronePorts = computed(
    () => new Map((loaded(this.analysis)?.dronePorts ?? []).map((port) => [port.id, port])),
  );
  /** What flows through each pipe in the model, by id (asked 2026-11-24). */
  private readonly pipeFlows = computed(
    () => new Map((loaded(this.analysis)?.pipeFlows ?? []).map((flow) => [flow.id, flow])),
  );
  protected readonly headLiftColour = conveyorColours.headLift;
  private readonly bottlenecks = computed(
    () =>
      new Map(
        (loaded(this.analysis)?.bottlenecks ?? []).map((bottleneck) => [bottleneck.id, bottleneck]),
      ),
  );
  /** Each building class's footprint, by class name; a class without one is drawn as a dot. */
  private readonly footprints = computed(
    () =>
      new Map(
        (loaded(this.world)?.footprints ?? []).map(
          (footprint) => [footprint.id, footprint] as [string, FootprintDocument],
        ),
      ),
  );
  private readonly factoryMachines = computed(() => {
    const factory = loaded(this.factories)?.find((trace) => trace.factoryId === this.factoryId());
    return factory ? new Set(factory.machines.map((machine) => machine.id)) : null;
  });
  /** A count picked in the Info tab's overview: its machines or generators are shown (asked 2026-11-23). */
  protected readonly shownStatus = signal<ShownStatus | null>(null);
  /** A factory's building tapped once on a touch screen, by id: a second tap on it opens the factory (ADR-0033). */
  private readonly tapped = signal<string | null>(null);
  /**
   * The buildings stood out, the others dimmed: those of the status picked in the overview, or else the highlighted
   * factory's. Only one at a time: picking either lets go of the other.
   */
  private readonly highlighted = computed(
    () => buildingsShown(loaded(this.analysis), this.shownStatus()) ?? this.factoryMachines(),
  );
  /** Who each machine belongs to, with the owner's colour: the same edges as in 3D. */
  private readonly owners = computed(() => machineOwners(loaded(this.factories) ?? []));
  /** The factories to choose from, by name, each with the colour its machines are outlined in. */
  protected readonly factoryOptions = computed(() => {
    const factories = loaded(this.factories) ?? [];
    return factories
      .map((factory) => ({ ...factory, colour: factoryColour(factories, factory.factoryId) }))
      .sort((one, other) => one.name.localeCompare(other.name));
  });

  protected readonly legend = (
    ['none', 'starved', 'outputBlocked', 'noRecipe', 'unknown'] as MachineStatus[]
  ).map((status) => ({
    status,
    label: statusLabels[status],
    colour: statusColours[status],
  }));
  protected readonly problem = problemMessage;
  protected readonly loaded = loaded;

  private readonly container = viewChild.required<ElementRef<HTMLElement>>('map');
  private map?: L.Map;
  private readonly machineLayer = L.layerGroup();
  private readonly conveyorLayer = L.layerGroup();
  private readonly providerLayer = L.layerGroup();
  private readonly otherLayer = L.layerGroup();
  private readonly nodeLayer = L.layerGroup();
  private readonly artLayer = L.layerGroup();
  private readonly focusLayer = L.layerGroup();
  /** Rings around the buildings of the status picked in the overview. */
  private readonly shownLayer = L.layerGroup();
  /** Every drawn buildable's shape, by id: to open the tooltip of the one in focus. */
  private readonly shapes = new Map<string, L.Path>();
  private fitted = false;
  private readonly sidebar = inject(SidebarTabs);
  /**
   * What the map last zoomed to, to zoom to it again when the open tab folds, opens or changes height (asked
   * 2026-11-24); forgotten once the player drags or zooms the map, or lets go of what's highlighted.
   */
  private lastFit: { bounds: L.LatLngBounds; margin: number; maxZoom?: number } | null = null;
  /** Zooming to something: the zoom that starts is the map's, not the player's. */
  private fitting = false;

  constructor() {
    afterNextRender(() => this.createMap());
    // Leaflet follows the window's size only: the sidebar beside the page also changes the map's (asked 2026-11-22).
    const resized = new ResizeObserver(() => this.map?.invalidateSize());
    afterNextRender(() => resized.observe(this.container().nativeElement));
    inject(DestroyRef).onDestroy(() => {
      resized.disconnect();
      if (this.map) removeMap(this.map);
    });

    // Redraw a layer when what it shows changes; Leaflet itself knows nothing about signals.
    effect(() => this.drawConveyors());
    effect(() => this.drawMachines());
    effect(() => this.drawProviders());
    effect(() => this.drawOthers());
    effect(() => this.drawNodes());
    effect(() => this.drawMapArt());
    // Again with the snapshot's buildings, which may stand past the picture.
    effect(() => this.holdToMapArt());
    effect(() => this.fitToHighlighted());
    // Last, so the shapes it opens a tooltip on are drawn.
    effect(() => this.focusOnBuildable());
    // The open tab folded, opened or changed height: what was zoomed to, centred again in what's left in view, once the
    // page has its new layout.
    effect(() => {
      this.sidebar.collapsed();
      this.sidebar.sheet();
      setTimeout(() => this.fitAgain());
    });
  }

  private createMap(): void {
    this.map = L.map(this.container().nativeElement, {
      crs: L.CRS.Simple,
      preferCanvas: true,
      minZoom: MinZoom,
      // Shift and drag draws an area to see in 3D (listenForArea), not a box to zoom to.
      boxZoom: false,
      maxZoom: 3,
      zoomSnap: 0.25,
      // Held to the map picture, once there is one: it stops at its edges instead of giving way a little.
      maxBoundsViscosity: 1,
      attributionControl: false,
    });
    this.map.on('resize', () => this.holdToMapArt());
    // A click anywhere else on the map lets go of what's stood out, a factory or a count, so nothing is faded (asked
    // 2026-11-23). A shape that does something when clicked stops its click; drawing an area leaves it be.
    this.map.on('click', () => {
      if (this.drawing() || this.selection.selecting()) return;
      this.tapped.set(null);
      this.factoryId.set(null);
      this.shownStatus.set(null);
    });
    // The resource nodes' markers read their size from the map (styles.scss): sized again for each zoom, not redrawn. An
    // animated zoom says where it goes as it starts, so they grow along with the map; others end with 'zoom'.
    const sizeNodes = (zoom = this.map!.getZoom()) =>
      this.container().nativeElement.style.setProperty('--node-size', `${nodeMarkerSize(zoom)}px`);
    // The player moves the map: what was zoomed to is theirs to find again.
    this.map.on('dragstart', () => (this.lastFit = null));
    this.map.on('zoomstart', () => {
      if (!this.fitting) this.lastFit = null;
    });
    this.map.on('moveend', () => (this.fitting = false));
    this.map.on('zoomanim', (event) => sizeNodes((event as L.ZoomAnimEvent).zoom));
    this.map.on('zoom', () => sizeNodes());
    // The map picture first, so everything else lies over it.
    this.artLayer.addTo(this.map);
    this.gridLayer().addTo(this.map);
    this.nodeLayer.addTo(this.map);
    this.otherLayer.addTo(this.map);
    this.conveyorLayer.addTo(this.map);
    this.providerLayer.addTo(this.map);
    this.machineLayer.addTo(this.map);
    this.shownLayer.addTo(this.map);
    this.focusLayer.addTo(this.map);
    this.map.setView([0, 0], -3);
    sizeNodes();
    this.listenForArea(this.map);
    this.selection.attach(this.map);
    this.map.on('mousemove', (event: L.LeafletMouseEvent) =>
      this.pointer.set(
        positionInMetres({ x: event.latlng.lng * 100, y: -event.latlng.lat * 100, z: 0 }).replace(
          /, 0 m$/,
          ' m',
        ),
      ),
    );
    this.drawConveyors();
    this.drawMachines();
    this.drawProviders();
    this.drawOthers();
    this.drawNodes();
    this.drawMapArt();
    this.focusOnBuildable();
  }

  /** The game's map picture, a tile at a time over its part of the world; not clickable, so it never takes a click. */
  private drawMapArt(): void {
    const art = loaded(this.mapArt) ?? null;
    if (!this.map) return;

    this.artLayer.clearLayers();
    for (const tile of mapArtTiles(art))
      L.imageOverlay(tile.url, tile.bounds, { interactive: false }).addTo(this.artLayer);
    this.holdToMapArt();
  }

  /**
   * With the map picture, the map zooms out no further than the whole picture and moves no further than its edges: past
   * them is only the grid (asked 2026-11-20). Buildings past its edges, as the game allows, take the edges with them
   * (asked 2026-11-23). Without one, the whole world's grid can be seen, as before.
   */
  private holdToMapArt(): void {
    if (!this.map) return;
    const extent = mapExtent(loaded(this.mapArt) ?? null, loaded(this.world) ?? null);
    holdToExtent(this.map, extent, MinZoom);
  }

  /** A factory picked in the list beside the map is highlighted; picked again, none is (asked 2026-11-19). */
  protected toggleFactory(factoryId: string): void {
    this.shownStatus.set(null);
    this.factoryId.update((highlighted) => (highlighted === factoryId ? null : factoryId));
  }

  /** A count clicked in the overview shows its buildings; clicked again, none (asked 2026-11-23). */
  protected showStatus(status: ShownStatus | null): void {
    this.factoryId.set(null);
    this.shownStatus.set(status);
  }

  /** Opens the area selected: in 3D, or in the optimal arrangement. */
  protected openSelected(page: 'area' | 'arrangement'): void {
    const area = this.selected();
    if (!area) return;
    this.selection.toggle();
    void this.router.navigate(['/snapshots', this.id(), page], { queryParams: area });
  }

  /** Stops drawing an area: the map pans again on a drag. */
  private stopDrawing(): void {
    this.drawing.set(null);
    this.map?.dragging.enable();
  }

  /**
   * A drag with a key held draws a rectangle, and letting go opens what's inside it: with shift, in 3D (asked
   * 2026-11-23, as defining a factory in 3D does; it was a button); with ctrl (or ⌘ on a Mac), in the optimal
   * arrangement (asked 2026-11-23). The map listens in the capture phase, so it hears the press before Leaflet's panning
   * does, and can switch the panning off in time. Leaflet's own shift-drag, zooming to a box, is off.
   */
  private listenForArea(map: L.Map): void {
    let start: L.LatLng | null = null;
    let rectangle: L.Rectangle | null = null;
    // What the drag opens, kept from its start: letting go ends drawing before Leaflet's mouseup comes.
    let opens: '3d' | 'arrangement' | null = null;
    map.getContainer().addEventListener(
      'pointerdown',
      (event) => {
        const kind = event.shiftKey ? '3d' : event.ctrlKey || event.metaKey ? 'arrangement' : null;
        if (!this.id() || !kind || event.button !== 0) return;
        this.drawing.set(kind);
        map.dragging.disable();
      },
      { capture: true },
    );
    // However the press ends, panning comes back: a press on a resource node's pin never reaches the map's mousedown.
    map.getContainer().addEventListener('pointerup', () => {
      if (this.drawing()) this.stopDrawing();
    });
    map.on('mousedown', (event: L.LeafletMouseEvent) => {
      const kind = this.drawing();
      if (!kind) return;
      opens = kind;
      start = event.latlng;
      rectangle = L.rectangle(L.latLngBounds(start, start), {
        color: dragColours[kind],
        weight: 2,
        fillOpacity: 0.1,
        interactive: false,
      }).addTo(map);
    });
    map.on('mousemove', (event: L.LeafletMouseEvent) => {
      if (start && rectangle) rectangle.setBounds(L.latLngBounds(start, event.latlng));
    });
    map.on('mouseup', (event: L.LeafletMouseEvent) => {
      if (!start) return;
      const area = areaBetween(
        fromMapPoint(start.lat, start.lng),
        fromMapPoint(event.latlng.lat, event.latlng.lng),
      );
      const page = opens === 'arrangement' ? 'arrangement' : 'area';
      rectangle?.remove();
      [start, rectangle, opens] = [null, null, null];
      this.stopDrawing();
      // A click without a drag draws nothing.
      if (area.x1 - area.x0 < 200 || area.y1 - area.y0 < 200) return;
      void this.router.navigate(['/snapshots', this.id(), page], { queryParams: area });
    });
  }

  /** A building as the ground it covers, or a dot without a footprint: as the optimal arrangement draws it (buildingShape). */
  private shape(
    position: PositionDocument,
    yaw: number,
    className: string,
    style: L.PathOptions,
    dot = 4,
  ): L.Path {
    return buildingShape({ className, position, yaw }, this.footprints(), style, dot);
  }

  private gridLayer(): L.LayerGroup {
    const grid = L.layerGroup();
    const style: L.PolylineOptions = {
      color: '#ffffff',
      opacity: 0.08,
      weight: 1,
      interactive: false,
    };
    for (let metres = GridExtent.from; metres <= GridExtent.to; metres += GridStep) {
      L.polyline(
        [
          [GridExtent.from, metres],
          [GridExtent.to, metres],
        ],
        style,
      ).addTo(grid);
      L.polyline(
        [
          [metres, GridExtent.from],
          [metres, GridExtent.to],
        ],
        style,
      ).addTo(grid);
    }
    return grid;
  }

  private drawMachines(): void {
    const machines = loaded(this.world)?.machines ?? [];
    const statuses = this.statuses();
    const utilisations = this.utilisations();
    const findings = this.machineFindings();
    const highlighted = this.highlighted();
    const owners = this.owners();
    if (!this.map) return;

    this.machineLayer.clearLayers();
    const points: L.LatLngExpression[] = [];
    for (const machine of machines) {
      const status: MachineStatus = statuses.get(machine.id) ?? 'unknown';
      const inFactory = highlighted?.has(machine.id) ?? false;
      const owner = owners.get(machine.id)?.owner;
      const point = toMapPoint(machine.position);
      points.push(point);
      const utilisation = utilisations.get(machine.id);
      const finding = findings.get(machine.id);
      // A held-back machine of no factory: a click explains it; one of a factory opens the factory, which explains it.
      const explains = !owner && finding !== undefined && this.explainer.explainable(finding);
      // A factory's machines are edged in its colour, boldly when it is the one highlighted.
      const style = buildingStyle(
        statusColours[status],
        owner?.colour,
        highlighted ? inFactory : null,
      );
      const shape = this.shape(machine.position, machine.yaw, machine.className, style)
        .bindTooltip(
          `${this.icon(machine.className)}<b>${this.names.building(machine.className)}</b> · ${this.names.recipe(machine.recipe)}<br>` +
            `${statusLabels[status]}${utilisation !== undefined ? ` · ${percent(utilisation)}` : ''}<br>` +
            (finding ? this.limitLine(finding) : '') +
            (machine.inputContents ? `In: ${this.contents(machine.inputContents)}<br>` : '') +
            (machine.outputContents ? `Out: ${this.contents(machine.outputContents)}<br>` : '') +
            this.factoryLine(owner) +
            (explains ? 'Click to explain why<br>' : '') +
            positionInMetres(machine.position),
        )
        .addTo(this.machineLayer);
      this.opensFactory(shape, machine.id, owner);
      this.reopenTapped(machine.id, shape, owner);
      if (explains && finding)
        this.explainsWhy(shape, {
          className: machine.className,
          recipe: machine.recipe,
          limit: finding.limit,
        });
      this.shapes.set(machine.id, shape);
    }

    // The first time there's something to show, zoom to it.
    if (!this.fitted && points.length > 0) {
      this.fitVisible(L.latLngBounds(points), 24);
      this.fitted = true;
    }
  }

  /** Belts and pipes as built (their saved curves); a bottleneck red, with what it carries and what's offered on top. */
  private drawConveyors(): void {
    const conveyors = loaded(this.world)?.conveyors ?? [];
    const bottlenecks = this.bottlenecks();
    const flows = this.pipeFlows();
    if (!this.map) return;

    this.conveyorLayer.clearLayers();
    for (const conveyor of conveyors) {
      const bottleneck = bottlenecks.get(conveyor.id);
      const short = shortOfHeadLift(conveyor);
      const colour = bottleneck
        ? conveyorColours.bottleneck
        : short
          ? conveyorColours.headLift
          : conveyorColours[conveyor.kind];
      const line = L.polyline(sampleCurve(conveyor.curve), {
        color: colour,
        weight: bottleneck ? 4 : short ? 3.5 : conveyor.kind === 'pipe' ? 2.5 : 1.5,
        opacity: bottleneck || short ? 1 : 0.6,
      });
      const carries = this.contents(conveyor.contents);
      const flow = flows.get(conveyor.id);
      line.bindTooltip(
        (bottleneck
          ? `<b>Bottleneck</b> · ${this.icon(conveyor.className)}${this.names.building(conveyor.className)}<br>` +
            `carries ${perMinute(bottleneck.flow)} of ${perMinute(bottleneck.capacity)}, ${perMinute(bottleneck.offeredMore)} more offered`
          : `${this.icon(conveyor.className)}<b>${this.names.building(conveyor.className)}</b>`) +
          (carries ? `<br>${conveyor.kind === 'pipe' ? 'In it (m³)' : 'On it'}: ${carries}` : '') +
          // Neither is in the save: the model's flow, and its estimate of head lift (BR-A22).
          (flow
            ? `<br>In the model: ${describePipeFlow(flow, (item) => this.names.item(item))}`
            : '') +
          (conveyor.headLiftToSpare != null
            ? `<br>Head lift in the model: ${describeHeadLift(conveyor.headLiftToSpare)}`
            : ''),
      );
      line.addTo(this.conveyorLayer);
      this.shapes.set(conveyor.id, line);
    }
  }

  private drawProviders(): void {
    const providers = loaded(this.world)?.providers ?? [];
    const dronePorts = this.dronePorts();
    if (!this.map) return;

    this.providerLayer.clearLayers();
    for (const provider of providers) {
      const shape = this.shape(
        provider.position,
        provider.yaw,
        provider.className,
        {
          color: provider.purity ? purityColours[provider.purity] : '#42a5f5',
          weight: 2,
          fillColor: '#42a5f5',
          fillOpacity: 0.6,
        },
        5,
      )
        .bindTooltip(
          `${this.icon(provider.className)}<b>${this.names.building(provider.className)}</b>` +
            (provider.resource
              ? ` · ${this.icon(provider.resource)}${this.names.item(provider.resource)}`
              : '') +
            (provider.purity ? ` · ${provider.purity}` : '') +
            '<br>' +
            this.dronePortLine(dronePorts.get(provider.id)) +
            positionInMetres(provider.position),
        )
        .addTo(this.providerLayer);
      this.shapes.set(provider.id, shape);
    }
  }

  /** Splitters, mergers, lifts, storage, and the consumers belts end in (BR-W8): so belts don't stop in mid-air. */
  private drawOthers(): void {
    const world = loaded(this.world);
    const generators = this.generators();
    const dronePorts = this.dronePorts();
    if (!this.map) return;

    this.otherLayer.clearLayers();
    const owners = this.owners();
    const highlighted = this.highlighted();
    const consumerColour = '#ab47bc';
    const style = (consumer: boolean): L.PathOptions => ({
      color: consumer ? consumerColour : '#78909c',
      weight: 1,
      fillColor: consumer ? consumerColour : '#546e7a',
      fillOpacity: 0.5,
    });
    for (const [buildings, consumer] of [
      [world?.logistics ?? [], false],
      [world?.consumers ?? [], true],
    ] as const) {
      for (const building of buildings) {
        // A generator burning a known fuel shows how it runs (BR-A15), in the machines' colours.
        const generator = consumer ? generators.get(building.id) : undefined;
        const colour = generator ? statusColours[generator.limit] : undefined;
        const owner = owners.get(building.id)?.owner;
        const shape = this.shape(
          building.position,
          building.yaw,
          building.className,
          // A generator of a factory is edged in its colour, as its machines are. Dimmed too while others stand out.
          colour || owner || highlighted
            ? buildingStyle(
                colour ?? consumerColour,
                owner?.colour,
                highlighted ? highlighted.has(building.id) : null,
              )
            : style(consumer),
          2,
        )
          .bindTooltip(
            `${this.icon(building.className)}<b>${this.names.building(building.className)}</b><br>` +
              (generator
                ? `Burns ${this.icon(generator.fuel)}${this.names.item(generator.fuel)} · ${statusLabels[generator.limit]} · ${percent(generator.utilisation)}<br>` +
                  this.limitLine(generator)
                : '') +
              this.dronePortLine(dronePorts.get(building.id)) +
              (building.contents ? `Holds: ${this.contents(building.contents)}<br>` : '') +
              this.factoryLine(owner) +
              positionInMetres(building.position),
          )
          .addTo(this.otherLayer);
        this.opensFactory(shape, building.id, owner);
        this.reopenTapped(building.id, shape, owner);
        this.shapes.set(building.id, shape);
      }
    }
  }

  /** What a drone port burns in the model, as a tooltip line; empty for any other building. */
  private dronePortLine(port: DronePortFinding | undefined): string {
    return port
      ? `${escapeHtml(describeDronePort(port, (item) => this.names.item(item)))}<br>`
      : '';
  }

  /** The factory a building belongs to, as a tooltip line; empty when it belongs to none. */
  private factoryLine(owner: OutlinedFactory | undefined): string {
    // On a touch screen the first tap shows this; a second opens the factory (opensFactory).
    const open = touchScreen() ? 'tap again to open' : 'click to open';
    return owner ? `Factory <b>${escapeHtml(owner.name)}</b> · ${open}<br>` : '';
  }

  /**
   * A click on a factory's building opens the factory's page; not while drawing an area for 3D. On a touch screen, where
   * nothing hovers to show what it is first (asked 2026-11-24, ADR-0033): the first tap says what it is, in its tooltip,
   * and highlights its factory; a second tap on the same building opens the page.
   */
  private opensFactory(shape: L.Path, id: string, owner: OutlinedFactory | undefined): void {
    if (!owner) return;
    shape.on('click', (event: L.LeafletMouseEvent) => {
      if (this.drawing() || this.selection.selecting()) return;
      L.DomEvent.stopPropagation(event);
      if (touchScreen() && (this.tapped() !== id || this.factoryId() !== owner.factoryId)) {
        this.shownStatus.set(null);
        this.factoryId.set(owner.factoryId);
        this.tapped.set(id);
        this.openLabel(shape, owner);
        return;
      }
      this.openFactory(owner);
    });
  }

  /** The building tapped once on a touch screen: its label open again when the map is drawn anew, as it highlights. */
  private reopenTapped(id: string, shape: L.Path, owner: OutlinedFactory | undefined): void {
    if (owner && untracked(this.tapped) === id) this.openLabel(shape, owner);
  }

  /**
   * A factory's building's tooltip, opened by a tap: a tap on it opens the factory too, as a second tap on the building
   * does (asked 2026-11-24: "tap again to open" invites one). Leaflet's tooltips take no pointer; this one does.
   */
  private openLabel(shape: L.Path, owner: OutlinedFactory): void {
    shape.openTooltip();
    const label = shape.getTooltip()?.getElement();
    if (!label) return;
    label.style.pointerEvents = 'auto';
    label.onclick = (event) => {
      // Not on to the map, whose click lets go of the highlight.
      event.stopPropagation();
      this.openFactory(owner);
    };
  }

  private openFactory(owner: OutlinedFactory): void {
    void this.router.navigate(['/snapshots', this.id(), 'factories', owner.factoryId]);
  }

  /** A click on a held-back machine of no factory asks why, in the sidebar (asked 2026-11-22); not while drawing. */
  private explainsWhy(shape: L.Path, machine: HeldBackMachine): void {
    shape.on('click', (event: L.LeafletMouseEvent) => {
      if (this.drawing() || this.selection.selecting()) return;
      L.DomEvent.stopPropagation(event);
      this.explainer.explain(machine, this.id()!);
    });
  }

  /** Why it runs as it does, as a tooltip line; empty when there's nothing to say. */
  private limitLine(finding: LimitFacts): string {
    const text = limitText(finding, (item) => this.names.item(item));
    return text ? `${text}<br>` : '';
  }

  /** The game's icon of a class, before its name in a tooltip; nothing when it has none. */
  private icon(className: string | null | undefined): string {
    return iconTag(this.names.icon(className));
  }

  /** Contents in words, with the Catalog's item names. */
  private contents(contents: Record<string, number> | null | undefined): string {
    return describeContents(contents, (item) => this.names.item(item));
  }

  /** The Unused supply tab (asked 2026-11-23): the sources that could give more, the most left over first. */
  protected readonly unusedSupply = computed(() =>
    [...(loaded(this.analysis)?.unusedSupply ?? [])].sort(
      (a, b) => b.offered - b.delivered - (a.offered - a.delivered),
    ),
  );
  /** The Unknown supply tab (asked 2026-11-23): the sources whose rate the save doesn't tell, as the analysis gives them. */
  protected readonly unknownSupply = computed(() => loaded(this.analysis)?.unknownSupply ?? []);
  protected readonly perMinute = perMinute;

  /** What an unknown source is assumed to give, per item, the most first. */
  protected deliveries(delivered: Record<string, number>): { item: string; rate: number }[] {
    return Object.entries(delivered)
      .map(([item, rate]) => ({ item, rate }))
      .sort((a, b) => b.rate - a.rate);
  }

  protected recipeName(recipe: string): string {
    return this.names.recipe(recipe);
  }

  protected buildingName(className: string): string {
    return this.names.building(className);
  }

  protected itemName(item: string): string {
    return this.names.item(item);
  }

  /** A cell of the Resource nodes tab, in words: "Impure Iron Ore · 3 nodes". */
  protected nodeKindLabel(resource: string, purity: string, count: number): string {
    return `${purity.charAt(0).toUpperCase() + purity.slice(1)} ${this.names.item(resource)} · ${count} ${count === 1 ? 'node' : 'nodes'}`;
  }

  protected resourceIcon(resource: string): string | null {
    return this.names.icon(resource);
  }

  protected purityColour(purity: string): string {
    return purityColours[purity];
  }

  /** Hides every kind of node in the tab. */
  protected hideAllNodes(): void {
    this.hiddenNodes.hideAll(
      this.nodeKinds().flatMap((kind) => Purities.map((purity) => nodeKey(kind.resource, purity))),
    );
  }

  /** Every resource node, as a pin (asked 2026-11-23): free or with a miner or extractor on it, which its tooltip names. */
  private drawNodes(): void {
    const nodes = shownNodes(loaded(this.nodes) ?? [], this.hiddenNodes.hidden());
    const takers = nodeTakers(loaded(this.world)?.providers ?? []);
    if (!this.map) return;

    this.nodeLayer.clearLayers();
    // With no snapshot, nothing else to zoom to: the nodes, the first time.
    if (!this.id() && !this.fitted && nodes.length > 0) {
      this.fitVisible(L.latLngBounds(nodes.map((node) => toMapPoint(node.position))), 24);
      this.fitted = true;
    }
    for (const node of nodes) {
      const purity = purityColours[node.purity] ?? '#bdbdbd';
      const icon = this.names.icon(node.resource);
      // With the game's icons, a pin (asked 2026-11-23); without, a dashed dot.
      const marker = icon
        ? L.marker(toMapPoint(node.position), {
            // No size of its own: the pin is drawn from the node by CSS, at the map's --node-size.
            icon: L.divIcon({
              html: nodeMarkerHtml(icon, purity),
              className: '',
              iconSize: [0, 0],
              iconAnchor: [0, 0],
            }),
          })
        : L.circleMarker(toMapPoint(node.position), {
            radius: 5,
            color: purity,
            weight: 1.5,
            dashArray: '3 3',
            fill: false,
          });
      // With a snapshot, what stands on it, or that it's free; without one, nothing is known of either.
      const taker = takers.get(node.name);
      const standing = !this.id()
        ? ''
        : taker
          ? ` · ${this.icon(taker.className)}${this.names.building(taker.className)} on it`
          : ' · free';
      marker
        .bindTooltip(
          `${node.purity.charAt(0).toUpperCase() + node.purity.slice(1)} node · ${iconTag(icon)}${this.names.item(node.resource)}${standing}` +
            `<br>${positionInMetres(node.position)}`,
        )
        .addTo(this.nodeLayer);
    }
  }

  /** Zoom to the buildings stood out, a factory's or a status's, and ring each one: a machine is small from afar. */
  private fitToHighlighted(): void {
    const highlighted = this.highlighted();
    const world = loaded(this.world);
    if (!this.map) return;

    this.shownLayer.clearLayers();
    if (!highlighted) this.lastFit = null;
    if (!highlighted || !world) return;
    const points = [
      ...[...world.machines, ...world.consumers]
        .filter((building) => highlighted.has(building.id))
        .map((building) => toMapPoint(building.position)),
      // The bottlenecks, picked in the overview: belts and pipes, ringed halfway along.
      ...world.conveyors
        .filter((conveyor) => highlighted.has(conveyor.id))
        .map((conveyor) => locate(world, conveyor.id))
        .filter((point) => point !== null),
    ];
    // A factory's machines are edged in its colour already; a status's, scattered over the world, are ringed.
    if (this.shownStatus()) {
      for (const point of points)
        L.circleMarker(point, {
          radius: 12,
          color: '#ffeb3b',
          weight: 2,
          fill: false,
          interactive: false,
        }).addTo(this.shownLayer);
    }
    if (points.length > 0) this.fitVisible(L.latLngBounds(points), 48, 1);
  }

  /**
   * Zooms to what's within these bounds, this many pixels in from the edges, and centres it in the part of the map the
   * open tab leaves in view (asked 2026-11-24): above a sheet on a phone, left of a tab lying over the map.
   */
  private fitVisible(bounds: L.LatLngBounds, margin: number, maxZoom?: number): void {
    if (!this.map) return;
    // The sidebar is the shell's, beside the page: where its open tab lies, as the browser has laid it out.
    const tab = document.querySelector('app-sidebar .tab');
    const covered = coveredSides(
      this.container().nativeElement.getBoundingClientRect(),
      tab?.getBoundingClientRect() ?? null,
    );
    this.lastFit = { bounds, margin, maxZoom };
    this.fitting = true;
    this.map.fitBounds(bounds, {
      paddingTopLeft: [margin + covered.left, margin + covered.top],
      paddingBottomRight: [margin + covered.right, margin + covered.bottom],
      maxZoom,
    });
  }

  /** What was zoomed to last, again, in what's now in view; nothing once the player has moved the map. */
  private fitAgain(): void {
    if (!this.map || !this.lastFit) return;
    // Beside the page, the sidebar changes the map's size too: Leaflet's own idea of it first.
    this.map.invalidateSize({ pan: false });
    const { bounds, margin, maxZoom } = this.lastFit;
    this.fitVisible(bounds, margin, maxZoom);
  }

  /** A finding's buildable (?focus=): zoom to it, ring it, and open its tooltip. Nothing when the map doesn't have it. */
  private focusOnBuildable(): void {
    const world = loaded(this.world);
    const id = this.focus();
    if (!this.map || !world || !id) return;

    const point = locate(world, id);
    this.focusLayer.clearLayers();
    if (!point) return;

    L.circleMarker(point, {
      radius: 22,
      color: '#ffeb3b',
      weight: 3,
      fill: false,
      interactive: false,
    }).addTo(this.focusLayer);
    this.fitVisible(L.latLngBounds([point, point]), 0, 1);
    // The first fit to all machines would otherwise zoom out again.
    this.fitted = true;
    this.shapes.get(id)?.openTooltip(point);
  }
}

/** The area between two corners on the map, in the world's centimetres. */
function toArea(from: L.LatLng, to: L.LatLng): Area {
  return areaBetween(fromMapPoint(from.lat, from.lng), fromMapPoint(to.lat, to.lng));
}

/** A touch screen: nothing hovers, as the stylesheets ask with `@media (hover: none)`. */
function touchScreen(): boolean {
  return window.matchMedia?.('(hover: none)').matches ?? false;
}
