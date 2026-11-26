import { NgTemplateOutlet } from '@angular/common';
import {
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  output,
  resource,
  signal,
  untracked,
  viewChild,
  TemplateRef,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSliderModule } from '@angular/material/slider';
import { RouterLink } from '@angular/router';
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

import { Api } from '../../api/api';
import { analyseSnapshot } from '../../api/fn/world/analyse-snapshot';
import { defineFactory } from '../../api/fn/world/define-factory';
import { editFactory } from '../../api/fn/world/edit-factory';
import { getSnapshot } from '../../api/fn/world/get-snapshot';
import { traceFactories } from '../../api/fn/world/trace-factories';
import { getWorldMap } from '../../api/fn/world/get-world-map';
import { previewFactory } from '../../api/fn/world/preview-factory';
import { FootprintDocument } from '../../api/models/footprint-document';
import { DronePortFinding } from '../../api/models/drone-port-finding';
import { GeneratorFinding } from '../../api/models/generator-finding';
import { MachineFinding } from '../../api/models/machine-finding';
import { MapConveyor } from '../../api/models/map-conveyor';
import { PipeFlowFinding } from '../../api/models/pipe-flow-finding';
import { CatalogNames } from '../../shared/catalog-names';
import { megawatts, percent, perMinute, Position, positionInMetres } from '../../shared/display';
import { problemMessage, whenAnalysed } from '../../shared/problem';
import { LimitFacts, LimitParts, limitParts } from '../limit-text';
import {
  contentEntries,
  Contents,
  conveyorColours,
  describeHeadLift,
  describeDronePort,
  describePipeFlow,
  statusColours,
  statusLabels,
  MachineStatus,
} from '../map/map-model';
import { boxGeometry } from './box-geometry';
import { sceneBackground } from './scene-background';
import {
  Area,
  areaAround,
  buildingBoxes,
  curveInArea,
  inArea,
  attachmentArrows,
  sampleCurve3d,
  toScene,
  tubeColour,
} from './area-model';
import { flowArrow } from './flow-arrow';
import { SidebarTab } from '../../sidebar/sidebar-tab';
import { SidebarTabs } from '../../sidebar/sidebar-tabs';
import { framingDistance, pointsToFrame } from './framing';
import { coveredSides } from '../map/visible-part';
import { Explainer } from '../explain/explainer';
import { FactoryDeletion } from '../factory-deletion';
import {
  blockedMachines,
  factoriesInArea,
  machineOwners,
  OutlinedFactory,
} from './factory-outlines';
import { handoverPoint } from './factory-preview';
import { pieceAt, pieceStarts } from './merged-pieces';
import {
  boxSelect,
  choicesTop,
  ClickReach,
  isTap,
  rectBetween,
  ScreenPoint,
  ScreenRect,
  SeenRange,
  toggle,
} from './selection';
import { GameIcon } from '../../shared/game-icon';
import { loaded } from '../../shared/loaded';

/** A building in the area, with what the panel shows about it. */
interface AreaBuilding {
  id: string;
  className: string;
  kind: 'machine' | 'provider' | 'logistics' | 'consumer';
  position: Position;
  yaw: number;
  recipe?: string | null;
  inputContents?: Record<string, number> | null;
  outputContents?: Record<string, number> | null;
  contents?: Record<string, number> | null;
  isGenerator?: boolean;
}

/** How far past a factory's outermost machines its 3D view reaches, in centimetres, below and above too: its belts in and out. */
const factoryMargin = 1500;

/** How see-through what isn't connected to a factory is drawn in its view: there, but not in the way. */
const FadedOpacity = 0.18;

/** What a factory's machines can be (BR-W2): a machine, or a generator (decided 2026-11-11). */
function choosable(building: AreaBuilding | null | undefined): building is AreaBuilding {
  return (
    building?.kind === 'machine' || (building?.kind === 'consumer' && building.isGenerator === true)
  );
}

/**
 * An area of a snapshot in 3D (ADR-0024): every building inside a rectangle drawn on the map, as its clearance boxes,
 * with belts, pipes and lifts along their curves. Machines are coloured by the analysis; a click shows what a building or
 * a belt is and holds, in the sidebar's Info tab; the factories in the area are its Factories tab (asked 2026-11-22).
 * While defining a factory, a click adds or takes out a machine, and a box dragged with shift or alt adds or takes out
 * every machine inside it (BR-W2).
 */
@Component({
  selector: 'app-area-page',
  imports: [
    GameIcon,
    NgTemplateOutlet,
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatSliderModule,
    SidebarTab,
  ],
  templateUrl: './area-page.html',
  styleUrl: './area-page.scss',
})
export class AreaPage {
  private readonly api = inject(Api);
  private readonly factoryDeletion = inject(FactoryDeletion);
  protected readonly names = inject(CatalogNames);
  protected readonly explainer = inject(Explainer);
  private readonly sidebar = inject(SidebarTabs);

  /** From the route: /snapshots/:id/area?x0=…&y0=…&x1=…&y1=… (centimetres), or ?factory=… for the area around a factory. */
  readonly id = input.required<string>();
  readonly x0 = input<string>();
  readonly y0 = input<string>();
  readonly x1 = input<string>();
  readonly y1 = input<string>();
  readonly factory = input<string>();
  /** Shown inside a factory's page (asked 2026-11-19): no title or way back of its own; the page says what changed. */
  readonly embedded = input(false);
  /**
   * What the page around puts at the top of the Info tab: a factory's page, what comes in and what leaves (asked
   * 2026-11-23). Not while defining, where the tab has the steps.
   */
  readonly infoTop = input<TemplateRef<unknown> | null>(null);
  /** A factory saved here, new or edited: its id. */
  readonly saved = output<string>();

