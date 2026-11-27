import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';

import { FlowArrow, flowArrow } from './flow-arrow';
import { sceneBackground } from './scene-background';

/** How each 3D view wants its stage. */
export interface StageOptions {
  /** The camera's far plane, in metres: an area's is shorter than a whole floor's. */
  far: number;
  /** The canvas before what the element already holds, under its overlays; after it otherwise. */
  canvasFirst?: boolean;
  /** Whether materials may clip against planes: the area's height cut-off and its bounds. */
  clipping?: boolean;
}

/** What the stage uses of three.js's WebGLRenderer: the real one draws, a test's stands in for it (jsdom has no WebGL). */
export type StageRenderer = Pick<
  THREE.WebGLRenderer,
  'domElement' | 'localClippingEnabled' | 'setPixelRatio' | 'setSize' | 'render' | 'dispose'
> & { setAnimationLoop(loop: (() => void) | null): void };

/** A point on screen, in pixels from the canvas's top left. */
export interface ScreenPoint {
  x: number;
  y: number;
}

/** How far a pointer may move between pressing and letting go, in pixels, and still press rather than turn the view. */
export const PressReach = 4;

/** A click or a tap: a pointer pressed and let go without turning the view, where, by what, and what lies under it. */
export interface Press extends ScreenPoint {
  pointerType: string;
  /** Which of these lie under it, nearest first. */
  hits(among: readonly THREE.Object3D[]): THREE.Intersection[];
}

function webGl(): StageRenderer {
  return new THREE.WebGLRenderer({ antialias: true });
}

/**
 * Where a 3D view draws: the renderer at the size of its element, the camera turned by dragging, the scene drawn in a
 * loop. A view puts its content in `scene`, and disposes the stage with itself.
 */
export class Stage {
  readonly scene = new THREE.Scene();
  readonly canvas: HTMLCanvasElement;
  private readonly element: HTMLElement;
  private readonly renderer: StageRenderer;
  private readonly camera: THREE.PerspectiveCamera;
  private readonly controls: OrbitControls;
  private readonly resizing: ResizeObserver;
  private readonly ray = new THREE.Raycaster();
  /** A splitter's or merger's ways in and out, over the content (asked 2026-11-26). */
  private readonly arrows = Object.assign(new THREE.Group(), { name: 'arrows' });

  constructor(
    element: HTMLElement,
    options: StageOptions,
    makeRenderer: () => StageRenderer = webGl,
  ) {
    this.element = element;
    this.renderer = makeRenderer();
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.localClippingEnabled = options.clipping ?? false;
    this.canvas = this.renderer.domElement;
    // As big as the element. A view's stylesheet can't say so: three.js makes the canvas, so it lacks the attribute
    // Angular scopes the view's styles by. Unsized, it would show at its pixels' size: twice the element on a phone.
    Object.assign(this.canvas.style, { display: 'block', width: '100%', height: '100%' });
    if (options.canvasFirst) element.prepend(this.canvas);
    else element.append(this.canvas);

    this.camera = new THREE.PerspectiveCamera(45, 1, 0.5, options.far);
    this.controls = new OrbitControls(this.camera, this.canvas);
    this.controls.screenSpacePanning = true;
    // The element's own background, as the browser worked it out: the site's colours, never white (asked 2026-11-20).
    this.scene.background = new THREE.Color(
      sceneBackground(getComputedStyle(element).backgroundColor),
    );
    this.scene.add(this.arrows);

    const resize = () => {
      const { clientWidth: width, clientHeight: height } = element;
      this.renderer.setSize(width, height, false);
      this.camera.aspect = width / height;
      this.camera.updateProjectionMatrix();
    };
    this.resizing = new ResizeObserver(resize);
    this.resizing.observe(element);
    resize();

    this.renderer.setAnimationLoop(() => {
      this.controls.update();
      this.renderer.render(this.scene, this.camera);
    });
  }

  /** Whether a drag turns the view: the area's view stops turning while a box is drawn instead. */
  get orbiting(): boolean {
    return this.controls.enabled;
  }
  set orbiting(on: boolean) {
    this.controls.enabled = on;
  }

  /** Calls back on every press: a pointer let go within `PressReach` of where it was pressed. A drag turns the view. */
  onPress(handler: (press: Press) => void): void {
    let down: { x: number; y: number } | null = null;
    this.canvas.addEventListener('pointerdown', (event) => {
      down = { x: event.clientX, y: event.clientY };
    });
    this.canvas.addEventListener('pointerup', (event) => {
      const moved = down ? Math.hypot(event.clientX - down.x, event.clientY - down.y) : Infinity;
      down = null;
      if (moved > PressReach) return;
      const rect = this.canvas.getBoundingClientRect();
      handler({
        pointerType: event.pointerType,
        x: event.clientX - rect.left,
        y: event.clientY - rect.top,
        hits: (among) => this.hitsAt(event, among),
      });
    });
  }

  /** Which of these lie under the pointer, nearest first. */
  hitsAt(event: PointerEvent, among: readonly THREE.Object3D[]): THREE.Intersection[] {
    const rect = this.canvas.getBoundingClientRect();
    this.camera.updateMatrixWorld();
    this.ray.setFromCamera(
      new THREE.Vector2(
        ((event.clientX - rect.left) / rect.width) * 2 - 1,
        -((event.clientY - rect.top) / rect.height) * 2 + 1,
      ),
      this.camera,
    );
    return this.ray.intersectObjects([...among], false);
  }

  /**
   * Looks at the middle of a box from a side, as far back as `distance` says for its size on this screen: each view
   * has its own rule for that (the area's from a corner, the floor's whole).
   */
  frame(
    box: THREE.Box3,
    from: THREE.Vector3,
    distance: (size: THREE.Vector3, aspect: number) => number,
  ): void {
    const middle = box.getCenter(new THREE.Vector3());
    const size = box.getSize(new THREE.Vector3());
    this.camera.position
      .copy(middle)
      .add(from.clone().normalize().multiplyScalar(distance(size, this.camera.aspect)));
    this.controls.target.copy(middle);
    this.controls.update();
  }

  /** Where a point lies on screen, in pixels from the element's top left; null when it lies behind the camera. */
  onScreen(point: THREE.Vector3): ScreenPoint | null {
    this.camera.updateMatrixWorld();
    const projected = point.clone().project(this.camera);
    // Behind the camera: projecting goes through it, so the point would land on screen mirrored.
    if (projected.z > 1) return null;
    const { clientWidth: width, clientHeight: height } = this.element;
    return { x: ((projected.x + 1) / 2) * width, y: ((1 - projected.y) / 2) * height };
  }

  /** Shows these arrows in place of those shown before; none for an empty list. Their geometry is shared, never disposed. */
  showArrows(arrows: readonly FlowArrow[]): void {
    this.arrows.clear();
    for (const arrow of arrows) this.arrows.add(flowArrow(arrow.at, arrow.along, arrow.way));
  }

  dispose(): void {
    this.renderer.setAnimationLoop(null);
    this.resizing.disconnect();
    this.arrows.clear();
    this.controls.dispose();
    this.renderer.dispose();
  }
}
