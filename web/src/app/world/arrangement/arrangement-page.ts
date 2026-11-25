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
import { previewFactory } from '../../api/fn/world/preview-factory';
import { MapConveyor } from '../../api/models/map-conveyor';
import { AreaSelection } from '../map/area-selection';
import { buildingShape } from '../map/building-shape';
import { holdToExtent } from '../map/hold-to-extent';
import { MapArt } from '../map/map-art';
import { removeMap } from '../map/remove-map';
import { SidebarTab } from '../../sidebar/sidebar-tab';
import { RunPicker } from './run-picker';
import {
  dragColours,
  footprintShapes,
  mapArtTiles,
  mapExtent,
  sampleCurve,
  toMapPoint,
  escapeHtml,
} from '../map/map-model';
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
  crossingMarks,
  placedMachines,
  movedMetres,
  relatedFactories,
  jointColours,
  spotMark,
  wayMark,
  standsOut,
  pickedAlone,
  SolverTime,
  solverTime,
  solverTimeOf,
  areaFromQuery,
  PickedConnections,
  relationsOf,
  lineGroups,
  lineItems,
  linesToLabel,
  AreaFactory,
  arrangedFactories,
  factoriesInArea,
  beltsToLabel,
  halfway,
} from './arrangement-model';
import { loaded } from '../../shared/loaded';

/** What stays where it is and takes its plots: the stations' classes, as the arrangement counts them. */
/** How far out the map zooms without a map picture to hold it to. */
const LeastZoom = -4;

/** The area to arrange: blue, as the rectangle drawn with ctrl on the world map (asked 2026-11-24). */
const areaStyle: L.PathOptions = { color: dragColours.arrangement, weight: 2, fillOpacity: 0.05 };
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
    RunPicker,
    SidebarTab,
  ],
  templateUrl: './arrangement-page.html',
  styleUrl: './arrangement-page.scss',
})
export class ArrangementPage {
  /** The snapshot, from the route. */
  readonly id = input.required<string>();
  /** ?run=…: the run shown; the newest when not given, and none when an area is given. */
  readonly run = input<string>();
  /** ?x0=…&y0=…&x1=…&y1=…: an area to arrange, dragged with ctrl held on the world map (asked 2026-11-23). */
  readonly x0 = input<string>();
  readonly y0 = input<string>();
  readonly x1 = input<string>();
  readonly y1 = input<string>();
  private readonly dragged = computed(() =>
    areaFromQuery(this.x0(), this.y0(), this.x1(), this.y1()),
  );

  private readonly api = inject(Api);
  protected readonly problem = problemMessage;
  protected readonly loaded = loaded;

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
  /** The area drawn to arrange, in the game's centimetres; at first, the one dragged on the world map, if any. */
  protected readonly area = linkedSignal<WorldRectangle | null>(() => this.dragged());
  /** Where no factory may stand inside the area, in the game's centimetres: the plots they touch stay free. */
  protected readonly exclusions = signal<WorldRectangle[]>([]);
  /**
   * What a drag on the map draws now: the area, with shift held, or an exclusion inside it, with ctrl held (asked
   * 2026-11-23; they were buttons); or nothing, and the map pans.
   */
  protected readonly drawing = signal<'area' | 'exclusion' | null>(null);
  /**
   * Selecting on a touch screen, with no keys to hold (asked 2026-11-24, ADR-0033): a drag draws a rectangle, which the
   * page then offers as the area or, inside one, as a part kept out. As small as with a key held is a tap.
   */
  protected readonly selection = new AreaSelection(({ from, to }) => {
    const area = areaBetween([from.lat, from.lng], [to.lat, to.lng]);
    return area.x1 - area.x0 >= 1000 && area.y1 - area.y0 >= 1000;
  }, areaStyle);

