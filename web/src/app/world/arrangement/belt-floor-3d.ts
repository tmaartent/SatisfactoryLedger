import {
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

import { ArrangementResponse } from '../../api/models/arrangement-response';
import { CatalogNames } from '../../shared/catalog-names';
import { beltStretches3d, beltTooltip, factoryTooltip, movedMetres } from './arrangement-model';

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
    :host {
      display: block;
      height: 560px;
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

  /**
   * The highest level shown, for building level by level: the levels above it hidden, the ones below faded as built;
   * null for all of them.
   */
  readonly upTo = input<number | null>(null);

  /** What the pointer is on, and where: shown beside it. */
  protected readonly tip = signal<{ html: string; x: number; y: number } | null>(null);

  private readonly names = inject(CatalogNames);
  private readonly pointer = new THREE.Raycaster();
  private readonly stage = viewChild.required<ElementRef<HTMLElement>>('stage');
  private readonly scene = new THREE.Scene();
  private readonly content = new THREE.Group();
  private readonly materials = new Map<string, THREE.Material>();
  private renderer?: THREE.WebGLRenderer;
  private camera?: THREE.PerspectiveCamera;
  private controls?: OrbitControls;

  constructor() {
    this.scene.add(this.content, new THREE.AmbientLight('#ffffff', 1.2));
    const sun = new THREE.DirectionalLight('#ffffff', 1.6);
    sun.position.set(0.4, 1, 0.3);
    this.scene.add(sun);
    afterNextRender(() => this.start());
    inject(DestroyRef).onDestroy(() => this.stop());
    effect(() => {
      const arrangement = this.arrangement();
      const colourOf = this.colourOf();
      if (this.renderer) this.draw(arrangement, colourOf);
    });
    effect(() => {
      const upTo = this.upTo();
      if (this.renderer) this.showLevels(upTo);
    });
  }

  private start(): void {
    const stage = this.stage().nativeElement;
    this.renderer = new THREE.WebGLRenderer({ antialias: true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    stage.append(this.renderer.domElement);
    this.camera = new THREE.PerspectiveCamera(45, 1, 0.5, 50000);
    this.controls = new OrbitControls(this.camera, this.renderer.domElement);
    this.controls.screenSpacePanning = true;
    this.scene.background = new THREE.Color(
      getComputedStyle(stage).getPropertyValue('--scene').trim() || '#1b1f24',
    );
    const resize = () => {
      const { clientWidth: w, clientHeight: h } = stage;
      this.renderer!.setSize(w, h, false);
      this.camera!.aspect = w / h;
      this.camera!.updateProjectionMatrix();
    };
    new ResizeObserver(resize).observe(stage);
    resize();
    this.renderer.domElement.addEventListener('pointermove', (event) => this.point(event));
    this.renderer.domElement.addEventListener('pointerleave', () => this.tip.set(null));
    this.renderer.setAnimationLoop(() => {
      this.controls!.update();
      this.renderer!.render(this.scene, this.camera!);
    });
    this.draw(this.arrangement(), this.colourOf());
  }

  private stop(): void {
    this.renderer?.setAnimationLoop(null);
    this.controls?.dispose();
    this.renderer?.dispose();
    this.clear();
    this.materials.forEach((material) => material.dispose());
  }

  private clear(): void {
    this.content.traverse((object) => (object as THREE.Mesh).geometry?.dispose());
    this.content.clear();
  }

  private draw(
    arrangement: ArrangementResponse,
    colourOf: (factoryId: string) => string | undefined,
  ): void {
    this.clear();
    const { area, ceilingMetres } = arrangement.summary;
    const corners = arrangement.belts.flatMap((belt) => belt.corners);
    const bounds = area ?? {
      x0: Math.min(...corners.map((c) => c.x)),
      y0: Math.min(...corners.map((c) => c.y)),
      x1: Math.max(...corners.map((c) => c.x)),
      y1: Math.max(...corners.map((c) => c.y)),
    };
    const middle = { x: (bounds.x0 + bounds.x1) / 2, y: (bounds.y0 + bounds.y1) / 2 };

    // The floor, under the area.
    const [width, depth] = [(bounds.x1 - bounds.x0) / 100, (bounds.y1 - bounds.y0) / 100];
    const floor = new THREE.Mesh(
      new THREE.BoxGeometry(width, 0.5, depth),
      this.material('#3a4048'),
    );
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
      this.content.add(slab);
    }

    // The belts and pipes, in the colour of the factory they serve: each straight stretch a piece of its own, a flat
    // box for a belt and a cylinder for a pipe, with a joint at every corner; one mesh per belt and level, so pointing
    // finds it and levels can be shown one by one.
    const factories = new Set(arrangement.factories.map((factory) => factory.factoryId));
    for (const belt of arrangement.belts) {
      const owner = factories.has(belt.to) ? belt.to : belt.from;
      const stretches = beltStretches3d(belt, middle, ceilingMetres);
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
        mesh.userData['level'] = level;
        mesh.userData['colour'] = colour;
        mesh.userData['opacity'] = opacity;
        this.content.add(mesh);
      }
    }

    this.frame();
    this.showLevels(untracked(this.upTo));
  }

  /** The levels up to one: those above hidden, those below faded, that one as drawn; all as drawn for null. */
  private showLevels(upTo: number | null): void {
    for (const object of this.content.children) {
      const { level, colour, opacity } = object.userData as {
        level?: number;
        colour: string;
        opacity: number;
      };
      if (level === undefined) continue;
      object.visible = upTo === null || level <= upTo;
      (object as THREE.Mesh).material = this.material(
        colour,
        upTo !== null && level < upTo ? opacity * 0.25 : opacity,
      );
    }
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
    const canvas = this.renderer!.domElement;
    const rect = canvas.getBoundingClientRect();
    const [x, y] = [event.clientX - rect.left, event.clientY - rect.top];
    this.pointer.setFromCamera(
      new THREE.Vector2((x / rect.width) * 2 - 1, -(y / rect.height) * 2 + 1),
      this.camera!,
    );
    const hits = this.pointer
      .intersectObjects(this.content.children, false)
      .filter((hit) => hit.object.visible && hit.object.userData['tooltip']);
    const hit = hits.find((candidate) => candidate.object.userData['belt']) ?? hits[0];
    this.tip.set(hit ? { html: hit.object.userData['tooltip'] as string, x, y } : null);
  }

  /**
   * A straight stretch of belt or pipe from one point to the next: a belt as a flat box, level across; a pipe as a
   * cylinder. None between two points that are the same.
   */
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
    const bounds = new THREE.Box3().setFromObject(this.content);
    const middle = bounds.getCenter(new THREE.Vector3());
    const size = bounds.getSize(new THREE.Vector3());
    const reach = Math.max(size.x, size.y, size.z, 20);
    this.camera!.position.copy(middle).add(
      new THREE.Vector3(1, 0.6, 1.15).normalize().multiplyScalar(reach * 0.9),
    );
    this.controls!.target.copy(middle);
  }
}
