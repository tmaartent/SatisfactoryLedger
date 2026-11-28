import * as THREE from 'three';

import { Press, Stage, StageOptions, StageRenderer } from './stage';

/** A renderer without WebGL (jsdom has none): its canvas is real, the drawing isn't. */
class FakeRenderer implements StageRenderer {
  // OrbitControls captures the pointer it's pressed with; jsdom's canvas can't.
  readonly domElement = Object.assign(document.createElement('canvas'), {
    setPointerCapture: vi.fn(),
    releasePointerCapture: vi.fn(),
    hasPointerCapture: () => false,
  });
  localClippingEnabled = false;
  loop: (() => void) | null = null;
  readonly setPixelRatio = vi.fn();
  readonly setSize = vi.fn();
  readonly render = vi.fn();
  readonly dispose = vi.fn();
  setAnimationLoop(loop: (() => void) | null): void {
    this.loop = loop;
  }
}

function fakeRenderer() {
  return new FakeRenderer();
}

/** jsdom lays nothing out: the observers made, to call back as if their element were resized. */
let observers: { callback: () => void; disconnected: boolean }[] = [];

beforeEach(() => {
  observers = [];
  vi.stubGlobal(
    'ResizeObserver',
    class {
      private readonly entry: { callback: () => void; disconnected: boolean };
      constructor(callback: () => void) {
        this.entry = { callback, disconnected: false };
        observers.push(this.entry);
      }
      observe() {
        /* calls back when a test says so */
      }
      disconnect() {
        this.entry.disconnected = true;
      }
    },
  );
});
afterEach(() => vi.unstubAllGlobals());

/** An element of the size given, with one child already in it, and a stage on it. */
function stageOn(options: Partial<StageOptions> = {}, size = { width: 800, height: 600 }) {
  const element = document.createElement('div');
  element.append(document.createElement('span'));
  Object.defineProperty(element, 'clientWidth', { configurable: true, value: size.width });
  Object.defineProperty(element, 'clientHeight', { configurable: true, value: size.height });
  const renderer = fakeRenderer();
  const stage = new Stage(element, { far: 20000, ...options }, () => renderer);
  return { element, renderer, stage };
}

describe('a 3D stage', () => {
  it('puts its canvas after what the element holds, or before it when asked', () => {
    expect(stageOn().element.lastElementChild?.tagName).toBe('CANVAS');
    expect(stageOn({ canvasFirst: true }).element.firstElementChild?.tagName).toBe('CANVAS');
  });

  it('draws at the size of its element, and again when the element is resized', () => {
    const { element, renderer } = stageOn();
    expect(renderer.setSize).toHaveBeenLastCalledWith(800, 600, false);

    Object.defineProperty(element, 'clientWidth', { configurable: true, value: 400 });
    observers[0].callback();

    expect(renderer.setSize).toHaveBeenLastCalledWith(400, 600, false);
  });

  it('draws the scene in a loop until it is disposed, and lets go of everything then', () => {
    const { renderer, stage } = stageOn();
    renderer.loop?.();
    expect(renderer.render).toHaveBeenCalledWith(stage.scene, expect.any(THREE.Camera));

    stage.dispose();

    expect(renderer.loop).toBeNull();
    expect(renderer.dispose).toHaveBeenCalled();
    expect(observers[0].disconnected).toBe(true);
  });

  it('clips only when asked', () => {
    expect(stageOn().renderer.localClippingEnabled).toBe(false);
    expect(stageOn({ clipping: true }).renderer.localClippingEnabled).toBe(true);
  });
});

/** A pointer pressed or let go at a point on the page (jsdom may lack PointerEvent: a mouse event with its fields). */
function pointer(type: 'pointerdown' | 'pointerup', x: number, y: number, pointerType = 'mouse') {
  const event = new MouseEvent(type, { clientX: x, clientY: y, bubbles: true });
  return Object.assign(event, { pointerType, isPrimary: true });
}

/**
 * A box of a metre on the camera's axis, `distance` metres in front of it: the camera starts looking down -z. A little
 * off the axis, so the ray doesn't run along the diagonal its face is cut into triangles by, and hit both.
 */
function boxAhead(distance: number): THREE.Mesh {
  const box = new THREE.Mesh(new THREE.BoxGeometry(1, 1, 1));
  box.position.set(0.2, 0.1, -distance);
  box.updateMatrixWorld();
  return box;
}

