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
import { MatButtonToggleModule } from '@angular/material/button-toggle';
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
import { getArrangementMachines } from '../../api/fn/world/get-arrangement-machines';
import { ArrangedMachineResponse } from '../../api/models/arranged-machine-response';
import { getArrangements } from '../../api/fn/world/get-arrangements';
import { getSnapshot } from '../../api/fn/world/get-snapshot';
import { getWorldMap } from '../../api/fn/world/get-world-map';
import { requestArrangement } from '../../api/fn/world/request-arrangement';
import { traceFactories } from '../../api/fn/world/trace-factories';
import { ArrangementResponse } from '../../api/models/arrangement-response';
import { ArrangementSummary } from '../../api/models/arrangement-summary';
import { WorldRectangle } from '../../api/models/world-rectangle';
import { problemMessage } from '../../shared/problem';
import { TracedFactory, factoryColour, machineOwners } from '../area/factory-outlines';
import { ArrangementDeletion } from './arrangement-deletion';
import { BeltFloor3dDialog, BeltFloor3dDialogData } from './belt-floor-3d-dialog';
import { CatalogNames } from '../../shared/catalog-names';
import { MatDialog } from '@angular/material/dialog';
import { footprintShapes, toMapPoint } from '../map/map-model';
import {
  areaBetween,
  beltEnds,
  beltLine,
  beltTooltip,
  factoryTooltip,
  joinedWays,
  ceilingHeights,
  ceilingMetres,
  foundations,
  mapBounds,
  metres,
  lineJoints,
  placedMachines,
  movedMetres,
  relatedFactories,
  saving,
  jointColours,
  spotMark,
  wayMark,
  standsOut,
  toggled,
  SolverTime,
  solverTime,
  solverTimeOf,
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
    MatButtonToggleModule,
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
  /** The ceiling of the belt floor below the factories, in foundation heights of 4 metres (BR-O2). */
  protected readonly ceiling = signal(8);
  /** Whether belts are routed as supply lines, topped up by priority mergers (BR-O2). */
  protected readonly supplyLines = signal(false);
  /** How long the solver looks for a better layout: a minute when nothing else is picked. */
  protected readonly solverTime = signal<SolverTime>('balanced');
  /** The area drawn to arrange, in the game's centimetres. */
  protected readonly area = signal<WorldRectangle | null>(null);
  /** Where no factory may stand inside the area, in the game's centimetres: the plots they touch stay free. */
  protected readonly exclusions = signal<WorldRectangle[]>([]);
  /** What a drag on the map draws now: the area, an exclusion, or nothing (the map pans). */
  protected readonly drawing = signal<'area' | 'exclusion' | null>(null);

  protected draw(kind: 'area' | 'exclusion'): void {
    this.drawing.update((drawing) => (drawing === kind ? null : kind));
  }

  /** Back to the start: no area, no exclusions, no run shown; the settings stay. */
  protected reset(): void {
    this.drawing.set(null);
    this.selected.set(null);
    this.area.set(null);
    this.exclusions.set([]);
  }

  /** The shown run's belt floor in 3D, over the page. */
  protected open3d(): void {
    const arrangement = this.arrangement.value();
    if (!arrangement) return;
    const data: BeltFloor3dDialogData = {
      arrangement,
      colourOf: this.colourOf(),
      machines: this.machines.value() ?? null,
      focused: this.focused,
    };
    this.dialog.open(BeltFloor3dDialog, { data, width: '95vw', maxWidth: '95vw', height: '90vh' });
  }

  /** Deletes a run after asking; the list shows the newest left. */
  protected async delete(run: ArrangementSummary): Promise<void> {
    const requested = new Date(run.requestedAt).toLocaleString();
    if (await this.deletion.confirmAndDelete({ id: run.id, requested })) this.runs.reload();
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

  /**
   * Whether the shown run is being loaded or computed: the whole time it's computed, not only while it's asked for again
   * every two seconds, so the progress bar doesn't blink (2026-11-19). A resource keeps its value while it reloads.
   */
  protected readonly busy = computed(
    () => this.arrangement.isLoading() || this.arrangement.value()?.summary.status === 'pending',
  );

  /**
   * The shown run's machines as they stand now, to draw moved with their factories as the map draws machines: asked
   * once the run is ready, since they're read from its snapshot.
   */
  private readonly machines = resource({
    params: () => {
      const summary = this.arrangement.value()?.summary;
      return summary?.status === 'ready' ? { id: summary.id } : undefined;
    },
    loader: ({ params }) => this.api.invoke(getArrangementMachines, params),
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
  /** Whether to show the belts routed on the floor below the factories. */
  protected readonly showBelts = signal(true);
  private readonly names = inject(CatalogNames);
  private readonly dialog = inject(MatDialog);
  private readonly deletion = inject(ArrangementDeletion);
  /** A factory's colour, as the map draws it. */
  protected readonly colourOf = computed(() => {
    const traces = this.factories.value() ?? [];
    return (factoryId: string) => factoryColour(traces, factoryId);
  });
  /** The factories picked in the side bar or on the map: they, their belts and the factories linked to them stand out. */
  protected readonly focused = signal<ReadonlySet<string>>(new Set());

  protected toggleFocus(factoryId: string): void {
    this.focused.update((focused) => toggled(focused, factoryId));
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
  private readonly beltLayer = L.layerGroup();
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
      for (const layer of [this.worldLayer, this.runLayer, this.beltLayer, this.areaLayer])
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
      this.ceiling.set(ceilingHeights(run.ceilingMetres));
      this.supplyLines.set(run.supplyLines);
      this.solverTime.set(solverTimeOf(run.seconds));
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
          ceilingMetres: ceilingMetres(this.ceiling()),
          supplyLines: this.supplyLines(),
          // The server's: 2 for keeping together, 0.5 for nodes and stations (owner, 2026-11-19).
          together: null,
          spotWeight: null,
          seconds: solverTime(this.solverTime()),
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
        : 'No layout found: nothing placed';
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
   * The shown run: its area and where each factory would go (filled, in its colour, with its machines). Where they stand
   * now (the rectangle around their machines) shows when asked. With factories picked, those that don't feed one fade.
   */
  private drawRun(): void {
    const arrangement = this.arrangement.value();
    const traces = this.factories.value() ?? [];
    const points = this.machinePoints();
    const [showNow, focused] = [this.showNow(), this.focused()];
    const machines = this.machines.value();
    const footprints = new Map(
      (machines?.footprints ?? []).map((footprint) => [footprint.id, footprint]),
    );
    const owned = new Map<string, ArrangedMachineResponse[]>();
    for (const machine of machines?.machines ?? [])
      owned.set(machine.factoryId, [...(owned.get(machine.factoryId) ?? []), machine]);
    if (!this.map) return;
    this.runLayer.clearLayers();
    this.beltLayer.clearLayers();
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

    if (this.showBelts()) this.drawBelts(arrangement, traces, focused);

    // With factories picked, the others fade, unless they feed one picked.
    const related = relatedFactories(focused, arrangement.links);
    for (const factory of arrangement.factories) {
      if (!factory.proposed) continue;
      const colour = factoryColour(traces, factory.factoryId) ?? '#90a4ae';
      const name = factory.name ?? '(deleted)';
      const proposed = L.latLngBounds(mapBounds(factory.proposed));
      const metres = movedMetres(factory.now, factory.proposed);
      const standing = owned.get(factory.factoryId) ?? [];
      const fade = related !== null && !related.has(factory.factoryId) ? 0.25 : 1;
      L.rectangle(proposed, {
        color: colour,
        weight: focused.has(factory.factoryId) ? 4 : 2,
        opacity: fade,
        fillColor: colour,
        fillOpacity: (standing.length > 0 ? 0.15 : 0.45) * fade,
      })
        .bindTooltip(factoryTooltip(name, metres))
        .on('click', () => this.toggleFocus(factory.factoryId))
        .addTo(this.runLayer);

      // Its machines in the middle of the plot, as the map draws them, facing the same way (decided 2026-11-18).
      for (const moved of placedMachines(standing, footprints, factory.proposed)) {
        if (!footprints.has(moved.className)) continue;
        L.polygon(
          footprintShapes(moved, footprints).map((ring) => [ring]),
          {
            fillRule: 'nonzero',
            color: colour,
            weight: 1,
            opacity: fade,
            fillColor: colour,
            fillOpacity: 0.85 * fade,
            interactive: false,
          },
        ).addTo(this.runLayer);
      }

      // Where it stands now, when asked: the rectangle around its machines, as built.
      const machines = points.get(factory.factoryId) ?? [];
      if (machines.length === 0 || !showNow) continue;
      L.rectangle(L.latLngBounds(machines).pad(0.05), {
        color: colour,
        weight: 1,
        dashArray: '4 4',
        fill: false,
        opacity: 0.9 * fade,
        interactive: false,
      }).addTo(this.runLayer);
    }
  }

  /**
   * The belts routed on the floor below (BR-O2): a thin line each, in the colour of the factory it serves, with a dot at
   * each lift; at a miner a red circle, at an extractor one in the colour of its fluid (asked 2026-11-19); at a station
   * or drone port the dot is dark. Where a belt leaves the floor for one outside
   * the area, an amber ring. Belts on one square are stacked, so their lines lie on each other. With factories picked,
   * their belts stand out.
   */
  private drawBelts(
    arrangement: ArrangementResponse,
    traces: readonly TracedFactory[],
    focused: ReadonlySet<string>,
  ): void {
    const factories = new Set(arrangement.factories.map((factory) => factory.factoryId));
    // Spots whose existing belt comes into the area: the yellow circle stands where it comes in, drawn with the way.
    const comingIn = new Set(arrangement.ways.map((way) => way.spotId));
    const spotNames = {
      item: (item: string) => this.names.item(item),
      building: (name: string) => this.names.building(name),
      extractorForm: (name: string) => this.names.extractorForm(name),
      fluidColour: (item: string) => this.names.fluidColour(item),
    };
    for (const belt of arrangement.belts) {
      const picked = focused.has(belt.from) || focused.has(belt.to);
      const dimmed = !standsOut(focused, [belt.from, belt.to]);
      const ends = beltEnds(belt, factories);
      const colour = factoryColour(traces, ends.factory) ?? '#e0e0e0';
      const line = beltLine(belt.corners);
      L.polyline(line, {
        color: colour,
        weight: picked ? 3 : 1.5,
        opacity: dimmed ? 0.25 : 0.9,
        // A pipe dashed, a belt solid.
        dashArray: belt.pipe ? '6 4' : undefined,
        interactive: false,
      }).addTo(this.beltLayer);
      // What it carries, on a wider line that can't be seen, so a thin belt is easy to point at.
      L.polyline(line, { opacity: 0, weight: 10 })
        .bindTooltip(() => beltTooltip(belt, (item) => this.names.item(item)), {
          sticky: true,
        })
        .addTo(this.beltLayer);
      const spot =
        ends.spot === 'start' ? line[0] : ends.spot === 'end' ? line[line.length - 1] : null;
      // Its lifts: none where it joins another belt, at a splitter or merger drawn below.
      const lifts = [
        ...(belt.startsOnBelt ? [] : [line[0]]),
        ...(belt.endsOnBelt ? [] : [line[line.length - 1]]),
      ];
      const mark = spotMark(belt, factories, spotNames);
      for (const lift of lifts) {
        const atSpot = lift === spot;
        if (atSpot && mark)
          L.circleMarker(lift, {
            radius: picked ? 6 : 5,
            weight: 1.5,
            color: '#fafafa',
            opacity: dimmed ? 0.25 : 1,
            fillColor: mark.colour,
            fillOpacity: dimmed ? 0.25 : 1,
          })
            .bindTooltip(mark.tooltip)
            .addTo(this.beltLayer);
        else
          L.circleMarker(lift, {
            radius: picked ? 3 : 2,
            weight: 1,
            color: '#212121',
            fillColor: atSpot ? '#37474f' : colour,
            fillOpacity: dimmed ? 0.25 : 1,
            interactive: false,
          }).addTo(this.beltLayer);
        if (
          atSpot &&
          belt.leavesFloor &&
          !comingIn.has(ends.spot === 'start' ? belt.from : belt.to)
        )
          L.circleMarker(lift, {
            radius: 6,
            weight: 2,
            color: '#ffb300',
            fill: false,
            opacity: dimmed ? 0.25 : 1,
            interactive: false,
          }).addTo(this.beltLayer);
      }
    }

    // Outside the area, the existing belts the routed ones join where they come into the area (decided 2026-11-18):
    // dotted, in the colour of the factory they serve, from the building to a yellow circle on the area's edge. The
    // routed belts run on across the strip of the area off the floor to it, at right angles. At a miner or extractor,
    // its circle, as inside the area (asked 2026-11-19).
    for (const spot of arrangement.ways)
      for (const way of joinedWays([spot], arrangement.belts, factories, spotNames)) {
        const colour = factoryColour(traces, way.factory) ?? '#e0e0e0';
        const dimmed = !standsOut(focused, [way.factory]);
        const entry = toMapPoint({ ...way.entry, z: 0 });
        const line = way.points.map((point) => toMapPoint({ ...point, z: 0 }));
        L.polyline(line, {
          color: colour,
          weight: 1.5,
          opacity: dimmed ? 0.25 : 0.8,
          dashArray: '2 4',
          interactive: false,
        }).addTo(this.beltLayer);
        L.polyline(line, { opacity: 0, weight: 10 })
          .bindTooltip(way.tooltip, { sticky: true })
          .addTo(this.beltLayer);
        for (const link of way.links)
          L.polyline(
            link.map((point) => toMapPoint({ ...point, z: 0 })),
            {
              color: colour,
              weight: 1.5,
              opacity: dimmed ? 0.25 : 0.9,
              interactive: false,
            },
          ).addTo(this.beltLayer);
        L.circleMarker(entry, {
          radius: 6,
          weight: 2,
          color: '#ffb300',
          fill: false,
          opacity: dimmed ? 0.25 : 1,
          interactive: false,
        }).addTo(this.beltLayer);
        const mark = wayMark(spot, arrangement.belts, spotNames);
        if (mark)
          L.circleMarker(toMapPoint({ ...mark.at, z: 0 }), {
            radius: 5,
            weight: 1.5,
            color: '#fafafa',
            opacity: dimmed ? 0.25 : 1,
            fillColor: mark.colour,
            fillOpacity: dimmed ? 0.25 : 1,
          })
            .bindTooltip(mark.tooltip)
            .addTo(this.beltLayer);
      }

    // Where supply lines are topped up (decided 2026-11-18): a priority merger, an orange square, where a top-up belt
    // ends; a smart splitter, a blue diamond, where its overflow runs on; a splitter, a white diamond, where a line runs
    // on past a factory (decided 2026-11-19).
    for (const joint of lineJoints(arrangement.belts, (item) => this.names.item(item))) {
      const [lat, lng] = toMapPoint({ ...joint.at, z: 0 });
      const [r, d] = [2.5, 3.5];
      const shape: [number, number][] =
        joint.kind === 'merger'
          ? [
              [lat - r, lng - r],
              [lat - r, lng + r],
              [lat + r, lng + r],
              [lat + r, lng - r],
            ]
          : [
              [lat - d, lng],
              [lat, lng + d],
              [lat + d, lng],
              [lat, lng - d],
            ];
      L.polygon(shape, {
        color: '#111111',
        weight: 1,
        fillColor: jointColours[joint.kind],
        fillOpacity: 1,
      })
        .bindTooltip(joint.tooltip)
        .addTo(this.beltLayer);
    }
  }
}