  /** What the machines' colours mean: how they run (BR-A11). */
  /** The legend's last entry: what a red belt, lift or pipe means. */
  protected readonly troubleColour = conveyorColours.bottleneck;
  protected readonly statusLegend = (
    ['none', 'starved', 'outputBlocked', 'noRecipe'] as MachineStatus[]
  ).map((status) => ({
    status,
    label: statusLabels[status],
    colour: statusColours[status],
  }));

  protected readonly area = computed<Area>(() => {
    const factoryId = this.factory();
    if (factoryId === undefined)
      return {
        x0: Number(this.x0()),
        y0: Number(this.y0()),
        x1: Number(this.x1()),
        y1: Number(this.y1()),
      };
    const members = new Set(this.factoryMachines());
    const world = loaded(this.world);
    const footprints = new Map(
      world?.footprints.map(
        (footprint) => [footprint.id, footprint] as [string, FootprintDocument],
      ),
    );
    // Every corner of the machines' boxes, so the area reaches from their feet to their roofs.
    const corners = [...(world?.machines ?? []), ...(world?.consumers ?? [])]
      .filter((b) => members.has(b.id))
      .flatMap((b) => buildingBoxes(b, footprints).flat());
    return areaAround(corners, factoryMargin) ?? { x0: 0, y0: 0, x1: 0, y1: 0 };
  });

  /** The machines of the factory shown, by id: none when this is an area drawn on the map. */
  private readonly factoryMachines = computed(() => {
    const factoryId = this.factory();
    return factoryId === undefined
      ? []
      : (loaded(this.factories)
          ?.find((trace) => trace.factoryId === factoryId)
          ?.machines.map((m) => m.id) ?? []);
  });
  /** What's connected to the factory shown, as a preview of its own machines traces it. */
  private readonly factoryParts = resource({
    params: () =>
      this.factoryMachines().length > 0
        ? { id: this.id(), body: { machines: this.factoryMachines() } }
        : undefined,
    loader: ({ params }) => this.api.invoke(previewFactory, params),
  });
  /**
   * In a factory's view, what's connected to it, by id: through its lines, owned or shared, to their ends. The rest is
   * faded but can still be clicked (asked 2026-11-23). Null in an area drawn on the map, and until it's known: then
   * nothing is faded.
   */
  private readonly connected = computed(() => {
    const parts = loaded(this.factoryParts);
    return parts ? new Set(parts.connected) : null;
  });

