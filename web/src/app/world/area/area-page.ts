import { Component, DestroyRef, ElementRef, afterNextRender, computed, effect, inject, input, resource, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSliderModule } from '@angular/material/slider';
import { Router, RouterLink } from '@angular/router';
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
import { GeneratorFinding } from '../../api/models/generator-finding';
import { MachineFinding } from '../../api/models/machine-finding';
import { CatalogNames } from '../../shared/catalog-names';
import { percent, perMinute, Position, positionInMetres } from '../../shared/display';
import { problemMessage, whenAnalysed } from '../../shared/problem';
import { LimitFacts, limitText } from '../limit-text';
import { conveyorColours, describeContents, statusColours, statusLabels, MachineStatus } from '../map/map-model';
import { Area, areaAround, Box, buildingBoxes, curveInArea, inArea, sampleCurve3d, toScene } from './area-model';
import { FactoryDeletion } from '../factory-deletion';
import { blockedMachines, factoriesInArea, machineOwners, OutlinedFactory } from './factory-outlines';
import { handoverPoint } from './factory-preview';
import { boxSelect, rectBetween, ScreenPoint, ScreenRect, toggle } from './selection';

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

/** How far past a factory's outermost machines its 3D view reaches, in centimetres: its belts in and out. */
const factoryMargin = 1500;

/** What a factory's machines can be (BR-W2): a machine, or a generator (decided 2026-11-11). */
function choosable(building: AreaBuilding | null | undefined): building is AreaBuilding {
  return building?.kind === 'machine' || (building?.kind === 'consumer' && building.isGenerator === true);
}

/** The six sides of a box, as corner numbers (see Box): drawn as two triangles each. */
const SIDES = [[0, 1, 3, 2], [4, 6, 7, 5], [0, 4, 5, 1], [2, 3, 7, 6], [0, 2, 6, 4], [1, 5, 7, 3]];

/**
 * An area of a snapshot in 3D (ADR-0024): every building inside a rectangle drawn on the map, as its clearance boxes,
 * with belts and pipes along their curves. Machines are coloured by the analysis; a click shows what one is and holds.
 * While defining a factory, a click adds or takes out a machine, and a box dragged with shift or alt adds or takes out
 * every machine inside it (BR-W2).
 */
@Component({
  selector: 'app-area-page',
  imports: [FormsModule, RouterLink, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressBarModule, MatSliderModule],
  templateUrl: './area-page.html',
  styleUrl: './area-page.scss',
})
export class AreaPage {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly factoryDeletion = inject(FactoryDeletion);
  protected readonly names = inject(CatalogNames);

  /** From the route: /snapshots/:id/area?x0=…&y0=…&x1=…&y1=… (centimetres), or ?factory=… for the area around a factory. */
  readonly id = input.required<string>();
  readonly x0 = input<string>();
  readonly y0 = input<string>();
  readonly x1 = input<string>();
  readonly y1 = input<string>();
  readonly factory = input<string>();

  protected readonly area = computed<Area>(() => {
    const factoryId = this.factory();
    if (factoryId === undefined) return { x0: Number(this.x0()), y0: Number(this.y0()), x1: Number(this.x1()), y1: Number(this.y1()) };
    const members = new Set(this.factories.value()?.find((trace) => trace.factoryId === factoryId)?.machines.map((m) => m.id));
    const world = this.world.value();
    const positions = [...(world?.machines ?? []), ...(world?.consumers ?? [])].filter((b) => members.has(b.id)).map((b) => b.position);
    return areaAround(positions, factoryMargin) ?? { x0: 0, y0: 0, x1: 0, y1: 0 };
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
    loader: ({ params, abortSignal }) => whenAnalysed(() => this.api.invoke$Response(analyseSnapshot, params), abortSignal),
  });
  protected readonly problem = problemMessage;

  /** Every building inside the area, at any height. */
  protected readonly buildings = computed<AreaBuilding[]>(() => {
    const world = this.world.value();
    const area = this.area();
    if (!world) return [];
    return [
      ...world.machines.map((m) => ({ ...m, kind: 'machine' as const })),
      ...world.providers.map((p) => ({ ...p, kind: 'provider' as const })),
      ...world.logistics.map((l) => ({ ...l, kind: 'logistics' as const })),
      ...world.consumers.map((c) => ({ ...c, kind: 'consumer' as const })),
    ].filter((b) => inArea(b.position, area));
  });
  protected readonly machineCount = computed(() => this.buildings().filter((b) => b.kind === 'machine').length);