  /**
   * The rectangle selected becomes the area, or a part kept out. Selecting goes on, so the next part can be drawn at
   * once; the select button ends it.
   */
  protected useSelected(as: 'area' | 'exclusion'): void {
    const drawn = this.selection.drawn();
    if (!drawn) return;
    const area = areaBetween([drawn.from.lat, drawn.from.lng], [drawn.to.lat, drawn.to.lng]);
    if (as === 'area') this.area.set(area);
    else this.exclusions.update((exclusions) => [...exclusions, area]);
    this.selection.clear();
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
    const arrangement = loaded(this.arrangement);
    if (!arrangement) return;
    const data: BeltFloor3dDialogData = {
      arrangement,
      colourOf: this.colourOf(),
      machines: loaded(this.machines) ?? null,
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
  protected readonly world = resource({
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
      const world = loaded(this.snapshot)?.sessionName;
      return world ? { world } : undefined;
    },
    loader: ({ params }) => this.api.invoke(getArrangements, params),
  });

  protected readonly selected = linkedSignal(
    // An area dragged on the world map is a new run to make: no earlier run's area over it.
    () => this.run() ?? (this.dragged() ? null : (loaded(this.runs)?.[0]?.id ?? null)),
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
    () => this.arrangement.isLoading() || loaded(this.arrangement)?.summary.status === 'pending',
  );

  /**
   * The shown run's machines as they stand now, to draw moved with their factories as the map draws machines: asked
   * once the run is ready, since they're read from its snapshot.
   */
  private readonly machines = resource({
    params: () => {
      const summary = loaded(this.arrangement)?.summary;
      return summary?.status === 'ready' ? { id: summary.id } : undefined;
    },
    loader: ({ params }) => this.api.invoke(getArrangementMachines, params),
  });

  /** The factories of the shown run, the farthest move first. */
  /**
   * Before a run, what's connected to each factory picked, and the logistics it owns, by factory, in the order picked:
   * through its lines to their ends and the machines of the factories they reach (asked 2026-11-23). None with a run shown.
   */
  private readonly connectedParts = resource({
    params: () => {
      const running = (loaded(this.arrangement)?.factories.length ?? 0) > 0;
      const picked = [...this.focused()];
      if (running || picked.length === 0) return undefined;
      const traces = loaded(this.factories) ?? [];
      const factories = picked
        .map((factoryId) => ({
          factoryId,
          machines:
            traces.find((trace) => trace.factoryId === factoryId)?.machines.map((m) => m.id) ?? [],
        }))
        .filter((factory) => factory.machines.length > 0);
      return factories.length > 0 ? { id: this.id(), factories } : undefined;
    },
    loader: async ({ params }) =>
      new Map<string, PickedConnections>(
        await Promise.all(
          params.factories.map(async (factory) => {
            const preview = await this.api.invoke(previewFactory, {
              id: params.id,
              body: { machines: factory.machines },
            });
            return [
              factory.factoryId,
              {
                supplies: new Set(preview.supplies),
                handovers: new Map(
                  preview.inputs
                    .filter((input) => input.outside)
                    .map((input) => [input.outside!.id, input.items] as [string, string[]]),
                ),
              },
            ] as [string, PickedConnections];
          }),
        ),
      ),
  });

  private readonly names = inject(CatalogNames);
  private readonly dialog = inject(MatDialog);
  private readonly deletion = inject(ArrangementDeletion);
  /** A factory's colour, as the map draws it. */
  protected readonly colourOf = computed(() => {
    const traces = loaded(this.factories) ?? [];
    return (factoryId: string) => factoryColour(traces, factoryId);
  });
  /** The factories picked in the side bar or on the map: they, their belts and the factories linked to them stand out. */
  protected readonly focused = signal<ReadonlySet<string>>(new Set());

  /** A click on a factory, on the map or in the Factories tab: it alone is picked, or let go. */
  protected toggleFocus(factoryId: string): void {
    this.focused.update((focused) => pickedAlone(focused, factoryId));
  }

  /**
   * The factories in the area (asked 2026-11-25): the Factories tab lists them and the map names them, so what lights up
   * can be told. A run's, on the plots it proposes; before one, those inside the area drawn, where they stand.
   */
  protected readonly areaFactories = computed<AreaFactory[]>(() => {
    const arrangement = loaded(this.arrangement);
    if (arrangement && this.selected()) return arrangedFactories(arrangement.factories);
    const world = loaded(this.world);
    const owners = machineOwners(loaded(this.factories) ?? []);
    return factoriesInArea(
      [...(world?.machines ?? []), ...(world?.consumers ?? [])],
      owners,
      this.area(),
    );
  });

  private readonly container = viewChild.required<ElementRef<HTMLElement>>('map');
  private map?: L.Map;
  /** The game's map picture under everything, as on the world map (asked 2026-11-23). */
  private readonly mapArt = inject(MapArt).art;
  private readonly artLayer = L.layerGroup();
  private readonly worldLayer = L.layerGroup();
  private readonly runLayer = L.layerGroup();
  private readonly beltLayer = L.layerGroup();
  private readonly areaLayer = L.layerGroup();
  private readonly nameLayer = L.layerGroup();
  /** The factories' names on the map, the picked first. */
  private nameLabels: L.Tooltip[] = [];
  private fitted = false;

  constructor() {
    afterNextRender(() => {
      this.map = L.map(this.container().nativeElement, {
        crs: L.CRS.Simple,
        preferCanvas: true,
        minZoom: LeastZoom,
        // Shift and drag draws the area to arrange (listenForArea), not a box to zoom to.
        boxZoom: false,
        maxZoom: 3,
        zoomSnap: 0.25,
        attributionControl: false,
      });
      // The map picture first, so everything else lies over it.
      for (const layer of [
        this.artLayer,
        this.worldLayer,
        this.runLayer,
        this.beltLayer,
        this.areaLayer,
        this.nameLayer,
      ])
        layer.addTo(this.map);
      this.map.setView([0, 0], -3);
      this.listenForArea(this.map);
      this.selection.attach(this.map);
      this.map.on('resize', () => this.holdToMapArt());
      this.map.on('zoomend', () => this.declutterNames());
      this.drawMapArt();
      this.drawWorld();
      this.drawRun();
      this.drawArea();
      this.drawNames();
      this.fitToArea();
    });
    // Leaflet follows the window's size only: the sidebar's tabs beside the map change its size too, as on the world map.
    const resized = new ResizeObserver(() => this.map?.invalidateSize());
    afterNextRender(() => resized.observe(this.container().nativeElement));
    inject(DestroyRef).onDestroy(() => {
      resized.disconnect();
      if (this.map) removeMap(this.map);
    });
    effect(() => this.fitToArea());
    effect(() => this.drawMapArt());
    effect(() => this.drawWorld());
    effect(() => this.drawRun());
    effect(() => this.drawArea());
    effect(() => this.drawNames());
    // Another run: nothing picked, and its area, exclusions and settings are where a new run starts.
    effect(() => {
      const id = this.selected();
      this.focused.set(new Set());
      const run = untracked(() => loaded(this.runs))?.find((candidate) => candidate.id === id);
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

    // While a run is computed, ask again every two seconds.
    effect((onCleanup) => {
      const pending = (loaded(this.runs) ?? []).some((run) => run.status === 'pending');
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
    // The run is asked for: done selecting, so the map pans again when it's shown (asked 2026-11-25).
    if (this.selection.selecting()) this.selection.toggle();
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

  /**
   * What the lines between a factory picked and its suppliers carry (asked 2026-11-23): once per line, the pieces that
   * meet end to end, the icons of its items in a pill at its middle, their names on hover. What's on its belts at save
   * time, and what its handover into the factory brings, for a pipe or an empty belt.
   */
  private drawLineItems(lit: MapConveyor[], picked: ReadonlyMap<string, PickedConnections>): void {
    const handovers = new Map<string, readonly string[]>();
    for (const { handovers: across } of picked.values())
      for (const [id, items] of across) handovers.set(id, items);
    const byId = new Map(lit.map((conveyor) => [conveyor.id, conveyor]));
    const contents = new Map(lit.map((conveyor) => [conveyor.id, conveyor.contents]));
    const pieces = lit.map((conveyor) => ({
      id: conveyor.id,
      from: conveyor.curve[0].location,
      to: conveyor.curve[conveyor.curve.length - 1].location,
    }));
    // Each piece's length along its curve, in centimetres.
    const lengths = new Map(
      lit.map((conveyor) => {
        const points = sampleCurve(conveyor.curve);
        let length = 0;
        for (let i = 1; i < points.length; i++)
          length +=
            Math.hypot(points[i][0] - points[i - 1][0], points[i][1] - points[i - 1][1]) * 100;
        return [conveyor.id, length] as [string, number];
      }),
    );
    for (const line of linesToLabel(lineGroups(pieces), (id) => lengths.get(id) ?? 0)) {
      const items = [...lineItems(line, contents, handovers)];
      if (items.length === 0) continue;
      // Halfway along the line: the middle of its middle piece.
      const middle = sampleCurve(byId.get(line[Math.floor(line.length / 2)])!.curve);
      this.itemPill(items, middle[Math.floor(middle.length / 2)]).addTo(this.worldLayer);
    }
  }

  /** The icons of what a line carries in a pill at a point on it, their names on hover. */
  private itemPill(items: readonly string[], at: L.LatLngExpression): L.Marker {
    const html = items
      .map((item) => {
        const icon = this.names.icon(item);
        return icon
          ? `<img src="${escapeHtml(icon)}" alt="" width="18" height="18">`
          : `<span>${escapeHtml(this.names.item(item))}</span>`;
      })
      .join('');
    return L.marker(at, {
      icon: L.divIcon({
        html: `<div class="line-items">${html}</div>`,
        className: '',
        iconSize: [0, 0],
      }),
    }).bindTooltip(items.map((item) => this.names.item(item)).join(', '));
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
   * Held to the map picture as the world map is (asked 2026-11-23): out no further than the whole picture, no further
   * than its edges, which buildings past them take along.
   */
  private holdToMapArt(): void {
    if (!this.map) return;
    const extent = mapExtent(loaded(this.mapArt) ?? null, loaded(this.world) ?? null);
    holdToExtent(this.map, extent, LeastZoom);
  }

  /** While drawing, a drag draws the area to arrange, or an exclusion inside it. */
  private listenForArea(map: L.Map): void {
    let start: L.LatLng | null = null;
    let rectangle: L.Rectangle | null = null;
    // What the drag draws, kept from its start: letting go ends drawing before Leaflet's mouseup comes.
    let draws: 'area' | 'exclusion' | null = null;
    // In the capture phase, so the press is heard before Leaflet's panning starts, and panning can be switched off.
    map.getContainer().addEventListener(
      'pointerdown',
      (event) => {
        // A part kept out lies in the area: without one, ctrl does nothing.
        const kind = event.shiftKey
          ? 'area'
          : (event.ctrlKey || event.metaKey) && this.area()
            ? 'exclusion'
            : null;
        if (!kind || event.button !== 0) return;
        this.drawing.set(kind);
        map.dragging.disable();
      },
      { capture: true },
    );
    // However the press ends, panning comes back.
    map.getContainer().addEventListener('pointerup', () => {
      if (!this.drawing()) return;
      this.drawing.set(null);
      map.dragging.enable();
    });
    map.on('mousedown', (event: L.LeafletMouseEvent) => {
      const drawing = this.drawing();
      if (!drawing) return;
      draws = drawing;
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
      const drew = draws;
      rectangle?.remove();
      [start, rectangle, draws] = [null, null, null];
      this.drawing.set(null);
      map.dragging.enable();
      // A click without a drag draws nothing.
      if (area.x1 - area.x0 < 1000 || area.y1 - area.y0 < 1000) return;
      if (drew === 'area') this.area.set(area);
      else this.exclusions.update((exclusions) => [...exclusions, area]);
    });
  }

  /**
   * The world as it stands, before a run: each factory's machines in its colour, and the miners and extractors,
   * stations and sinks, each the ground it covers; no belts. With factories picked in the Factories tab (asked
   * 2026-11-23), each factory is a whole: the picked ones and every factory their lines reach light up, all their
   * machines; the belts and pipes between them are drawn as built, with the splitters on them and the miners, stations
   * and sinks at their ends, in the colour of the factory picked; the rest fades. A factory's own belts aren't drawn.
   * With a run shown, the run is drawn instead.
   */
  private drawWorld(): void {
    const world = loaded(this.world);
    const traces = loaded(this.factories) ?? [];
    const running = (loaded(this.arrangement)?.factories.length ?? 0) > 0;
    if (!this.map || !world) return;
    this.worldLayer.clearLayers();
    if (running) return;
    const owners = machineOwners(traces);
    const picking = this.focused().size > 0;
    const picked = loaded(this.connectedParts) ?? new Map<string, PickedConnections>();
    const { factories, parts } = relationsOf(picked, (id) => owners.get(id)?.[0]?.factoryId);
    const colourOf = (factoryId: string) => factoryColour(traces, factoryId) ?? '#90a4ae';
    const footprints = new Map(world.footprints.map((footprint) => [footprint.id, footprint]));
    const named = (className: string) => this.names.building(className);

    // The belts and pipes between the factories picked and what supplies them, as built; none else.
    const lit = world.conveyors.filter(
      (conveyor) => parts.has(conveyor.id) && conveyor.curve.length >= 2,
    );
    for (const conveyor of lit) {
      L.polyline(sampleCurve(conveyor.curve), {
        color: colourOf(parts.get(conveyor.id)!),
        weight: 3,
        opacity: 0.9,
      })
        .bindTooltip(named(conveyor.className))
        .addTo(this.worldLayer);
    }
    this.drawLineItems(lit, picked);

    // Splitters, mergers and containers only on those lines; miners and extractors, stations and sinks always.
    const others = [
      ...world.providers.map((building) => ({ building, colour: '#42a5f5' })),
      ...world.logistics
        .filter((building) => parts.has(building.id))
        .map((building) => ({ building, colour: '#546e7a' })),
      ...world.consumers
        .filter((consumer) => !owners.has(consumer.id))
        .map((building) => ({ building, colour: '#ab47bc' })),
    ];
    for (const { building, colour: own } of others) {
      const station = stationClasses.has(building.className);
      const between = parts.get(building.id);
      const fades = picking && !between;
      const colour = between ? colourOf(between) : station ? '#455a64' : own;
      buildingShape(
        building,
        footprints,
        {
          weight: between ? 2 : 1,
          color: between ? colour : station ? '#263238' : own,
          opacity: fades ? 0.2 : 1,
          fillColor: colour,
          fillOpacity: fades ? 0.1 : station ? 0.9 : 0.6,
        },
        station ? 3 : 2,
      )
        .bindTooltip(station ? `Station · ${named(building.className)}` : named(building.className))
        .addTo(this.worldLayer);
    }

    // The factories' machines last, on top: a factory lit up or faded as a whole. While picking, the factories lit up
    // take the colour of the factory picked they supply (a factory's own colour can be any of eight, often shared:
    // 2026-11-23, two suppliers lit in their own salmon and mint couldn't be told from the rest), and the rest are grey,
    // so stacked floors don't add up to a colour; with none picked, each its own.
    const points: L.LatLngExpression[] = [];
    for (const machine of [
      ...world.machines,
      ...world.consumers.filter((consumer) => owners.has(consumer.id)),
    ]) {
      const owner = owners.get(machine.id)?.[0];
      points.push(toMapPoint(machine.position));
      const supplied = owner ? factories.get(owner.factoryId) : undefined;
      const fades = picking && !supplied;
      const colour = supplied
        ? colourOf(supplied)
        : fades
          ? '#90a4ae'
          : (owner?.colour ?? '#78909c');
      buildingShape(
        machine,
        footprints,
        {
          color: colour,
          weight: 1,
          opacity: fades ? 0.15 : 0.9,
          fillColor: colour,
          fillOpacity: fades ? 0.1 : owner ? 0.75 : 0.4,
        },
        2,
      )
        .bindTooltip(owner?.name ?? 'No factory')
        // A click on a factory picks it alone (asked 2026-11-23): what supplies it lights up.
        .on('click', (event: L.LeafletMouseEvent) => {
          if (!owner || this.drawing() || this.selection.selecting()) return;
          L.DomEvent.stopPropagation(event);
          this.toggleFocus(owner.factoryId);
        })
        .addTo(this.worldLayer);
    }
    // The whole world, unless there's an area to show (fitToArea).
    if (!this.fitted && !untracked(() => this.area()) && points.length > 0) {
      this.map.fitBounds(L.latLngBounds(points), { padding: [24, 24] });
      this.fitted = true;
    }
  }

  /**
   * Opens on the area to arrange (asked 2026-11-25): one handed over by the world map, or the shown run's once the runs
   * come. Once only: an area drawn here afterwards is where the player already looks.
   */
  private fitToArea(): void {
    const area = this.area();
    if (!this.map || !area || this.fitted) return;
    this.map.fitBounds(L.latLngBounds(mapBounds(area)), { padding: [24, 24] });
    this.fitted = true;
  }

  /**
   * The factories that stand out: the one picked and those lit up with it, what feeds it (a run's links, or before one
   * what supplies it as built). None while nothing is picked.
   */
  private readonly highlighted = computed<ReadonlySet<string>>(() => {
    const focused = this.focused();
    if (focused.size === 0) return focused;
    const arrangement = loaded(this.arrangement);
    if (arrangement && this.selected())
      return relatedFactories(focused, arrangement.links) ?? focused;
    const owners = machineOwners(loaded(this.factories) ?? []);
    const picked = loaded(this.connectedParts) ?? new Map<string, PickedConnections>();
    const { factories } = relationsOf(picked, (id) => owners.get(id)?.[0]?.factoryId);
    return new Set([...focused, ...factories.keys()]);
  });

  /**
   * The highlighted factories named at their middle, not only when pointed at (asked 2026-11-25; first every factory in
   * the area, which piled up on a phone). A tap or click on a name picks the factory, as on its shapes; the picked one's
   * name stands out.
   */
  private drawNames(): void {
    const highlighted = this.highlighted();
    const factories = this.areaFactories().filter((factory) => highlighted.has(factory.factoryId));
    const focused = this.focused();
    if (!this.map) return;
    this.nameLayer.clearLayers();
    this.nameLabels = [];
    // The picked first: its name always shows (declutterNames).
    const ordered = [
      ...factories.filter((factory) => focused.has(factory.factoryId)),
      ...factories.filter((factory) => !focused.has(factory.factoryId)),
    ];
    for (const factory of ordered) {
      const name = L.tooltip({
        permanent: true,
        direction: 'center',
        interactive: true,
        className: focused.has(factory.factoryId) ? 'factory-label picked' : 'factory-label',
      })
        .setLatLng(toMapPoint({ ...factory.middle, z: 0 }))
        .setContent(escapeHtml(factory.name));
      name.on('click', () => this.toggleFocus(factory.factoryId));
      name.addTo(this.nameLayer);
      this.nameLabels.push(name);
    }
    this.declutterNames();
  }

  /**
   * Hides a name that would lie over one shown before it, until a closer zoom leaves room (asked 2026-11-25: on a
   * phone the names of factories close together piled up). The picked factory's comes first, so it always shows.
   */
  private declutterNames(): void {
    const shown: DOMRect[] = [];
    for (const name of this.nameLabels) {
      const element = name.getElement();
      if (!element) continue;
      element.style.visibility = '';
      const box = element.getBoundingClientRect();
      const overlaps = shown.some(
        (other) =>
          box.left < other.right &&
          other.left < box.right &&
          box.top < other.bottom &&
          other.top < box.bottom,
      );
      if (overlaps) element.style.visibility = 'hidden';
      else shown.push(box);
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
   * The shown run: its area and where each factory would go (filled, in its colour, with its machines), and the belts
   * and pipes on the floor below, always (asked 2026-11-23). With factories picked, those that don't feed one fade.
   */
  private drawRun(): void {
    const arrangement = loaded(this.arrangement);
    const traces = loaded(this.factories) ?? [];
    const focused = this.focused();
    const machines = loaded(this.machines);
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
        color: dragColours.arrangement,
        weight: 1,
        dashArray: '6 6',
        fill: false,
        interactive: false,
      }).addTo(this.runLayer);
    }

    // Always (asked 2026-11-23): the belts and pipes on the floor below are part of the proposal.
    this.drawBelts(arrangement, traces, focused);
    // With a factory picked, what its long belts and pipes carry, as the lines before a run (asked 2026-11-25).
    for (const belt of beltsToLabel(arrangement.belts, focused))
      this.itemPill([belt.item!], toMapPoint({ ...halfway(belt.corners), z: 0 })).addTo(
        this.beltLayer,
      );

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
    }
  }

  /**
   * The belts routed on the floor below (BR-O2): a thin line each, in the colour of the factory it serves, with a dot at
   * each lift; at a miner a red circle, at an extractor one in the colour of its fluid (asked 2026-11-19); at a station
   * or drone port the dot is dark. Where a belt leaves the floor for one outside
   * the area, an amber ring. Belts on one square are stacked, so their lines lie on each other. With factories picked,
   * their belts stand out. Where a belt crosses another as a last resort, an amber × (step 102).
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

    // Where a belt crosses another as a last resort (step 102, decided 2026-11-25): an amber ×, to check in the game.
    for (const crossing of crossingMarks(arrangement.belts))
      L.marker(toMapPoint({ ...crossing.at, z: 0 }), {
        icon: L.divIcon({ html: '×', className: 'crossing-mark', iconSize: [16, 16] }),
      })
        .bindTooltip(crossing.tooltip)
        .addTo(this.beltLayer);

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
