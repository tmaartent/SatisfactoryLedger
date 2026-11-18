import { DatePipe, DecimalPipe } from '@angular/common';
import {
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  computed,
  effect,
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
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import * as L from 'leaflet';

import { Api } from '../../api/api';
import { getArrangement } from '../../api/fn/world/get-arrangement';
import { getArrangements } from '../../api/fn/world/get-arrangements';
import { getSnapshot } from '../../api/fn/world/get-snapshot';
import { getWorldMap } from '../../api/fn/world/get-world-map';
import { requestArrangement } from '../../api/fn/world/request-arrangement';
import { traceFactories } from '../../api/fn/world/trace-factories';
import { ArrangementResponse } from '../../api/models/arrangement-response';
import { ArrangementSummary } from '../../api/models/arrangement-summary';
import { WorldRectangle } from '../../api/models/world-rectangle';
import { problemMessage } from '../../shared/problem';
import { factoryColour, machineOwners } from '../area/factory-outlines';
import { toMapPoint } from '../map/map-model';
import {
  areaBetween,
  foundations,
  mapBounds,
  metres,
  movedMetres,
  saving,
} from './arrangement-model';

/** What stays where it is and takes its plots: the stations' classes, as the arrangement counts them. */
const areaStyle: L.PathOptions = { color: '#ffeb3b', weight: 2, fillOpacity: 0.05 };
const exclusionStyle: L.PathOptions = {
  color: '#ef5350',
  weight: 2,
  dashArray: '4 4',
  fillColor: '#ef5350',
  fillOpacity: 0.25,
};
const stationClasses = new Set([
  'Build_TrainDockingStation_C',
  'Build_TrainDockingStationLiquid_C',
  'Build_TruckStation_C',
  'Build_DroneStation_C',
]);

/**
 * The optimal arrangement (decided 2026-11-12): draw an area, and see where its factories would best stand on plots in
 * rows and columns, so the belt work to their inputs is least. A proposal only: runs are computed by the analysis
 * service and every run is kept.
 */
@Component({
  selector: 'app-arrangement-page',
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSlideToggleModule,
    MatTooltipModule,
  ],
  templateUrl: './arrangement-page.html',
  styleUrl: './arrangement-page.scss',
})
export class ArrangementPage {
  /** The snapshot, from the route. */
  readonly id = input.required<string>();
  /** ?run=…: the run shown; the newest when not given. */
  readonly run = input<string>();

  private readonly api = inject(Api);
  protected readonly problem = problemMessage;
  protected readonly saving = saving;

  protected readonly foundations = foundations;
  /** Plots and roads in foundations: 6 by 6 plots with roads of 4 when nothing else is picked. */
  protected readonly plotWidth = signal(6);
  protected readonly plotDepth = signal(6);
  protected readonly roadX = signal(4);
  protected readonly roadY = signal(4);
  /** How strongly the factories are kept together: the belt work one plot of spread costs (BR-O1). */
  protected readonly together = signal(2);
  protected readonly seconds = signal(60);
  /** The area drawn to arrange, in the game's centimetres. */
  protected readonly area = signal<WorldRectangle | null>(null);
  /** Where no factory may stand inside the area, in the game's centimetres: the plots they touch stay free. */
  protected readonly exclusions = signal<WorldRectangle[]>([]);
  /** What a drag on the map draws now: the area, an exclusion, or nothing (the map pans). */
  protected readonly drawing = signal<'area' | 'exclusion' | null>(null);

  protected draw(kind: 'area' | 'exclusion'): void {
    this.drawing.update((drawing) => (drawing === kind ? null : kind));
  }
  protected readonly requesting = signal(false);
  protected readonly requestError = signal<string | null>(null);