  /** What the analysis says about each machine and generator, by id. */
  private readonly findings = computed(() => {
    const analysis = this.analysis.value();
    const all: (MachineFinding | GeneratorFinding)[] = [...(analysis?.machines ?? []), ...(analysis?.generators ?? [])];
    return new Map(all.map((finding) => [finding.id, finding]));
  });

  /** The world's factories as traced in this snapshot: their machines are outlined, and can't be chosen again (BR-W5). */
  protected readonly factories = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => this.api.invoke(traceFactories, params),
  });
  private readonly owners = computed(() => machineOwners(this.factories.value() ?? []));
  /** The machines that can't be chosen: every factory's, or while editing one, the other factories' (BR-W5). */
  private readonly blocked = computed(() => blockedMachines(this.owners(), this.editing()));
  protected readonly factoriesShown = computed(() => factoriesInArea(this.factories.value() ?? [], new Set(this.buildings().map((b) => b.id))));

  protected readonly selected = signal<AreaBuilding | null>(null);
  protected readonly selectedFinding = computed(() => {
    const building = this.selected();
    return building ? this.findings().get(building.id) : undefined;
  });
  /** The building that glows: the one looked at, except while defining, where a click only turns a machine blue or back. */
  private readonly glowing = computed(() => (this.defining() ? null : (this.selected()?.id ?? null)));
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
    return zs.length ? { low: Math.floor(Math.min(...zs)), high: Math.ceil(Math.max(...zs)) } : { low: 0, high: 0 };
  });
  protected readonly cutOff = signal<number | null>(null);

  /** Defining a factory: the machines chosen so far, by id, and what to call it. */
  protected readonly defining = signal(false);
  /** The factory being edited; null when a new one is defined. */
  protected readonly editing = signal<string | null>(null);
  protected readonly chosen = signal<ReadonlySet<string>>(new Set());
  protected readonly factoryName = signal('');
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);
  protected readonly canSave = computed(() => this.chosen().size > 0 && this.factoryName().trim() !== '' && !this.saving());

  /** The chosen machines once the choosing pauses: the preview follows them (part 2b(2)). */
  private readonly settled = signal<readonly string[]>([]);
  /** What the chosen machines would make a factory, traced without saving it: what it would own and hand over. */
  protected readonly preview = resource({
    params: () => (this.defining() && this.settled().length > 0 ? { id: this.id(), body: { machines: [...this.settled()] } } : undefined),
    loader: ({ params }) => this.api.invoke(previewFactory, params),
  });
  private readonly owned = computed(() => new Set(this.preview.value()?.logistics ?? []));
  private readonly previewLayer = new THREE.Group();
  /** The box being dragged, on screen, while shift or alt is held. */
  protected readonly dragBox = signal<ScreenRect | null>(null);

  protected readonly percent = percent;
  protected readonly perMinute = perMinute;
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
  private framed = false;

  constructor() {
    this.scene.add(this.content);
    this.scene.add(this.previewLayer);
    this.scene.add(new THREE.HemisphereLight(0xffffff, 0x445566, 2.2));
    const sun = new THREE.DirectionalLight(0xffffff, 1.6);
    sun.position.set(60, 140, 90);
    this.scene.add(sun);

    afterNextRender(() => this.start());
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
  }

  protected startDefining(): void {
    this.defining.set(true);
    this.settled.set([]);
    this.saveError.set(null);
  }

  /** Edits a factory as a new one is defined: its machines chosen, its name filled in. */
  protected startEditing(factory: OutlinedFactory): void {
    const machines = this.factories.value()?.find((trace) => trace.factoryId === factory.factoryId)?.machines ?? [];
    this.editing.set(factory.factoryId);
    this.chosen.set(new Set(machines.map((machine) => machine.id)));
    this.factoryName.set(factory.name);
    this.settled.set([]);
    this.saveError.set(null);
    this.defining.set(true);
  }

  /** After asking; then the factories again, without it. */
  protected async deleteFactory(factory: OutlinedFactory): Promise<void> {
    if (!(await this.factoryDeletion.confirmAndDelete(factory))) return;
    if (this.editing() === factory.factoryId) this.stopDefining();
    this.factories.reload();
  }

  protected stopDefining(): void {
    this.defining.set(false);
    this.editing.set(null);
    this.chosen.set(new Set());
    this.settled.set([]);
    this.factoryName.set('');
    this.saveError.set(null);
  }

  /** Stores the factory, new or edited, in the snapshot's world, then opens its page. */
  protected async save(): Promise<void> {
    const snapshot = this.snapshot.value();
    if (!snapshot || !this.canSave()) return;
    this.saving.set(true);
    this.saveError.set(null);
    try {
      const name = this.factoryName().trim();
      const machines = [...this.chosen()];
      const editing = this.editing();
      const factory = editing
        ? await this.api.invoke(editFactory, { id: editing, body: { name, machines } })
        : await this.api.invoke(defineFactory, { body: { world: snapshot.sessionName, name, machines } });
      await this.router.navigate(['/snapshots', this.id(), 'factories', factory.id]);
    } catch (error) {
      this.saveError.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  /** What the panel says about why the selected machine runs as it does. */
  protected why(finding: LimitFacts): string {
    return limitText(finding, (item) => this.names.item(item));
  }

  protected statusLabel(finding: { limit: MachineStatus }): string {
    return statusLabels[finding.limit];
  }

  protected contents(contents: Record<string, number> | null | undefined): string {
    return describeContents(contents, (item) => this.names.item(item));
  }

  private start(): void {
    const stage = this.stage().nativeElement;
    this.renderer = new THREE.WebGLRenderer({ antialias: true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.localClippingEnabled = true;
    stage.prepend(this.renderer.domElement);
    this.camera = new THREE.PerspectiveCamera(45, 1, 0.5, 20000);
    this.controls = new OrbitControls(this.camera, this.renderer.domElement);
    this.controls.screenSpacePanning = true;
    this.scene.background = new THREE.Color(getComputedStyle(stage).getPropertyValue('--scene').trim() || '#1b1f24');

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

  /** A line material per factory colour, for the edges of its machines. */
  private readonly outlineMaterials = new Map<string, THREE.LineBasicMaterial>();

  private outlineMaterial(colour: string): THREE.LineBasicMaterial {
    let material = this.outlineMaterials.get(colour);
    if (!material) {
      material = new THREE.LineBasicMaterial({ color: colour });
      this.outlineMaterials.set(colour, material);
    }
    return material;
  }

  /** A material per colour; belts and pipes get their own, cut off by the clipping plane. */
  private material(colour: string, clipped = false): THREE.MeshLambertMaterial {
    const key = `${colour}${clipped ? ':clipped' : ''}`;
    let material = this.materials.get(key);
    if (!material) {
      material = new THREE.MeshLambertMaterial({ color: colour, side: THREE.DoubleSide, clippingPlanes: clipped ? [this.clip] : [] });
      this.materials.set(key, material);
    }
    return material;
  }

  private draw(): void {
    const world = this.world.value();
    const buildings = this.buildings();
    const findings = this.findings();
    const owners = this.owners();
    if (!this.renderer || !world) return;

    this.content.clear();
    const area = this.area();
    const middle = { x: (area.x0 + area.x1) / 2, y: (area.y0 + area.y1) / 2 };
    const footprints = new Map(world.footprints.map((footprint) => [footprint.id, footprint] as [string, FootprintDocument]));
    const at = (p: Position) => new THREE.Vector3(...toScene(p, middle));

    for (const building of buildings) {
      const finding = findings.get(building.id);
      const colour = finding ? statusColours[finding.limit] : building.kind === 'machine' ? statusColours.unknown : building.kind === 'consumer' ? '#ab47bc' : '#78909c';
      const boxes = buildingBoxes(building, footprints);
      const geometry = mergeGeometries(boxes.map((box) => this.boxGeometry(box, at)));
      const material = this.material(colour);
      const mesh = new THREE.Mesh(geometry, material);
      const top = Math.max(...boxes.flat().map((corner) => corner.z)) / 100;
      mesh.userData = { building, material, z: building.position.z / 100, top };
      // A factory's machines are outlined in its colour. As a child of the mesh, the outline hides with it.
      const owner = owners.get(building.id)?.[0];
      if (owner) mesh.add(new THREE.LineSegments(new THREE.EdgesGeometry(geometry), this.outlineMaterial(owner.colour)));
      this.content.add(mesh);
    }

    // Belts and pipes, merged per kind and per metre of their lowest point: thousands of meshes would slow a big area
    // down, and one mesh per kind couldn't follow the height cut-off.
    const tubes = new Map<string, THREE.BufferGeometry[]>();
    for (const conveyor of world.conveyors) {
      if (conveyor.curve.length < 2 || !curveInArea(conveyor.curve, area)) continue;
      const sampled = sampleCurve3d(conveyor.curve);
      const base = Math.floor(Math.min(...sampled.map((p) => p.z)) / 100);
      const points = sampled.map(at);
      const tube = new THREE.TubeGeometry(new THREE.CatmullRomCurve3(points), Math.max(2, points.length), conveyor.kind === 'pipe' ? 0.45 : 0.3, 5, false);
      const key = `${conveyor.kind}:${base}`;
      tubes.set(key, [...(tubes.get(key) ?? []), tube]);
    }
    for (const [key, geometries] of tubes) {
      const [kind, base] = key.split(':') as ['belt' | 'pipe', string];
      const mesh = new THREE.Mesh(mergeGeometries(geometries), this.material(conveyorColours[kind], true));
      mesh.userData = { z: Number(base) };
      this.content.add(mesh);
    }

    if (!this.framed && buildings.length) {
      this.frame();
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
    const preview = this.preview.value();
    const world = this.world.value();
    this.previewLayer.clear();
    if (!preview || !world) return;

    const area = this.area();
    const middle = { x: (area.x0 + area.x1) / 2, y: (area.y0 + area.y1) / 2 };
    const at = (p: Position) => new THREE.Vector3(...toScene(p, middle));
    const owned = new Set(preview.logistics);
    const tubes: THREE.BufferGeometry[] = [];
    for (const conveyor of world.conveyors) {
      if (!owned.has(conveyor.id) || conveyor.curve.length < 2 || !curveInArea(conveyor.curve, area)) continue;
      const points = sampleCurve3d(conveyor.curve).map(at);
      tubes.push(new THREE.TubeGeometry(new THREE.CatmullRomCurve3(points), Math.max(2, points.length), conveyor.kind === 'pipe' ? 0.55 : 0.4, 5, false));
    }
    if (tubes.length > 0) this.previewLayer.add(new THREE.Mesh(mergeGeometries(tubes), this.ownedMaterial));

    const conveyors = new Map(world.conveyors.map((conveyor) => [conveyor.id, conveyor]));
    const positions = new Map(this.buildings().map((building) => [building.id, building.position]));
    const lines = [
      [preview.outputs, 'output', this.outputMarker],
      [preview.inputs, 'input', this.inputMarker],
    ] as const;
    for (const [connections, direction, material] of lines) {
      for (const connection of connections) {
        const point = handoverPoint(connection, direction, (id) => conveyors.get(id), (id) => positions.get(id));
        if (!point || !inArea(point, area)) continue;
        const marker = new THREE.Mesh(this.markerGeometry, material);
        marker.position.copy(at(point));
        this.previewLayer.add(marker);
      }
    }
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
      if (object.visible && object.userData['building']) roof = Math.max(roof, object.userData['top'] as number);
    }
    this.clip.constant = limit === null ? Infinity : roof;
  }

  private boxGeometry(box: Box, at: (p: Position) => THREE.Vector3): THREE.BufferGeometry {
    const corners = box.map(at);
    const positions: number[] = [];
    for (const [a, b, c, d] of SIDES) for (const k of [a, b, c, a, c, d]) positions.push(corners[k].x, corners[k].y, corners[k].z);
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
    geometry.computeVertexNormals();
    return geometry;
  }

  /** Looks at the whole area from the south-east, a little from above. */
  private frame(): void {
    const bounds = new THREE.Box3().setFromObject(this.content);
    const middle = bounds.getCenter(new THREE.Vector3());
    const size = bounds.getSize(new THREE.Vector3());
    const reach = Math.max(size.x, size.y, size.z, 20);
    this.camera!.position.copy(middle).add(new THREE.Vector3(1, 0.75, 1.15).normalize().multiplyScalar(reach * 1.3));
    this.controls!.target.copy(middle);
  }

  /**
   * A click without a drag selects the building under the pointer; a click on nothing clears the selection. While
   * defining a factory, a click on a machine or generator also adds it or takes it out.
   */
  private listenForClicks(canvas: HTMLCanvasElement): void {
    let down = { x: 0, y: 0 };
    const ray = new THREE.Raycaster();
    canvas.addEventListener('pointerdown', (event) => (down = { x: event.clientX, y: event.clientY }));
    canvas.addEventListener('pointerup', (event) => {
      if (Math.hypot(event.clientX - down.x, event.clientY - down.y) > 4) return;
      const rect = canvas.getBoundingClientRect();
      ray.setFromCamera(new THREE.Vector2(((event.clientX - rect.left) / rect.width) * 2 - 1, -((event.clientY - rect.top) / rect.height) * 2 + 1), this.camera!);
      const hit = ray.intersectObjects(this.content.children.filter((o) => o.visible && o.userData['building']), false)[0];
      const building = hit ? (hit.object.userData['building'] as AreaBuilding) : null;
      this.selected.set(building);
      if (this.defining() && choosable(building) && !this.blocked().has(building.id)) this.chosen.update((chosen) => toggle(chosen, building.id));
    });
  }

  /**
   * While defining a factory, a drag with shift or alt draws a box instead of turning the view: on release, every
   * machine shown whose middle lies inside it is added (shift) or taken out (alt). The stage listens in the capture
   * phase, so it hears the press before OrbitControls on the canvas does, and can switch the controls off in time.
   */
  private listenForBoxes(stage: HTMLElement): void {
    let start: ScreenPoint | null = null;
    let mode: 'add' | 'remove' = 'add';
    const at = (event: PointerEvent): ScreenPoint => {
      const rect = stage.getBoundingClientRect();
      return { x: event.clientX - rect.left, y: event.clientY - rect.top };
    };
    stage.addEventListener(
      'pointerdown',
      (event) => {
        if (!this.defining() || event.button !== 0 || !(event.shiftKey || event.altKey)) return;
        start = at(event);
        mode = event.altKey ? 'remove' : 'add';
        this.controls!.enabled = false;
        stage.setPointerCapture(event.pointerId);
        event.preventDefault();
      },
      { capture: true },
    );
    stage.addEventListener('pointermove', (event) => {
      if (start) this.dragBox.set(rectBetween(start, at(event)));
    });
    stage.addEventListener('pointerup', (event) => {
      if (!start) return;
      const box = rectBetween(start, at(event));
      start = null;
      this.dragBox.set(null);
      this.controls!.enabled = true;
      stage.releasePointerCapture(event.pointerId);
      this.chosen.update((chosen) => boxSelect(chosen, this.machinesOnScreen(stage), box, mode));
    });
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
        return [{ id: (o.userData['building'] as AreaBuilding).id, x: ((middle.x + 1) / 2) * width, y: ((1 - middle.y) / 2) * height }];
      });
  }

  /** The building looked at glows a little, so it can be found again after turning the view; chosen machines are blue. */
  private readonly highlightMaterial = new THREE.MeshLambertMaterial({ color: '#ffffff', emissive: '#ffd54f', emissiveIntensity: 0.6, side: THREE.DoubleSide });
  private readonly chosenMaterial = new THREE.MeshLambertMaterial({ color: '#42a5f5', emissive: '#1565c0', emissiveIntensity: 0.5, side: THREE.DoubleSide });
  /** The preview: what the factory would own in teal, its outputs green and its inputs orange. */
  private readonly ownedMaterial = new THREE.MeshLambertMaterial({ color: '#26a69a', emissive: '#00695c', emissiveIntensity: 0.5, side: THREE.DoubleSide, clippingPlanes: [this.clip] });
  private readonly outputMarker = new THREE.MeshLambertMaterial({ color: '#66bb6a', emissive: '#2e7d32', emissiveIntensity: 0.6, clippingPlanes: [this.clip] });
  private readonly inputMarker = new THREE.MeshLambertMaterial({ color: '#ffa726', emissive: '#e65100', emissiveIntensity: 0.6, clippingPlanes: [this.clip] });
  private readonly markerGeometry = new THREE.SphereGeometry(1.2, 12, 8);

  private paint(selectedId: string | null, chosen: ReadonlySet<string>, owned: ReadonlySet<string>): void {
    for (const object of this.content.children) {
      const building = object.userData['building'] as AreaBuilding | undefined;
      if (!building) continue;
      (object as THREE.Mesh).material =
        building.id === selectedId ? this.highlightMaterial
        : chosen.has(building.id) ? this.chosenMaterial
        : owned.has(building.id) ? this.ownedMaterial
        : (object.userData['material'] as THREE.Material);
    }
  }
}