describe('a press on a 3D stage', () => {
  function pressing() {
    const { stage } = stageOn();
    vi.spyOn(stage.canvas, 'getBoundingClientRect').mockReturnValue(new DOMRect(100, 50, 800, 600));
    const presses: Press[] = [];
    stage.onPress((press) => presses.push(press));
    const press = (from: [number, number], to: [number, number], pointerType = 'mouse') => {
      stage.canvas.dispatchEvent(pointer('pointerdown', ...from, pointerType));
      stage.canvas.dispatchEvent(pointer('pointerup', ...to, pointerType));
    };
    return { stage, presses, press };
  }

  it('finds what lies under it, nearest first', () => {
    const { presses, press } = pressing();
    const [near, far] = [boxAhead(10), boxAhead(20)];

    // The middle of the canvas, which stands 100 px from the page's left and 50 px from its top.
    press([500, 350], [500, 350]);

    expect(presses).toHaveLength(1);
    expect(presses[0].hits([far, near]).map((hit) => hit.object)).toEqual([near, far]);
    expect([presses[0].x, presses[0].y, presses[0].pointerType]).toEqual([400, 300, 'mouse']);
  });

  it('finds nothing beside what lies under it', () => {
    const { presses, press } = pressing();

    press([120, 60], [120, 60]);

    expect(presses[0].hits([boxAhead(10)])).toEqual([]);
  });

  it('is a press still when the pointer moved 4 pixels, not when it moved 5: then it turned the view', () => {
    const { presses, press } = pressing();

    press([500, 350], [504, 350], 'touch');
    press([500, 350], [505, 350]);

    expect(presses.map((press) => press.pointerType)).toEqual(['touch']);
  });
});

describe('framing on a 3D stage', () => {
  /** A box 2 metres each way around (10, 0, 0). */
  const box = new THREE.Box3(new THREE.Vector3(9, -1, -1), new THREE.Vector3(11, 1, 1));

  it('looks at the middle of what it frames, from the side and as far back as the view says', () => {
    const { stage } = stageOn();
    const distance = vi.fn(() => 5);

    stage.frame(box, new THREE.Vector3(0, 0, 2), distance);

    expect(distance).toHaveBeenCalledWith(new THREE.Vector3(2, 2, 2), 800 / 600);
    expect(stage.onScreen(new THREE.Vector3(10, 0, 0))).toEqual({ x: 400, y: 300 });
    // 5 metres back, along +z: a point a metre to its right at the box's middle lies right of the middle of the screen.
    expect(stage.onScreen(new THREE.Vector3(11, 0, 0))!.x).toBeGreaterThan(400);
  });

  it('puts nothing on screen that lies behind the camera', () => {
    const { stage } = stageOn();
    stage.frame(box, new THREE.Vector3(0, 0, 1), () => 5);

    expect(stage.onScreen(new THREE.Vector3(10, 0, 20))).toBeNull();
  });

  it('finds what it frames under a press in the middle of the screen', () => {
    const { stage } = stageOn();
    vi.spyOn(stage.canvas, 'getBoundingClientRect').mockReturnValue(new DOMRect(0, 0, 800, 600));
    const middle = new THREE.Mesh(new THREE.BoxGeometry(2, 2, 2));
    middle.position.set(10.3, 0.2, 0);
    middle.updateMatrixWorld();
    const presses: Press[] = [];
    stage.onPress((press) => presses.push(press));
    stage.frame(box, new THREE.Vector3(1, 0.75, 1.15), () => 30);

    stage.canvas.dispatchEvent(pointer('pointerdown', 400, 300));
    stage.canvas.dispatchEvent(pointer('pointerup', 400, 300));

    expect(presses[0].hits([middle]).map((hit) => hit.object)).toEqual([middle]);
  });
});

describe('arrows on a 3D stage', () => {
  /** Where the scene's arrows stand: each is drawn at its point, in the layer of arrows. */
  function arrowsIn(stage: Stage): { x: number; z: number }[] {
    const layer = stage.scene.getObjectByName('arrows');
    return (layer?.children ?? []).map(({ position }) => ({ x: position.x, z: position.z }));
  }
  const at = (x: number) => ({
    at: new THREE.Vector3(x, 0, 0),
    along: { x: 1, y: 0 },
    way: 'in' as const,
  });

  it("shows a splitter's ways in and out, in place of the arrows shown before", () => {
    const { stage } = stageOn();

    stage.showArrows([at(1), at(2)]);
    stage.showArrows([at(3)]);

    expect(arrowsIn(stage)).toEqual([{ x: 3, z: 0 }]);
  });

  it('shows none once none are given', () => {
    const { stage } = stageOn();
    stage.showArrows([at(1)]);

    stage.showArrows([]);

    expect(arrowsIn(stage)).toEqual([]);
  });
});
