import {
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

import { ArrangedMachineResponse } from '../../api/models/arranged-machine-response';
import { ArrangementMachinesResponse } from '../../api/models/arrangement-machines-response';
import { ArrangementResponse } from '../../api/models/arrangement-response';
import { RoutedBeltResponse } from '../../api/models/routed-belt-response';
import { WorldPoint } from '../../api/models/world-point';
import { Position } from '../../shared/display';
import { buildingBoxes } from '../area/area-model';
import { boxGeometry } from '../area/box-geometry';
import { FlowArrow } from '../stage/flow-arrow';
import { Stage } from '../stage/stage';
import { CatalogNames } from '../../shared/catalog-names';
import {
  beltStretches3d,
  beltTooltip,
  factoryTooltip,
  floorDistance,
  joinedWays,
  levelShown,
  jointArrows,
  lineJoints,
  crossingMarks,
  placedMachines,
  shownFactories,
  movedMetres,
  relatedFactories,
  suppliedBelts,
  jointColours,
  SourceKind,
  spotMark,
  wayMark,
  wayHeight,
} from './arrangement-model';

/** A belt as a flat band, in metres; a pipe round and a little wider, as in the game. */
const beltWidth = 1.2;
const beltThickness = 0.25;
const pipeRadius = 0.7;

/**
 * The belt floor of a run in 3D (decided 2026-11-18): the floor, the proposed factories on their plots at the ceiling,
 * and every routed belt and pipe at its heights, with its ramps and lifts. Drag to turn, scroll to zoom; pointing at a
 * belt or a factory tells what it is, as on the map.
 */
@Component({
  selector: 'app-belt-floor-3d',
  template: `
    <div #stage class="stage">
      @if (tip(); as tip) {
        <div
          class="tip"
          [style.left.px]="tip.x"
          [style.top.px]="tip.y"
          [innerHTML]="tip.html"
        ></div>
      }
    </div>
  `,
  styles: `
    /* As high as where it's put: the page puts it where the map is. */
    :host {
      display: block;
      height: 100%;
    }
    .stage {
      position: relative;
      height: 100%;
      border-radius: 4px;
      overflow: hidden;
      background: var(--scene, #1b1f24);
    }
    .tip {
      position: absolute;
      transform: translate(12px, 12px);
      pointer-events: none;
      padding: 4px 8px;
      border-radius: 4px;
      background: #fff;
      color: #222;
      font-size: 12px;
      white-space: nowrap;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.4);
    }
  `,
})
export class BeltFloor3d {
  readonly arrangement = input.required<ArrangementResponse>();

  /** A factory's colour, as on the map. */
  readonly colourOf = input.required<(factoryId: string) => string | undefined>();

  /** The factory a machine belongs to, with its colour: to mark a line's end at a machine, as the map does. */
  readonly ownerOf = input<(machine: string) => { name: string; colour: string } | undefined>(
    () => undefined,
  );

  /**
   * The levels shown, picked one by one (decided 2026-11-18): only their belts and pipes, the splitters and mergers on
   * them, the existing belts outside they join, and the factories they connect; null for every level and factory.
   */
  readonly levels = input<ReadonlySet<number> | null>(null);

  /** The factories' machines as they stand now, drawn on their slabs moved with them; none until they're fetched. */
  readonly machines = input<ArrangementMachinesResponse | null>(null);

  /** Whether the factories' machines show on their slabs; only the flat slabs when not. */
  readonly showMachines = input(true);

  /**
   * The factories picked, as on the map: the factories that don't feed one fade, and the belts that don't start or end
   * at one; none picked, nothing fades.
   */
  readonly focused = input<ReadonlySet<string>>(new Set());

  /** A factory clicked (not dragged across), to pick or unpick. */
  readonly picked = output<string>();

  /** What the pointer is on, and where: shown beside it. */
  protected readonly tip = signal<{ html: string; x: number; y: number } | null>(null);

  private readonly names = inject(CatalogNames);
  private readonly element = viewChild.required<ElementRef<HTMLElement>>('stage');
  /** Where the floor is drawn, once the view is on the page. */
  private stage?: Stage;
  private readonly content = new THREE.Group();
  /** The splitter or merger whose arrows show, its ways in and out (asked 2026-11-26); none when none is clicked. */
  private arrowsOf: THREE.Object3D | null = null;
  private readonly materials = new Map<string, THREE.Material>();

  constructor() {
    afterNextRender(() => this.start());
    inject(DestroyRef).onDestroy(() => this.stop());
    effect(() => {
      const arrangement = this.arrangement();
      const colourOf = this.colourOf();
      const machines = this.machines();
      this.ownerOf();
      if (this.stage) this.draw(arrangement, colourOf, machines);
    });
    effect(() => {
      const [arrangement, levels, machines, focused] = [
        this.arrangement(),
        this.levels(),
        this.showMachines(),
        this.focused(),
      ];
      if (this.stage) this.show(arrangement, levels, machines, focused);
    });
  }

  private start(): void {
    const stage = new Stage(this.element().nativeElement, { far: 50000 });
    this.stage = stage;
    const sun = new THREE.DirectionalLight('#ffffff', 1.6);
    sun.position.set(0.4, 1, 0.3);
    stage.scene.add(this.content, new THREE.AmbientLight('#ffffff', 1.2), sun);
    const canvas = stage.canvas;
    canvas.addEventListener('pointermove', (event) => this.point(event));
    // A finger leaves the canvas as it's lifted, right after a tap: the label it showed stays.
    canvas.addEventListener('pointerleave', (event) => {
      if (event.pointerType === 'mouse') this.tip.set(null);
    });
    // A click picks the factory under it; a drag turns the view and picks nothing. On a touch screen, where nothing
    // hovers, a tap also shows what it's on, as a tap on the map does (asked 2026-11-25); a tap on nothing closes it.
    stage.onPress((press) => {
      const hits = press.hits(this.content.children).filter((hit) => hit.object.visible);
      this.pick(hits);
      this.toggleArrows(stage, hits);
      if (press.pointerType !== 'mouse') this.tipAt(press, hits);
    });
    this.draw(this.arrangement(), this.colourOf(), this.machines());
  }

  private stop(): void {
    this.stage?.dispose();
    this.clear();
    this.materials.forEach((material) => material.dispose());
  }

  private clear(): void {
    this.content.traverse((object) => {
      (object as THREE.Mesh).geometry?.dispose();
      (object.userData['faded'] as THREE.Material | undefined)?.dispose();
    });
    this.content.clear();
  }

  private draw(
    arrangement: ArrangementResponse,
    colourOf: (factoryId: string) => string | undefined,
    machines: ArrangementMachinesResponse | null,
  ): void {
    this.clear();
    this.clearArrows();
    const { area, ceilingMetres } = arrangement.summary;
    const corners = arrangement.belts.flatMap((belt) => belt.corners);
    const bounds = area ?? {
      x0: Math.min(...corners.map((c) => c.x)),
      y0: Math.min(...corners.map((c) => c.y)),
      x1: Math.max(...corners.map((c) => c.x)),
      y1: Math.max(...corners.map((c) => c.y)),
    };
    const middle = { x: (bounds.x0 + bounds.x1) / 2, y: (bounds.y0 + bounds.y1) / 2 };

    // The floor, under the area: half see-through, so seen from below it hides nothing above it (asked 2026-11-25), and
    // drawn without hiding what's behind it, as the faded pieces are.
    const [width, depth] = [(bounds.x1 - bounds.x0) / 100, (bounds.y1 - bounds.y0) / 100];
    const floorMaterial = this.material('#3a4048', 0.5);
    floorMaterial.depthWrite = false;
    const floor = new THREE.Mesh(new THREE.BoxGeometry(width, 0.5, depth), floorMaterial);
    floor.position.set(0, -0.75, 0);
    this.content.add(floor);

    // The factories on their plots, as slabs at the ceiling.
    for (const factory of arrangement.factories) {
      const plot = factory.proposed;
      if (!plot) continue;
      const slab = new THREE.Mesh(
        new THREE.BoxGeometry((plot.x1 - plot.x0) / 100, 1, (plot.y1 - plot.y0) / 100),
        this.material(colourOf(factory.factoryId) ?? '#90a4ae', 0.35),
      );
      slab.userData['tooltip'] = factoryTooltip(
        factory.name ?? '(deleted)',
        movedMetres(factory.now, plot),
      );
      slab.position.set(
        ((plot.x0 + plot.x1) / 2 - middle.x) / 100,
        ceilingMetres + 0.5,
        ((plot.y0 + plot.y1) / 2 - middle.y) / 100,
      );
      slab.userData['factory'] = factory.factoryId;
      this.fadable(slab);
      this.content.add(slab);
    }

    // Each factory's machines on its slab, as the area view draws them: in the middle of its plot, each at its height
    // over the factory's lowest (decided 2026-11-18).
    const footprints = new Map(
      (machines?.footprints ?? []).map((footprint) => [footprint.id, footprint]),
    );
    const owned = new Map<string, ArrangedMachineResponse[]>();
    for (const machine of machines?.machines ?? [])
      owned.set(machine.factoryId, [...(owned.get(machine.factoryId) ?? []), machine]);
    for (const factory of arrangement.factories) {
      const [plot, standing] = [factory.proposed, owned.get(factory.factoryId) ?? []];
      if (!plot || standing.length === 0) continue;
      const lowest = Math.min(...standing.map((machine) => machine.position.z));
      const at = (p: Position) =>
        new THREE.Vector3(
          (p.x - middle.x) / 100,
          ceilingMetres + 1 + (p.z - lowest) / 100,
          (p.y - middle.y) / 100,
        );
      const geometries = placedMachines(standing, footprints, plot).flatMap((machine) =>
        buildingBoxes(machine, footprints).map((box) => boxGeometry(box, at)),
      );
      const mesh = new THREE.Mesh(
        mergeGeometries(geometries),
        this.material(colourOf(factory.factoryId) ?? '#90a4ae'),
      );
      geometries.forEach((geometry) => geometry.dispose());
      mesh.userData['tooltip'] = factoryTooltip(
        factory.name ?? '(deleted)',
        movedMetres(factory.now, plot),
      );
      mesh.userData['machines'] = true;
      mesh.userData['factory'] = factory.factoryId;
      this.fadable(mesh);
      this.content.add(mesh);
    }

    // Outside the area, the existing belts the routed ones join where they leave the floor (decided 2026-11-18): thin
    // lines in the colour of the factory they serve, at their heights as built from where they come in on the floor
    // (asked 2026-11-23); flat in runs from before heights were kept.
    const arranged = new Set(arrangement.factories.map((factory) => factory.factoryId));
    const spotNames = {
      item: (item: string) => this.names.item(item),
      building: (name: string) => this.names.building(name),
      extractorForm: (name: string) => this.names.extractorForm(name),
      fluidColour: (item: string) => this.names.fluidColour(item),
      ownerOf: this.ownerOf(),
    };
    for (const spot of arrangement.ways ?? [])
      for (const way of joinedWays([spot], arrangement.belts, arranged, spotNames)) {
        const line = new THREE.Line(
          new THREE.BufferGeometry().setFromPoints(
            [...way.points, ...way.links.flatMap((link) => [...[...link].reverse(), ...link])].map(
              (point) =>
                new THREE.Vector3(
                  (point.x - middle.x) / 100,
                  wayHeight(point, way.entry),
                  (point.y - middle.y) / 100,
                ),
            ),
          ),
          new THREE.LineBasicMaterial({ color: colourOf(way.factory) ?? '#e0e0e0' }),
        );
        const joining = way.joining;
        // What it is when pointed at, as on the map; found before the plots, as a belt. Lit with the belts joining it
        // (step 106).
        line.userData['tooltip'] = way.tooltip;
        line.userData['belt'] = true;
        line.userData['joins'] = joining;
        this.levelled(line, way.levels, [...new Set(joining.flatMap((belt) => belt.levels))]);
        this.content.add(line);

        // At a miner or extractor, its drum, as inside the area (asked 2026-11-19); elsewhere a dot (asked 2026-11-25).
        const mark = wayMark(spot, arrangement.belts, spotNames);
        if (mark) {
          const drum = this.mark(mark, middle);
          // At the height the way starts at, as built.
          drum.position.y += wayHeight(way.points[0] ?? way.entry, way.entry);
          drum.userData['joins'] = joining;
          this.levelled(drum, way.levels, [...new Set(joining.flatMap((belt) => belt.levels))]);
          this.content.add(drum);
        }
      }

    // The supply lines' smart splitters and priority mergers (decided 2026-11-18): on the belt, where it joins another
    // on the floor; else at the ceiling, where the belts go up into the factory. A priority merger orange, a smart
    // splitter blue; lit from within, so they stand out in any light.
    for (const belt of arrangement.belts)
      for (const joint of lineJoints([belt], (item) => this.names.item(item))) {
        const colour = jointColours[joint.kind];
        const cube = new THREE.Mesh(
          new THREE.BoxGeometry(2.5, 2, 2.5),
          new THREE.MeshBasicMaterial({ color: colour }),
        );
        cube.position.set(
          (joint.at.x - middle.x) / 100,
          joint.height === null ? ceilingMetres - 1 : joint.height + 0.5,
          (joint.at.y - middle.y) / 100,
        );
        // Pointing finds it before the slab above, as it does a belt.
        cube.userData['tooltip'] = joint.tooltip;
        cube.userData['belt'] = true;
        cube.userData['routed'] = belt;
        // Its ways in and out, shown when it's clicked: a merger where the belt ends, a splitter where it starts.
        cube.userData['arrows'] = jointArrows(
          belt,
          joint.kind === 'merger' ? 'end' : 'start',
          arrangement.belts,
        ).map((arrow) => ({
          ...arrow,
          at: new THREE.Vector3(
            (arrow.at.x - middle.x) / 100,
            cube.position.y,
            (arrow.at.y - middle.y) / 100,
          ),
        }));
        this.levelled(cube, [joint.level], belt.levels);
        this.content.add(cube);
      }

    // Where a belt crosses another as a last resort (step 102, decided 2026-11-25): an amber ball on the belt, lit from
    // within, as on the map.
    for (const belt of arrangement.belts)
      for (const crossing of crossingMarks([belt])) {
        const ball = new THREE.Mesh(
          new THREE.SphereGeometry(1.2, 12, 8),
          new THREE.MeshBasicMaterial({ color: '#ffb300' }),
        );
        ball.position.set(
          (crossing.at.x - middle.x) / 100,
          crossing.height + 1,
          (crossing.at.y - middle.y) / 100,
        );
        ball.userData['tooltip'] = crossing.tooltip;
        ball.userData['belt'] = true;
        ball.userData['routed'] = belt;
        this.levelled(ball, [crossing.level], belt.levels);
        this.content.add(ball);
      }

    // Where a line starts at a miner or an extractor (asked 2026-11-19): a drum on the ground at the foot of its lift, a
    // miner red, an extractor in the colour of its fluid; lit from within, as the splitters and mergers. Where it starts
    // or ends at a station, a drone port or machines, a dot in the colour the map gives it (asked 2026-11-25).
    for (const belt of arrangement.belts) {
      const mark = spotMark(belt, arranged, spotNames);
      if (!mark) continue;
      const [at, level] =
        mark.end === 'start'
          ? [belt.corners[0], belt.levels[0]]
          : [belt.corners[belt.corners.length - 1], belt.levels[belt.levels.length - 1]];
      if (!at) continue;
      const drum = this.mark({ ...mark, at }, middle);
      drum.userData['routed'] = belt;
      this.levelled(drum, [level ?? 0], belt.levels);
      this.content.add(drum);
    }

    // The belts and pipes, in the colour of the factory they serve: each straight stretch a piece of its own, a flat
    // box for a belt and a cylinder for a pipe, with a joint at every corner; one mesh per belt and level, so pointing
    // finds it and levels can be shown one by one.
    const factories = new Set(arrangement.factories.map((factory) => factory.factoryId));
    for (const belt of arrangement.belts) {
      const owner = factories.has(belt.to) ? belt.to : belt.from;
      const stretches = beltStretches3d(belt, middle, ceilingMetres, factories);
      const byLevel = new Map<number, THREE.BufferGeometry[]>();
      stretches.forEach(({ from, to, level }, i) => {
        const geometries = byLevel.get(level) ?? [];
        byLevel.set(level, geometries);
        const piece = this.stretch(new THREE.Vector3(...from), new THREE.Vector3(...to), belt.pipe);
        if (piece) geometries.push(piece);
        if (i < stretches.length - 1)
          geometries.push(this.joint(new THREE.Vector3(...to), belt.pipe));
      });
      const colour = colourOf(owner) ?? '#e0e0e0';
      const opacity = belt.pipe ? 0.75 : 1;
      for (const [level, geometries] of byLevel) {
        if (geometries.length === 0) continue;
        const mesh = new THREE.Mesh(mergeGeometries(geometries), this.material(colour, opacity));
        geometries.forEach((geometry) => geometry.dispose());
        mesh.userData['tooltip'] = beltTooltip(belt, (item) => this.names.item(item));
        mesh.userData['belt'] = true;
        mesh.userData['routed'] = belt;
        this.levelled(mesh, [level], belt.levels);
        this.content.add(mesh);
      }
    }

    this.frame();
    this.show(
      arrangement,
      untracked(this.levels),
      untracked(this.showMachines),
      untracked(this.focused),
    );
  }

  /**
   * What shows for the levels picked: the belts, pipes, splitters, mergers and existing belts outside on them; the
   * factories those connect (every one with every level); and the machines on them, unless they're switched off. With
   * factories picked, what doesn't feed them fades, as on the map.
   */
  private show(
    arrangement: ArrangementResponse,
    levels: ReadonlySet<number> | null,
    machines: boolean,
    focused: ReadonlySet<string>,
  ): void {
    const factories = shownFactories(levels, arrangement.belts);
    const related = relatedFactories(focused, arrangement.links);
    // The belts the factories picked stand out with: what supplies them, back to the source, and what they make.
    const supplied = suppliedBelts(arrangement.belts, focused);
    for (const object of this.content.children) {
      const { own, belt, routed, joins, factory, full, faded } = object.userData as {
        own?: number[];
        belt?: number[];
        routed?: RoutedBeltResponse;
        joins?: RoutedBeltResponse[];
        factory?: string;
        full?: THREE.Material;
        faded?: THREE.Material;
      };
      let fade = false;
      if (own && belt) {
        const shown = levelShown(levels, own, belt);
        object.visible = shown !== 'hidden';
        fade = shown === 'faded';
      }
      if (routed && !supplied.has(routed)) fade = true;
      if (joins && !joins.some((joined) => supplied.has(joined))) fade = true;
      if (factory) {
        object.visible =
          (factories === null || factories.has(factory)) &&
          (machines || !object.userData['machines']);
        if (related !== null && !related.has(factory)) fade = true;
      }
      if (full && faded) (object as THREE.Mesh).material = fade ? faded : full;
    }
  }

  /** A piece of a belt that shows with the levels picked: the levels it's on, and those its belt runs on. */
  private levelled(object: THREE.Mesh | THREE.Line, own: number[], belt: number[]): void {
    Object.assign(object.userData, { own, belt });
    this.fadable(object);
  }

  /** Something that can fade, for the levels or the factories picked: how it's drawn as it is, and faded. */
  private fadable(object: THREE.Mesh | THREE.Line): void {
    const full = object.material as THREE.Material;
    const faded = full.clone();
    faded.transparent = true;
    faded.opacity = full.opacity * 0.2;
    faded.depthWrite = false;
    Object.assign(object.userData, { full, faded });
  }

  /**
   * A click on a splitter or merger on the floor shows its ways in and out as arrows (asked 2026-11-26); a click on it
   * again, or anywhere else, lets go of them.
   */
  private toggleArrows(stage: Stage, hits: readonly THREE.Intersection[]): void {
    const hit = hits.find((candidate) => candidate.object.userData['arrows']);
    const clicked = hit && hit.object !== this.arrowsOf ? hit.object : null;
    this.arrowsOf = clicked;
    stage.showArrows(clicked ? (clicked.userData['arrows'] as FlowArrow[]) : []);
  }

  private clearArrows(): void {
    this.arrowsOf = null;
    this.stage?.showArrows([]);
  }

  /** The factory under a click, picked or unpicked: its slab or its machines, not a belt in front of it. */
  private pick(hits: readonly THREE.Intersection[]): void {
    const hit = hits.find((candidate) => candidate.object.userData['factory']);
    if (hit) this.picked.emit(hit.object.userData['factory'] as string);
  }

  /**
   * What the pointer is on: a belt or pipe before a factory, since the factories' slabs above would hide the belts
   * under them when looking down.
   */
  private point(event: PointerEvent): void {
    // Turning or moving the view: no tip in the way.
    if (event.buttons !== 0) {
      this.tip.set(null);
      return;
    }
    if (event.pointerType !== 'mouse' || !this.stage) return;
    const rect = this.stage.canvas.getBoundingClientRect();
    const hits = this.stage
      .hitsAt(event, this.content.children)
      .filter((hit) => hit.object.visible);
    this.tipAt({ x: event.clientX - rect.left, y: event.clientY - rect.top }, hits);
  }

  /** The label of what's under a pointer, beside it; none on nothing. */
  private tipAt(at: { x: number; y: number }, hits: readonly THREE.Intersection[]): void {
    const labelled = hits.filter((hit) => hit.object.userData['tooltip']);
    const hit = labelled.find((candidate) => candidate.object.userData['belt']) ?? labelled[0];
    this.tip.set(hit ? { html: hit.object.userData['tooltip'] as string, x: at.x, y: at.y } : null);
  }

  /**
   * A straight stretch of belt or pipe from one point to the next: a belt as a flat box, level across; a pipe as a
   * cylinder. None between two points that are the same.
   */
  /**
   * A miner's or extractor's drum on the ground, lit from within, saying what it is when pointed at; found before the
   * plots, as a belt.
   */
  /**
   * A line's end outside the arranged factories, on the ground: a miner's or extractor's drum, or a dot for a station,
   * a drone port or machines; lit from within, saying what it is when pointed at.
   */
  private mark(
    mark: { kind: SourceKind; at: WorldPoint; colour: string; tooltip: string },
    middle: { x: number; y: number },
  ): THREE.Mesh {
    const node = mark.kind === 'miner' || mark.kind === 'extractor';
    const shown = new THREE.Mesh(
      node ? new THREE.CylinderGeometry(2.5, 2.5, 1.5, 16) : new THREE.SphereGeometry(1.75, 16, 12),
      new THREE.MeshBasicMaterial({ color: mark.colour }),
    );
    shown.position.set(
      (mark.at.x - middle.x) / 100,
      node ? 0.75 : 1.75,
      (mark.at.y - middle.y) / 100,
    );
    shown.userData['tooltip'] = mark.tooltip;
    shown.userData['belt'] = true;
    return shown;
  }

  private stretch(
    from: THREE.Vector3,
    to: THREE.Vector3,
    pipe: boolean,
  ): THREE.BufferGeometry | null {
    const length = from.distanceTo(to);
    if (length === 0) return null;
    const geometry = pipe
      ? new THREE.CylinderGeometry(pipeRadius, pipeRadius, length, 8).rotateX(Math.PI / 2)
      : new THREE.BoxGeometry(beltWidth, beltThickness, length);
    const along = to.clone().sub(from).normalize();
    const turn = new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 0, 1), along);
    const middle = from.clone().add(to).multiplyScalar(0.5);
    return geometry.applyMatrix4(
      new THREE.Matrix4().compose(middle, turn, new THREE.Vector3(1, 1, 1)),
    );
  }

  /** What fills a corner, so the two stretches meet: a square of belt, a ball of pipe. */
  private joint(at: THREE.Vector3, pipe: boolean): THREE.BufferGeometry {
    const geometry = pipe
      ? new THREE.SphereGeometry(pipeRadius, 8, 6)
      : new THREE.BoxGeometry(beltWidth, beltThickness, beltWidth);
    return geometry.translate(at.x, at.y, at.z);
  }

  /** One material per colour and opacity, shared by everything drawn in it. */
  private material(colour: string, opacity = 1): THREE.Material {
    const key = `${colour}/${opacity}`;
    let material = this.materials.get(key);
    if (!material) {
      material = new THREE.MeshLambertMaterial({
        color: colour,
        transparent: opacity < 1,
        opacity,
      });
      this.materials.set(key, material);
    }
    return material;
  }

  /** Looks at the whole floor from the south-east, a little from above. */
  private frame(): void {
    this.stage?.frame(
      new THREE.Box3().setFromObject(this.content),
      new THREE.Vector3(1, 0.6, 1.15),
      (size, aspect) => floorDistance(Math.max(size.x, size.y, size.z, 20), aspect),
    );
  }
}