  protected readonly snapshot = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => this.api.invoke(getSnapshot, params),
  });
  protected readonly world = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => this.api.invoke(getWorldMap, params),
  });
  protected readonly analysis = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params, abortSignal }) =>
      whenAnalysed(() => this.api.invoke$Response(analyseSnapshot, params), abortSignal),
  });
  protected readonly problem = problemMessage;
  protected readonly loaded = loaded;

  /** Every building inside the area, at any height. */
  protected readonly buildings = computed<AreaBuilding[]>(() => {
    const world = loaded(this.world);
    const area = this.area();
    if (!world) return [];
    return [
      ...world.machines.map((m) => ({ ...m, kind: 'machine' as const })),
      ...world.providers.map((p) => ({ ...p, kind: 'provider' as const })),
      ...world.logistics.map((l) => ({ ...l, kind: 'logistics' as const })),
      ...world.consumers.map((c) => ({ ...c, kind: 'consumer' as const })),
    ].filter((b) => inArea(b.position, area));
  });
  protected readonly machineCount = computed(
    () => this.buildings().filter((b) => b.kind === 'machine').length,
  );

  /** What the analysis says about each machine and generator, by id. */
  private readonly findings = computed(() => {
    const analysis = loaded(this.analysis);
    const all: (MachineFinding | GeneratorFinding)[] = [
      ...(analysis?.machines ?? []),
      ...(analysis?.generators ?? []),
    ];
    return new Map(all.map((finding) => [finding.id, finding]));
  });

  /** The world's factories as traced in this snapshot: their machines are outlined, and can't be chosen again (BR-W5). */
  protected readonly factories = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => this.api.invoke(traceFactories, params),
  });
  private readonly owners = computed(() => machineOwners(loaded(this.factories) ?? []));
  /** The machines that can't be chosen: every factory's, or while editing one, the other factories' (BR-W5). */
  private readonly blocked = computed(() => blockedMachines(this.owners(), this.editing()));
  protected readonly factoriesShown = computed(() =>
    factoriesInArea(loaded(this.factories) ?? [], new Set(this.buildings().map((b) => b.id))),
  );

  protected readonly selected = signal<AreaBuilding | null>(null);
  protected readonly selectedFinding = computed(() => {
    const building = this.selected();
    return building ? this.findings().get(building.id) : undefined;
  });
  /** What the drone port looked at burns, in the model (BR-A16): or that no drone refuels there (issue #44). */
  protected readonly selectedDronePort = computed(() => {
    const building = this.selected();
    const ports = loaded(this.analysis)?.dronePorts ?? [];
    return building ? ports.find((port) => port.id === building.id) : undefined;
  });
  /** The belt, pipe or lift looked at (asked 2026-11-20); a building and a belt aren't looked at both at once. */
  protected readonly selectedConveyor = signal<MapConveyor | null>(null);
  private readonly bottlenecks = computed(
    () =>
      new Map(
        (loaded(this.analysis)?.bottlenecks ?? []).map((bottleneck) => [bottleneck.id, bottleneck]),
      ),
  );
  protected readonly selectedBottleneck = computed(() => {
    const conveyor = this.selectedConveyor();
    return conveyor ? this.bottlenecks().get(conveyor.id) : undefined;
  });
  /** What flows through the pipe looked at, in the model; neither it nor head lift is in the save (asked 2026-11-24). */
  protected readonly selectedPipeFlow = computed(() => {
    const conveyor = this.selectedConveyor();
    const flows = loaded(this.analysis)?.pipeFlows ?? [];
    return conveyor ? flows.find((flow) => flow.id === conveyor.id) : undefined;
  });
  protected readonly headLiftColour = conveyorColours.headLift;
  /** The building that glows: the one looked at, except while defining, where a click only turns a machine blue or back. */
  private readonly glowing = computed(() =>
    this.defining() ? null : (this.selected()?.id ?? null),
  );
  /** While defining, the machine or generator clicked, to name in the panel; any other building isn't named. */
  protected readonly clickedMachine = computed(() => {
    const building = this.selected();
    return choosable(building) ? building : null;
  });
  protected readonly selectedOwners = computed(() => {
    const building = this.selected();
    return building ? (this.owners().get(building.id) ?? []) : [];
  });

  /** The lowest and highest building in the area, in metres: the range of the height cut-off. */
  protected readonly heights = computed(() => {
    const zs = this.buildings().map((b) => b.position.z / 100);
    return zs.length
      ? { low: Math.floor(Math.min(...zs)), high: Math.ceil(Math.max(...zs)) }
      : { low: 0, high: 0 };
  });
  protected readonly cutOff = signal<number | null>(null);

  /**
   * Defining a factory: the machines chosen so far, by id, and what to call it. Public, with saving and stopping: a
   * factory's page edits its factory from its own header (asked 2026-11-19).
   */
  readonly defining = signal(false);
  /** The factory being edited; null when a new one is defined. */
  protected readonly editing = signal<string | null>(null);
  protected readonly chosen = signal<ReadonlySet<string>>(new Set());
  readonly factoryName = signal('');
  protected readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly canSave = computed(
    () => this.chosen().size > 0 && this.factoryName().trim() !== '' && !this.saving(),
  );

  /** The chosen machines once the choosing pauses: the preview follows them (part 2b(2)). */
  private readonly settled = signal<readonly string[]>([]);
  /** What the chosen machines would make a factory, traced without saving it: what it would own and hand over. */
  protected readonly preview = resource({
    params: () =>
      this.defining() && this.settled().length > 0
        ? { id: this.id(), body: { machines: [...this.settled()] } }
        : undefined,
    loader: ({ params }) => this.api.invoke(previewFactory, params),
  });
  private readonly owned = computed(() => new Set(loaded(this.preview)?.logistics ?? []));
  private readonly previewLayer = new THREE.Group();
  /** The belt, pipe or lift looked at, drawn again over itself, glowing. */
  private readonly selectionLayer = new THREE.Group();
  /** The ways in and out of the splitter or merger looked at, as arrows (asked 2026-11-26). */
  private readonly attachmentLayer = new THREE.Group();
  /** The box being dragged, on screen, while shift or alt is held, or while boxing on a touch screen. */
  protected readonly dragBox = signal<ScreenRect | null>(null);
  /**
   * Boxing machines on a touch screen, with no keys to hold (asked 2026-11-24, ADR-0033): while defining, a button turns
   * it on; the view stops turning, and a one-finger drag draws a box instead.
   */
  protected readonly boxing = signal(false);
  /** The box drawn while boxing, once let go: its machines to add or take out. */
  protected readonly drawnBox = signal<ScreenRect | null>(null);
  /** Where the choices for the box drawn stand, from the view's top: beside the box (choicesTop). */
  protected readonly choicesAt = signal(0);

  protected readonly percent = percent;
  protected readonly perMinute = perMinute;
  protected readonly megawatts = megawatts;
  protected readonly position = positionInMetres;

  private readonly stage = viewChild.required<ElementRef<HTMLElement>>('stage');
  private renderer?: THREE.WebGLRenderer;
  private camera?: THREE.PerspectiveCamera;
  private controls?: OrbitControls;
  private readonly scene = new THREE.Scene();
  private readonly content = new THREE.Group();
  private readonly materials = new Map<string, THREE.MeshLambertMaterial>();
  /** Cuts belts and pipes off above the roof of the highest floor still shown (see applyCutOff). */
  private readonly clip = new THREE.Plane(new THREE.Vector3(0, -1, 0), Infinity);
  /** Cuts belts and pipes off at the sides, floor and roof of a factory's area (see clipToArea); in a drawn area, nowhere. */
  private readonly bounds = [
    new THREE.Vector3(1, 0, 0),
    new THREE.Vector3(-1, 0, 0),
    new THREE.Vector3(0, 1, 0),
    new THREE.Vector3(0, -1, 0),
    new THREE.Vector3(0, 0, 1),
    new THREE.Vector3(0, 0, -1),
  ].map((normal) => new THREE.Plane(normal, Infinity));
  private readonly clips = [this.clip, ...this.bounds];
  private framed = false;

  constructor() {
    this.scene.add(this.content);
    this.scene.add(this.previewLayer);
    this.scene.add(this.selectionLayer);
    this.scene.add(this.attachmentLayer);
    this.scene.add(new THREE.HemisphereLight(0xffffff, 0x445566, 2.2));
    const sun = new THREE.DirectionalLight(0xffffff, 1.6);
    sun.position.set(60, 140, 90);
    this.scene.add(sun);

    afterNextRender(() => this.start());
    // Where the open tab would lie over the view, it starts closed, until something in the view is clicked.
    this.sidebar.closeWhereItCovers();
    inject(DestroyRef).onDestroy(() => this.stop());

    // Redraw when the world or its analysis arrives; three.js knows nothing about signals.
    effect(() => this.draw());
    effect(() => this.applyCutOff(this.cutOff()));
    effect(() => this.paint(this.glowing(), this.chosen(), this.owned()));

    // The preview follows the choice once it pauses for a moment, not every machine a dragged box sweeps in.
    effect((onCleanup) => {
      const chosen = [...this.chosen()];
      const timer = setTimeout(() => this.settled.set(chosen), 300);
      onCleanup(() => clearTimeout(timer));
    });
    effect(() => this.drawPreview());
    effect(() => this.drawSelectedConveyor());
    effect(() => this.drawAttachmentArrows());
  }

  /** Defining a factory shows the Info tab, which has the steps; the name and Save are in the page's header. */
  protected startDefining(): void {
    this.showStepsBeside();
    this.defining.set(true);
    this.settled.set([]);
    this.saveError.set(null);
  }

  /** Edits a factory as a new one is defined: its machines chosen, its name filled in. */
  protected startEditing(factory: Pick<OutlinedFactory, 'factoryId' | 'name'>): void {
    const machines =
      loaded(this.factories)?.find((trace) => trace.factoryId === factory.factoryId)?.machines ??
      [];
    this.editing.set(factory.factoryId);
    this.chosen.set(new Set(machines.map((machine) => machine.id)));
    this.factoryName.set(factory.name);
    this.settled.set([]);
    this.saveError.set(null);
    this.showStepsBeside();
    this.defining.set(true);
  }

  /**
   * The Info tab, where defining shows what the machines chosen make and take: opened where it stands beside the view;
   * where it would lie over it, on a phone or a tablet, it stays closed until a machine is added or taken out (asked
   * 2026-11-24).
   */
  private showStepsBeside(): void {
    this.sidebar.show('info');
    this.sidebar.closeWhereItCovers();
  }

  /** Edits a factory by its id, once its machines are known: the factory's page asks this from its Edit button. */
  edit(factoryId: string): void {
    const trace = loaded(this.factories)?.find((candidate) => candidate.factoryId === factoryId);
    if (trace) this.startEditing({ factoryId, name: trace.name });
  }

  /** After asking; then the factories again, without it. */
  protected async deleteFactory(factory: OutlinedFactory): Promise<void> {
    if (!(await this.factoryDeletion.confirmAndDelete(factory))) return;
    if (this.editing() === factory.factoryId) this.stopDefining();
    this.factories.reload();
  }

  /** The select button while defining: boxing starts, and the view no longer turns; or it stops, letting go of a box. */
  protected toggleBoxing(): void {
    const on = !this.boxing();
    this.boxing.set(on);
    this.letGoOfBox();
    if (this.controls) this.controls.enabled = !on;
  }

  /** Adds or takes out every machine in the box drawn. Boxing goes on, so the next box can be drawn at once. */
  protected useBox(mode: 'add' | 'remove'): void {
    const box = this.drawnBox();
    if (!box) return;
    const stage = this.stage().nativeElement;
    this.chosen.update((chosen) => boxSelect(chosen, this.machinesOnScreen(stage), box, mode));
    this.letGoOfBox();
    // What they make and take now, in the Info tab, as after a tap on a machine.
    this.sidebar.show('info');
  }

  protected letGoOfBox(): void {
    this.drawnBox.set(null);
    this.dragBox.set(null);
  }

  stopDefining(): void {
    if (this.boxing()) this.toggleBoxing();
    this.defining.set(false);
    this.editing.set(null);
    this.chosen.set(new Set());
    this.settled.set([]);
    this.factoryName.set('');
    this.saveError.set(null);
  }

  /**
   * Stores the factory, new or edited, in the snapshot's world, and stays: in the 3D view of an area the Factories tab
   * shows it among the others, so the next one can be defined (asked 2026-11-23). It no longer opens the factory's page.
   */
  async save(): Promise<void> {
    const snapshot = loaded(this.snapshot);
    if (!snapshot || !this.canSave()) return;
    this.saving.set(true);
    this.saveError.set(null);
    try {
      const name = this.factoryName().trim();
      const machines = [...this.chosen()];
      const editing = this.editing();
      const factory = editing
        ? await this.api.invoke(editFactory, { id: editing, body: { name, machines } })
        : await this.api.invoke(defineFactory, {
            body: { world: snapshot.sessionName, name, machines },
          });
      this.saved.emit(factory.id);
      // Stop defining, and show its machines as they are now, edged in its colour.
      this.stopDefining();
      this.factories.reload();
      // The factory's own page has no Factories tab: it reloads its findings on saved.
      if (!this.embedded()) this.sidebar.show('factories');
    } catch (error) {
      this.saveError.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  /** What the panel says about why the selected machine runs as it does: words around its items, each with its icon. */
  protected why(finding: LimitFacts): LimitParts | null {
    const parts = limitParts(finding);
    return parts.before || parts.items.length > 0 ? parts : null;
  }

  protected statusLabel(finding: { limit: MachineStatus }): string {
    return statusLabels[finding.limit];
  }

  /** What a drone port burns in the model, for the panel. */
  protected dronePort(port: DronePortFinding): string {
    return describeDronePort(port, (item) => this.names.item(item));
  }

  /** What flows through a pipe in the model, for the panel. */
  protected pipeFlow(flow: PipeFlowFinding): string {
    return describePipeFlow(flow, (item) => this.names.item(item));
  }

  /** A pipe's head lift in the model; "" for a belt, and a pipe nothing feeds. */
  protected headLift(toSpare: number | null | undefined): string {
    return toSpare === null || toSpare === undefined ? '' : describeHeadLift(toSpare);
  }

  /** What a machine, belt or container holds, item by item, for the panel to show with icons. */
  protected entries(contents: Contents): { item: string; count: number }[] {
    return contentEntries(contents);
  }

  private start(): void {
    const stage = this.stage().nativeElement;
    this.renderer = new THREE.WebGLRenderer({ antialias: true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.localClippingEnabled = true;
    // As big as the stage. Its stylesheet can't say so: three.js makes the canvas, so it lacks the attribute Angular
    // scopes the component's styles by. Unsized, it would show at its pixels' size: twice the stage on a phone's screen.
    Object.assign(this.renderer.domElement.style, {
      display: 'block',
      width: '100%',
      height: '100%',
    });
    stage.prepend(this.renderer.domElement);
    this.camera = new THREE.PerspectiveCamera(45, 1, 0.5, 20000);
    this.controls = new OrbitControls(this.camera, this.renderer.domElement);
    this.controls.screenSpacePanning = true;
    // The stage's own background, as the browser worked it out: the site's colours, never white (asked 2026-11-20).
    this.scene.background = new THREE.Color(
      sceneBackground(getComputedStyle(stage).backgroundColor),
    );

    const resize = () => {
      const { clientWidth: w, clientHeight: h } = stage;
      this.renderer!.setSize(w, h, false);
      this.camera!.aspect = w / h;
      this.camera!.updateProjectionMatrix();
    };
    new ResizeObserver(resize).observe(stage);
    resize();
    this.listenForClicks(this.renderer.domElement);
    this.listenForBoxes(stage);
    this.renderer.setAnimationLoop(() => {
      this.controls!.update();
      this.renderer!.render(this.scene, this.camera!);
    });
    this.draw();
  }

  private stop(): void {
    this.renderer?.setAnimationLoop(null);
    this.controls?.dispose();
    this.renderer?.dispose();
    this.content.traverse((object) => (object as THREE.Mesh).geometry?.dispose());
    this.materials.forEach((material) => material.dispose());
    this.outlineMaterials.forEach((material) => material.dispose());
  }

  /** A line material per factory colour, for the edges of its machines; faded with them. */
  private readonly outlineMaterials = new Map<string, THREE.LineBasicMaterial>();

  private outlineMaterial(colour: string, faded: boolean): THREE.LineBasicMaterial {
    const key = `${colour}${faded ? ':faded' : ''}`;
    let material = this.outlineMaterials.get(key);
    if (!material) {
      material = new THREE.LineBasicMaterial({
        color: colour,
        transparent: faded,
        opacity: faded ? FadedOpacity : 1,
      });
      this.outlineMaterials.set(key, material);
    }
    return material;
  }

  /** A material per colour; belts and pipes get their own, cut off by the clipping planes; what isn't connected, a faded one. */
  private material(colour: string, clipped = false, faded = false): THREE.MeshLambertMaterial {
    const key = `${colour}${clipped ? ':clipped' : ''}${faded ? ':faded' : ''}`;
    let material = this.materials.get(key);
    if (!material) {
      material = new THREE.MeshLambertMaterial({
        color: colour,
        side: THREE.DoubleSide,
        clippingPlanes: clipped ? this.clips : [],
      });
      if (faded)
        Object.assign(material, { transparent: true, opacity: FadedOpacity, depthWrite: false });
      this.materials.set(key, material);
    }
    return material;
  }

  private draw(): void {
    const world = loaded(this.world);
    const buildings = this.buildings();
    const findings = this.findings();
    const owners = this.owners();
    const connected = this.connected();
    const faded = (id: string) => connected !== null && !connected.has(id);
    if (!this.renderer || !world) return;

    this.content.clear();
    const area = this.area();
    const middle = { x: (area.x0 + area.x1) / 2, y: (area.y0 + area.y1) / 2 };
    this.clipToArea(area, middle);
    const footprints = new Map(
      world.footprints.map((footprint) => [footprint.id, footprint] as [string, FootprintDocument]),
    );
    const at = (p: Position) => new THREE.Vector3(...toScene(p, middle));

    for (const building of buildings) {
      const finding = findings.get(building.id);
      const colour = finding
        ? statusColours[finding.limit]
        : building.kind === 'machine'
          ? statusColours.unknown
          : building.kind === 'consumer'
            ? '#ab47bc'
            : '#78909c';
      const boxes = buildingBoxes(building, footprints);
      const geometry = mergeGeometries(boxes.map((box) => boxGeometry(box, at)));
      const material = this.material(colour, false, faded(building.id));
      const mesh = new THREE.Mesh(geometry, material);
      const top = Math.max(...boxes.flat().map((corner) => corner.z)) / 100;
      mesh.userData = { building, material, z: building.position.z / 100, top };
      // A factory's machines are outlined in its colour. As a child of the mesh, the outline hides with it.
      const owner = owners.get(building.id)?.[0];
      if (owner)
        mesh.add(
          new THREE.LineSegments(
            new THREE.EdgesGeometry(geometry),
            this.outlineMaterial(owner.colour, faded(building.id)),
          ),
        );
      this.content.add(mesh);
    }

    // Belts, pipes and lifts, merged per kind and per metre of their lowest point: thousands of meshes would slow a big
    // area down, and one mesh per kind couldn't follow the height cut-off. Each mesh keeps which belt lies where in it,
    // so a click can find the belt it hit.
    const bottleneckIds = new Set(this.bottlenecks().keys());
    const tubes = new Map<
      string,
      { conveyors: MapConveyor[]; geometries: THREE.BufferGeometry[] }
    >();
    for (const conveyor of world.conveyors) {
      if (conveyor.curve.length < 2 || !curveInArea(conveyor.curve, area)) continue;
      const base = Math.floor(Math.min(...sampleCurve3d(conveyor.curve).map((p) => p.z)) / 100);
      // Per colour rather than kind: a bottleneck or a belt with a loose end is red (asked 2026-11-23).
      const key = `${tubeColour(conveyor, bottleneckIds)}:${base}:${faded(conveyor.id)}`;
      const group = tubes.get(key) ?? { conveyors: [], geometries: [] };
      group.conveyors.push(conveyor);
      group.geometries.push(this.tube(conveyor, at, 0));
      tubes.set(key, group);
    }
    for (const [key, { conveyors, geometries }] of tubes) {
      const [colour, base, fade] = key.split(':');
      const mesh = new THREE.Mesh(
        mergeGeometries(geometries),
        this.material(colour, true, fade === 'true'),
      );
      const starts = pieceStarts(geometries.map((geometry) => geometry.index!.count / 3));
      mesh.userData = { z: Number(base), conveyors, starts };
      this.content.add(mesh);
    }

    if (!this.framed && this.content.children.length) {
      this.frame(world.conveyors, area, at);
      this.framed = true;
    }

    // A redraw makes new meshes: the cut-off and the colours of the selection apply to those too.
    untracked(() => {
      this.applyCutOff(this.cutOff());
      this.paint(this.glowing(), this.chosen(), this.owned());
    });
  }

  /**
   * The preview in 3D: the belts and pipes the factory would own drawn again over the others, a little thicker, and a
   * marker where each of its lines crosses: green going out, orange coming in.
   */
  private drawPreview(): void {
    const preview = loaded(this.preview);
    const world = loaded(this.world);
    this.previewLayer.clear();
    if (!preview || !world) return;

    const area = this.area();
    const middle = { x: (area.x0 + area.x1) / 2, y: (area.y0 + area.y1) / 2 };
    const at = (p: Position) => new THREE.Vector3(...toScene(p, middle));
    const owned = new Set(preview.logistics);
    const tubes: THREE.BufferGeometry[] = [];
    for (const conveyor of world.conveyors) {
      if (
        !owned.has(conveyor.id) ||
        conveyor.curve.length < 2 ||
        !curveInArea(conveyor.curve, area)
      )
        continue;
      tubes.push(this.tube(conveyor, at, 0.1));
    }
    if (tubes.length > 0)
      this.previewLayer.add(new THREE.Mesh(mergeGeometries(tubes), this.ownedMaterial));

    const conveyors = new Map(world.conveyors.map((conveyor) => [conveyor.id, conveyor]));
    const positions = new Map(this.buildings().map((building) => [building.id, building.position]));
    const lines = [
      [preview.outputs, 'output', this.outputMarker],
      [preview.inputs, 'input', this.inputMarker],
    ] as const;
    for (const [connections, direction, material] of lines) {
      for (const connection of connections) {
        const point = handoverPoint(
          connection,
          direction,
          (id) => conveyors.get(id),
          (id) => positions.get(id),
        );
        if (!point || !inArea(point, area)) continue;
        const marker = new THREE.Mesh(this.markerGeometry, material);
        marker.position.copy(at(point));
        this.previewLayer.add(marker);
      }
    }
  }

  /**
   * The splitter or merger looked at, with its ways in and out as arrows at the ends of its belts (asked 2026-11-26);
   * nothing while defining, as with a building.
   */
  private drawAttachmentArrows(): void {
    const building = this.selected();
    const world = loaded(this.world);
    this.attachmentLayer.clear();
    if (this.defining() || !world || !building?.className.startsWith('Build_ConveyorAttachment'))
      return;
    const area = this.area();
    const middle = { x: (area.x0 + area.x1) / 2, y: (area.y0 + area.y1) / 2 };
    for (const arrow of attachmentArrows(building.position, world.conveyors))
      this.attachmentLayer.add(
        flowArrow(new THREE.Vector3(...toScene(arrow.at, middle)), arrow.along, arrow.way),
      );
  }

  /** The belt, pipe or lift looked at, a little thicker and glowing; nothing while defining, as with a building. */
  private drawSelectedConveyor(): void {
    const conveyor = this.selectedConveyor();
    const defining = this.defining();
    this.selectionLayer.traverse((object) => (object as THREE.Mesh).geometry?.dispose());
    this.selectionLayer.clear();
    if (!conveyor || defining || conveyor.curve.length < 2) return;
    const area = this.area();
    const middle = { x: (area.x0 + area.x1) / 2, y: (area.y0 + area.y1) / 2 };
    const at = (p: Position) => new THREE.Vector3(...toScene(p, middle));
    this.selectionLayer.add(
      new THREE.Mesh(this.tube(conveyor, at, 0.15), this.highlightTubeMaterial),
    );
  }

  /** A belt, pipe or lift as a tube along its curve; thicker by the extra metres, to be drawn over itself. */
  private tube(
    conveyor: MapConveyor,
    at: (p: Position) => THREE.Vector3,
    extra: number,
  ): THREE.TubeGeometry {
    const points = sampleCurve3d(conveyor.curve).map(at);
    const radius = (conveyor.kind === 'pipe' ? 0.45 : 0.3) + extra;
    return new THREE.TubeGeometry(
      new THREE.CatmullRomCurve3(points),
      Math.max(2, points.length),
      radius,
      5,
      false,
    );
  }

  /**
   * The height cut-off (metres), so one floor can be seen from above: every building, belt and pipe whose base is above
   * it is hidden. Belts and pipes that climb higher, such as a pipe to the floors above, are cut off at the roof of the
   * tallest building still shown, so they don't stick out of the floor you're looking at.
   */
  private applyCutOff(limit: number | null): void {
    let roof = -Infinity;
    for (const object of this.content.children) {
      object.visible = limit === null || object.userData['z'] <= limit;
      if (object.visible && object.userData['building'])
        roof = Math.max(roof, object.userData['top'] as number);
    }
    this.clip.constant = limit === null ? Infinity : roof;
  }

  /**
   * Cuts belts and pipes off where a factory's area ends, at its sides, floor and roof, so a long belt out doesn't run
   * on across the scene (asked 2026-11-23). An area drawn on the map has no floor or roof: there they run on, as before.
   */
  private clipToArea(area: Area, middle: { x: number; y: number }): void {
    if (area.z0 === undefined || area.z1 === undefined) {
      this.bounds.forEach((plane) => (plane.constant = Infinity));
      return;
    }
    // A plane keeps what lies on the side its normal points to: normal . p + constant >= 0.
    const [west, floor, north] = toScene({ x: area.x0, y: area.y0, z: area.z0 }, middle);
    const [east, roof, south] = toScene({ x: area.x1, y: area.y1, z: area.z1 }, middle);
    [-west, east, -floor, roof, -north, south].forEach(
      (constant, i) => (this.bounds[i].constant = constant),
    );
  }

  /**
   * Looks at the whole area from the south-east, a little from above. At its buildings, not at all that's drawn: a belt
   * out of a factory runs on in the scene, cut off only where the area ends, and would pull the middle its way. An area
   * with no buildings, only belts and pipes, is framed at them where they lie in it.
   */
  private frame(conveyors: MapConveyor[], area: Area, at: (p: Position) => THREE.Vector3): void {
    const bounds = new THREE.Box3();
    for (const object of this.content.children)
      if (object.userData['building']) bounds.expandByObject(object);
    if (bounds.isEmpty())
      for (const point of pointsToFrame(
        conveyors.map((conveyor) => conveyor.curve),
        area,
      ))
        bounds.expandByPoint(at(point));
    const middle = bounds.getCenter(new THREE.Vector3());
    const size = bounds.getSize(new THREE.Vector3());
    // Seen from a corner, a floor's width across is its diagonal.
    const across = Math.max(Math.hypot(size.x, size.z), 20);
    this.camera!.position.copy(middle).add(
      new THREE.Vector3(1, 0.75, 1.15)
        .normalize()
        .multiplyScalar(framingDistance({ across, high: size.y }, this.camera!.aspect)),
    );
    this.controls!.target.copy(middle);
  }

  /**
   * A click without a drag selects the building, belt, pipe or lift under the pointer; a click on nothing clears the
   * selection. While defining a factory, a click on a machine or generator also adds it or takes it out.
   */
  private listenForClicks(canvas: HTMLCanvasElement): void {
    let down = { x: 0, y: 0 };
    const ray = new THREE.Raycaster();
    canvas.addEventListener(
      'pointerdown',
      (event) => (down = { x: event.clientX, y: event.clientY }),
    );
    canvas.addEventListener('pointerup', (event) => {
      if (Math.hypot(event.clientX - down.x, event.clientY - down.y) > ClickReach) return;
      const rect = canvas.getBoundingClientRect();
      ray.setFromCamera(
        new THREE.Vector2(
          ((event.clientX - rect.left) / rect.width) * 2 - 1,
          -((event.clientY - rect.top) / rect.height) * 2 + 1,
        ),
        this.camera!,
      );
      const shown = this.content.children.filter(
        (o) => o.visible && (o.userData['building'] || o.userData['conveyors']),
      );
      // The ray passes through what the cut-off and the area's bounds clip away: a belt counts only where it's still drawn.
      const hit = ray
        .intersectObjects(shown, false)
        .find(
          (h) =>
            !h.object.userData['conveyors'] ||
            this.clips.every((plane) => plane.distanceToPoint(h.point) >= 0),
        );
      const building = (hit?.object.userData['building'] as AreaBuilding | undefined) ?? null;
      const conveyors = hit?.object.userData['conveyors'] as MapConveyor[] | undefined;
      this.selected.set(building);
      this.selectedConveyor.set(
        conveyors && hit?.faceIndex != null
          ? conveyors[pieceAt(hit.object.userData['starts'], hit.faceIndex)]
          : null,
      );
      // What was clicked is in the Info tab: shown, and the sidebar opened, also when it was collapsed (asked 2026-11-22).
      if (building || this.selectedConveyor()) this.sidebar.show('info');
      if (this.defining() && choosable(building) && !this.blocked().has(building.id))
        this.chosen.update((chosen) => toggle(chosen, building.id));
    });
  }

  /**
   * While defining a factory, a drag with shift or alt draws a box instead of turning the view: on release, every
   * machine shown whose middle lies inside it is added (shift) or taken out (alt). The stage listens in the capture
   * phase, so it hears the press before OrbitControls on the canvas does, and can switch the controls off in time.
   *
   * While boxing on a touch screen, a one-finger drag draws the box too, and on release it stays, for the page to offer
   * to add or take out its machines. A tap draws none: the click on the machine under it adds or takes it out. The
   * pointer isn't captured then: a touch stays with the canvas it pressed, whose click listens for the release.
   */
  private listenForBoxes(stage: HTMLElement): void {
    let start: ScreenPoint | null = null;
    let mode: 'add' | 'remove' | 'offer' = 'add';
    const at = (event: PointerEvent): ScreenPoint => {
      const rect = stage.getBoundingClientRect();
      return { x: event.clientX - rect.left, y: event.clientY - rect.top };
    };
    stage.addEventListener(
      'pointerdown',
      (event) => {
        if (!this.defining() || event.button !== 0) return;
        if (event.shiftKey || event.altKey) {
          mode = event.altKey ? 'remove' : 'add';
          this.controls!.enabled = false;
          stage.setPointerCapture(event.pointerId);
          event.preventDefault();
        } else if (this.boxing() && event.isPrimary && event.target === this.renderer?.domElement) {
          // On the canvas only: a press on the choices or the select button, which lie over it, would let go of the box
          // before the button's click comes to use it (asked 2026-11-24: Add did nothing).
          mode = 'offer';
          this.letGoOfBox();
        } else return;
        start = at(event);
      },
      { capture: true },
    );
    stage.addEventListener('pointermove', (event) => {
      if (start && event.isPrimary) this.dragBox.set(rectBetween(start, at(event)));
    });
    const letGo = (event: PointerEvent) => {
      if (!start || !event.isPrimary) return;
      const box = rectBetween(start, at(event));
      start = null;
      if (mode === 'offer') {
        if (isTap(box)) this.letGoOfBox();
        else {
          this.choicesAt.set(choicesTop(box, this.seenRange(stage)));
          this.drawnBox.set(box);
        }
        return;
      }
      // A const, as the callback below can't rely on a narrowed outer variable.
      const keyed = mode;
      this.dragBox.set(null);
      this.controls!.enabled = true;
      stage.releasePointerCapture(event.pointerId);
      this.chosen.update((chosen) => boxSelect(chosen, this.machinesOnScreen(stage), box, keyed));
    };
    stage.addEventListener('pointerup', letGo);
    stage.addEventListener('pointercancel', letGo);
  }

  /**
   * The part of the stage in sight, from its top: inside the window, below the toolbar, which the page scrolls under, and
   * clear of the sidebar where it lies over the stage, as the bar of tabs and the sheet do on a phone.
   */
  private seenRange(stage: HTMLElement): SeenRange {
    const rect = stage.getBoundingClientRect();
    const toolbar = document.querySelector('.toolbar')?.getBoundingClientRect().bottom ?? 0;
    const shown = {
      left: rect.left,
      right: rect.right,
      top: Math.max(rect.top, toolbar),
      bottom: Math.min(rect.bottom, window.innerHeight),
    };
    const covered = coveredSides(
      shown,
      document.querySelector('app-sidebar')?.getBoundingClientRect() ?? null,
    );
    return {
      top: shown.top + covered.top - rect.top,
      bottom: shown.bottom - covered.bottom - rect.top,
    };
  }

  /**
   * Where the middle of every machine shown lies on screen, in pixels from the stage's top left. Machines of another
   * factory are left out: a machine belongs to one factory at most (BR-W5).
   */
  private machinesOnScreen(stage: HTMLElement): (ScreenPoint & { id: string })[] {
    const { clientWidth: width, clientHeight: height } = stage;
    const middle = new THREE.Vector3();
    const blocked = this.blocked();
    return this.content.children
      .filter((o) => {
        const building = o.userData['building'] as AreaBuilding | undefined;
        return o.visible && choosable(building) && !blocked.has(building.id);
      })
      .flatMap((o) => {
        const geometry = (o as THREE.Mesh).geometry;
        if (!geometry.boundingBox) geometry.computeBoundingBox();
        geometry.boundingBox!.getCenter(middle).project(this.camera!);
        // Behind the camera: projecting goes through it, so the machine would land on screen mirrored.
        if (middle.z > 1) return [];
        return [
          {
            id: (o.userData['building'] as AreaBuilding).id,
            x: ((middle.x + 1) / 2) * width,
            y: ((1 - middle.y) / 2) * height,
          },
        ];
      });
  }

  /** The building looked at glows a little, so it can be found again after turning the view; chosen machines are blue. */
  private readonly highlightMaterial = new THREE.MeshLambertMaterial({
    color: '#ffffff',
    emissive: '#ffd54f',
    emissiveIntensity: 0.6,
    side: THREE.DoubleSide,
  });
  private readonly highlightTubeMaterial = new THREE.MeshLambertMaterial({
    color: '#ffffff',
    emissive: '#ffd54f',
    emissiveIntensity: 0.6,
    side: THREE.DoubleSide,
    clippingPlanes: this.clips,
  });
  private readonly chosenMaterial = new THREE.MeshLambertMaterial({
    color: '#42a5f5',
    emissive: '#1565c0',
    emissiveIntensity: 0.5,
    side: THREE.DoubleSide,
  });
  /** The preview: what the factory would own in teal, its outputs green and its inputs orange. */
  private readonly ownedMaterial = new THREE.MeshLambertMaterial({
    color: '#26a69a',
    emissive: '#00695c',
    emissiveIntensity: 0.5,
    side: THREE.DoubleSide,
    clippingPlanes: this.clips,
  });
  private readonly outputMarker = new THREE.MeshLambertMaterial({
    color: '#66bb6a',
    emissive: '#2e7d32',
    emissiveIntensity: 0.6,
    clippingPlanes: this.clips,
  });
  private readonly inputMarker = new THREE.MeshLambertMaterial({
    color: '#ffa726',
    emissive: '#e65100',
    emissiveIntensity: 0.6,
    clippingPlanes: this.clips,
  });
  private readonly markerGeometry = new THREE.SphereGeometry(1.2, 12, 8);

  private paint(
    selectedId: string | null,
    chosen: ReadonlySet<string>,
    owned: ReadonlySet<string>,
  ): void {
    for (const object of this.content.children) {
      const building = object.userData['building'] as AreaBuilding | undefined;
      if (!building) continue;
      (object as THREE.Mesh).material =
        building.id === selectedId
          ? this.highlightMaterial
          : chosen.has(building.id)
            ? this.chosenMaterial
            : owned.has(building.id)
              ? this.ownedMaterial
              : (object.userData['material'] as THREE.Material);
    }
  }
}