  protected readonly snapshot = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => this.api.invoke(getSnapshot, params),
  });
  private readonly world = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => this.api.invoke(getWorldMap, params),
  });
  private readonly factories = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => this.api.invoke(traceFactories, params),
  });
  /** The world's runs, newest first. */
  protected readonly runs = resource({
    params: () => {
      const world = this.snapshot.value()?.sessionName;
      return world ? { world } : undefined;
    },
    loader: ({ params }) => this.api.invoke(getArrangements, params),
  });

  protected readonly selected = linkedSignal(
    () => this.run() ?? this.runs.value()?.[0]?.id ?? null,
  );
  protected readonly arrangement = resource({
    params: () => {
      const id = this.selected();
      return id ? { id } : undefined;
    },
    loader: ({ params }) => this.api.invoke(getArrangement, params),
  });

  /** The factories of the shown run, the farthest move first. */
  protected readonly moves = computed(() =>
    (this.arrangement.value()?.factories ?? [])
      .filter((factory) => factory.proposed)
      .map((factory) => ({
        factoryId: factory.factoryId,
        name: factory.name ?? '(deleted)',
        metres: movedMetres(factory.now, factory.proposed!),
      }))
      .sort((a, b) => b.metres - a.metres),
  );

  /** Whether to show where the arranged factories stand now (their machines, and the rectangle around them). */
  protected readonly showNow = signal(false);
  /** The factories picked in the side bar: their place now, their move and their links show. */
  protected readonly focused = signal<ReadonlySet<string>>(new Set());

  protected toggleFocus(factoryId: string): void {
    this.focused.update((focused) => {
      const next = new Set(focused);
      if (!next.delete(factoryId)) next.add(factoryId);
      return next;
    });
  }

  /** Where each factory's machines stand, on the map, by factory. */
  private readonly machinePoints = computed(() => {
    const world = this.world.value();
    const owners = machineOwners(this.factories.value() ?? []);
    const points = new Map<string, [number, number][]>();
    if (!world) return points;
    for (const machine of [...world.machines, ...world.consumers]) {
      const owner = owners.get(machine.id)?.[0];
      if (owner)
        points.set(owner.factoryId, [
          ...(points.get(owner.factoryId) ?? []),
          toMapPoint(machine.position),
        ]);
    }
    return points;
  });

  private readonly container = viewChild.required<ElementRef<HTMLElement>>('map');
  private map?: L.Map;
  private readonly worldLayer = L.layerGroup();
  private readonly runLayer = L.layerGroup();
  private readonly linkLayer = L.layerGroup();
  private readonly focusLayer = L.layerGroup();
  private readonly areaLayer = L.layerGroup();
  private fitted = false;

  constructor() {
    afterNextRender(() => {
      this.map = L.map(this.container().nativeElement, {
        crs: L.CRS.Simple,
        preferCanvas: true,
        minZoom: -4,
        maxZoom: 3,
        zoomSnap: 0.25,
        attributionControl: false,
      });
      for (const layer of [
        this.worldLayer,
        this.runLayer,
        this.focusLayer,
        this.linkLayer,
        this.areaLayer,
      ])
        layer.addTo(this.map);
      this.map.setView([0, 0], -3);
      this.listenForArea(this.map);
      this.drawWorld();
      this.drawRun();
      this.drawArea();
    });
    inject(DestroyRef).onDestroy(() => this.map?.remove());
    effect(() => this.drawWorld());
    effect(() => this.drawRun());
    effect(() => this.drawArea());
    // Another run: nothing picked, and its area, exclusions and settings are where a new run starts.
    effect(() => {
      const id = this.selected();
      this.focused.set(new Set());
      const run = untracked(() => this.runs.value())?.find((candidate) => candidate.id === id);
      if (!run) return;
      this.area.set(run.area ?? null);
      this.exclusions.set([...run.exclusions]);
      this.plotWidth.set(foundations(run.plotWidthMetres));
      this.plotDepth.set(foundations(run.plotDepthMetres));
      this.roadX.set(foundations(run.roadXMetres));
      this.roadY.set(foundations(run.roadYMetres));
      this.together.set(run.together);
      this.seconds.set(run.seconds);
    });
    effect(() => (this.drawing() ? this.map?.dragging.disable() : this.map?.dragging.enable()));

    // While a run is computed, ask again every two seconds.
    effect((onCleanup) => {
      const pending = (this.runs.value() ?? []).some((run) => run.status === 'pending');
      if (!pending) return;
      const timer = setTimeout(() => {
        this.runs.reload();
        this.arrangement.reload();
      }, 2000);
      onCleanup(() => clearTimeout(timer));
    });
  }

  protected async request(): Promise<void> {
    const area = this.area();
    if (!area) return;
    this.requesting.set(true);
    this.requestError.set(null);
    try {
      const run = await this.api.invoke(requestArrangement, {
        id: this.id(),
        body: {
          area,
          exclusions: this.exclusions(),
          plotWidthMetres: metres(this.plotWidth()),
          plotDepthMetres: metres(this.plotDepth()),
          roadXMetres: metres(this.roadX()),
          roadYMetres: metres(this.roadY()),
          together: this.together(),
          seconds: this.seconds(),
        },
      });
      this.selected.set(run.id);
      this.runs.reload();
    } catch (error) {
      this.requestError.set(problemMessage(error));
    } finally {
      this.requesting.set(false);
    }
  }

  protected statusOf(run: ArrangementSummary): string {
    if (run.status === 'pending') return 'Computing…';
    if (run.status === 'failed') return `Failed: ${run.error ?? ''}`;
    return run.quality === 'optimal'
      ? 'Optimal'
      : run.quality === 'feasible'
        ? 'Best found in time'
        : 'None found';
  }

  /** While drawing, a drag draws the area to arrange, or an exclusion inside it. */
  private listenForArea(map: L.Map): void {
    let start: L.LatLng | null = null;
    let rectangle: L.Rectangle | null = null;
    map.on('mousedown', (event: L.LeafletMouseEvent) => {
      const drawing = this.drawing();
      if (!drawing) return;
      start = event.latlng;
      rectangle = L.rectangle(L.latLngBounds(start, start), {
        ...(drawing === 'area' ? areaStyle : exclusionStyle),
        interactive: false,
      }).addTo(map);
    });
    map.on('mousemove', (event: L.LeafletMouseEvent) => {
      if (start && rectangle) rectangle.setBounds(L.latLngBounds(start, event.latlng));
    });
    map.on('mouseup', (event: L.LeafletMouseEvent) => {
      if (!start) return;
      const area = areaBetween([start.lat, start.lng], [event.latlng.lat, event.latlng.lng]);
      rectangle?.remove();
      [start, rectangle] = [null, null];
      // A click without a drag draws nothing.
      if (area.x1 - area.x0 < 1000 || area.y1 - area.y0 < 1000) return;
      if (this.drawing() === 'area') this.area.set(area);
      else this.exclusions.update((exclusions) => [...exclusions, area]);
      this.drawing.set(null);
    });
  }

  /**
   * The world as it stands: each factory's machines as dots in its colour, the stations as dark dots. Shown to draw an
   * area; with a run shown, only when asked (where they stand now).
   */
  private drawWorld(): void {
    const world = this.world.value();
    const traces = this.factories.value() ?? [];
    const running = (this.arrangement.value()?.factories.length ?? 0) > 0;
    if (!this.map || !world) return;
    this.worldLayer.clearLayers();
    if (running && !this.showNow()) return;
    const owners = machineOwners(traces);
    const points: L.LatLngExpression[] = [];
    for (const machine of [
      ...world.machines,
      ...world.consumers.filter((consumer) => owners.has(consumer.id)),
    ]) {
      const owner = owners.get(machine.id)?.[0];
      const point = toMapPoint(machine.position);
      points.push(point);
      L.circleMarker(point, {
        radius: 2,
        weight: 0,
        fillColor: owner?.colour ?? '#78909c',
        fillOpacity: owner ? 0.9 : 0.4,
      })
        .bindTooltip(owner?.name ?? 'No factory')
        .addTo(this.worldLayer);
    }
    for (const station of [...world.providers, ...world.consumers].filter((building) =>
      stationClasses.has(building.className),
    )) {
      L.circleMarker(toMapPoint(station.position), {
        radius: 3,
        weight: 1,
        color: '#263238',
        fillColor: '#455a64',
        fillOpacity: 0.9,
      })
        .bindTooltip('Station')
        .addTo(this.worldLayer);
    }
    if (!this.fitted && points.length > 0) {
      this.map.fitBounds(L.latLngBounds(points), { padding: [24, 24] });
      this.fitted = true;
    }
  }

  /** The area drawn and its exclusions: what a new run would arrange. Clicking an exclusion removes it. */
  private drawArea(): void {
    const area = this.area();
    const exclusions = this.exclusions();
    if (!this.map) return;
    this.areaLayer.clearLayers();
    if (area)
      L.rectangle(L.latLngBounds(mapBounds(area)), { ...areaStyle, interactive: false }).addTo(
        this.areaLayer,
      );
    exclusions.forEach((exclusion, index) =>
      L.rectangle(L.latLngBounds(mapBounds(exclusion)), exclusionStyle)
        .bindTooltip('No factories here · click to remove')
        .on('click', () =>
          this.exclusions.update((all) => all.filter((_, other) => other !== index)),
        )
        .addTo(this.areaLayer),
    );
  }

  /**
   * The shown run: its area and where each factory would go (filled, in its colour). Where they stand now (the
   * rectangle around their machines) and the moves (a line from there) show when asked, for all or the factories picked.
   * Pointing at a factory shows its links.
   */
  private drawRun(): void {
    const arrangement = this.arrangement.value();
    const traces = this.factories.value() ?? [];
    const points = this.machinePoints();
    const [showNow, focused] = [this.showNow(), this.focused()];
    if (!this.map) return;
    this.runLayer.clearLayers();
    this.focusLayer.clearLayers();
    this.linkLayer.clearLayers();
    if (!arrangement || arrangement.factories.length === 0) return;

    if (arrangement.summary.area) {
      L.rectangle(L.latLngBounds(mapBounds(arrangement.summary.area)), {
        color: '#ffeb3b',
        weight: 1,
        dashArray: '6 6',
        fill: false,
        interactive: false,
      }).addTo(this.runLayer);
    }

    const centres = this.centres(arrangement);
    for (const factory of arrangement.factories) {
      if (!factory.proposed) continue;
      const colour = factoryColour(traces, factory.factoryId) ?? '#90a4ae';
      const name = factory.name ?? '(deleted)';
      const proposed = L.latLngBounds(mapBounds(factory.proposed));
      const metres = movedMetres(factory.now, factory.proposed);
      L.rectangle(proposed, {
        color: colour,
        weight: focused.has(factory.factoryId) ? 4 : 2,
        fillColor: colour,
        fillOpacity: 0.45,
      })
        .bindTooltip(`<b>${name}</b><br>${metres > 0 ? `moves ${metres} m` : 'stays'}`)
        .on('mouseover', () => this.drawLinks(arrangement, factory.factoryId, centres))
        .on('mouseout', () => this.linkLayer.clearLayers())
        .on('click', () => this.toggleFocus(factory.factoryId))
        .addTo(this.runLayer);

      // Where it stands now: the rectangle around its machines, as built.
      const machines = points.get(factory.factoryId) ?? [];
      const picked = focused.has(factory.factoryId);
      if (machines.length === 0 || !(showNow || picked)) continue;
      const now = L.latLngBounds(machines);
      const layer = picked ? this.focusLayer : this.runLayer;
      if (showNow || picked) {
        L.rectangle(now.pad(0.05), {
          color: colour,
          weight: 1,
          dashArray: '4 4',
          fill: false,
          opacity: 0.9,
          interactive: false,
        }).addTo(layer);
        if (picked)
          for (const point of machines)
            L.circleMarker(point, {
              radius: 2,
              weight: 0,
              fillColor: colour,
              fillOpacity: 0.9,
              interactive: false,
            }).addTo(layer);
      }
      if (picked)
        L.polyline([now.getCenter(), proposed.getCenter()], {
          color: colour,
          weight: 2,
          opacity: 0.8,
          interactive: false,
        }).addTo(layer);
      if (picked) this.drawLinks(arrangement, factory.factoryId, centres, this.focusLayer);
    }
  }

  /** Where each end of a link stands in the run: a factory's proposed place, or else where it or a fixed place stands. */
  private centres(arrangement: ArrangementResponse): Map<string, L.LatLng> {
    const centres = new Map<string, L.LatLng>();
    for (const place of arrangement.fixed)
      centres.set(place.id, L.latLngBounds(mapBounds(place.space)).getCenter());
    for (const factory of arrangement.factories)
      centres.set(
        factory.factoryId,
        L.latLngBounds(mapBounds(factory.proposed ?? factory.now)).getCenter(),
      );
    return centres;
  }

  /**
   * The links of a factory, from its proposed place: a line to each factory arranged with it, thicker for more belts.
   */
  private drawLinks(
    arrangement: ArrangementResponse,
    factoryId: string,
    centres: Map<string, L.LatLng>,
    layer: L.LayerGroup = this.linkLayer,
  ): void {
    if (layer === this.linkLayer) this.linkLayer.clearLayers();
    const factories = new Set(arrangement.factories.map((factory) => factory.factoryId));
    for (const link of arrangement.links.filter(
      (candidate) => candidate.from === factoryId || candidate.to === factoryId,
    )) {
      const other = link.from === factoryId ? link.to : link.from;
      const from = centres.get(factoryId);
      const to = centres.get(other);
      if (!from || !to) continue;
      if (!factories.has(other)) continue;
      L.polyline([from, to], {
        color: '#ffffff',
        weight: 1 + Math.log2(link.belts),
        opacity: 0.85,
        interactive: false,
      }).addTo(layer);
    }
  }
}
