import * as L from 'leaflet';
import { Area } from '../area/area-model';
import { AreaDrag, AreaSelection } from './area-selection';
import { removeMap } from './remove-map';

/** The maps a test put in the page, taken out after it. */
const maps: L.Map[] = [];

afterEach(() => {
  for (const map of maps.splice(0)) {
    const container = map.getContainer();
    removeMap(map);
    container.remove();
  }
});

/**
 * A map in the page at zoom 0, so a point on the screen is a metre on the map: (10, 20) on the screen is x 1000 cm,
 * y 2000 cm in the game's units.
 */
function mapOnPage(): L.Map {
  const container = document.createElement('div');
  document.body.append(container);
  const map = L.map(container, { crs: L.CRS.Simple }).setView([0, 0], 0);
  maps.push(map);
  return map;
}

describe('choosing an area with a mouse, by a drag with a key held', () => {
  /** A drag from one point on the screen to another, with shift held. */
  function drag(map: L.Map, from: [number, number], to: [number, number]): void {
    const container = map.getContainer();
    const at = (type: string, [x, y]: [number, number]) =>
      new MouseEvent(type, { bubbles: true, shiftKey: true, button: 0, clientX: x, clientY: y });
    container.dispatchEvent(
      new PointerEvent('pointerdown', { bubbles: true, shiftKey: true, button: 0 }),
    );
    container.dispatchEvent(at('mousedown', from));
    container.dispatchEvent(at('mousemove', to));
    container.dispatchEvent(at('mouseup', to));
    container.dispatchEvent(new PointerEvent('pointerup', { bubbles: true }));
  }

  /** The area chooser on a map: shift draws an area, nothing else does; what it chose, by kind. */
  function chooser(minimum: number) {
    const map = mapOnPage();
    const chosen: [string, Area][] = [];
    const areaDrag = new AreaDrag<'area'>({
      minimum,
      kindOf: (press) => (press.shiftKey ? 'area' : null),
      style: () => ({ color: '#ffeb3b' }),
      drawn: (kind, area) => chosen.push([kind, area]),
    });
    areaDrag.attach(map);
    return { map, chosen, areaDrag };
  }

  it("chooses the area dragged over, in the game's centimetres, whichever way it was dragged", () => {
    const { map, chosen } = chooser(200);

    drag(map, [15, 30], [10, 10]);

    expect(chosen).toEqual([['area', { x0: 1000, y0: 1000, x1: 1500, y1: 3000 }]]);
  });

  it('chooses nothing for a drag shorter than the minimum a side: it counts as a tap', () => {
    const { map, chosen } = chooser(1000);

    drag(map, [10, 10], [15, 30]);

    expect(chosen).toEqual([]);
  });

  it('chooses an area as long as the minimum a side', () => {
    const { map, chosen } = chooser(1000);

    drag(map, [10, 10], [20, 20]);

    expect(chosen).toEqual([['area', { x0: 1000, y0: 1000, x1: 2000, y1: 2000 }]]);
  });

  it('pans the map again once let go', () => {
    const { map, areaDrag } = chooser(200);

    drag(map, [10, 10], [15, 30]);

    expect(areaDrag.drawing()).toBeNull();
    expect(map.dragging.enabled()).toBe(true);
  });

  it('pans the map for a press without the key', () => {
    const { map, areaDrag } = chooser(200);

    map.getContainer().dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, button: 0 }));

    expect(areaDrag.drawing()).toBeNull();
    expect(map.dragging.enabled()).toBe(true);
  });

  it('chooses nothing for a drag without the key', () => {
    const { map, chosen } = chooser(200);
    const container = map.getContainer();

    container.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, button: 0 }));
    container.dispatchEvent(
      new MouseEvent('mousedown', { bubbles: true, button: 0, clientX: 10, clientY: 10 }),
    );
    container.dispatchEvent(
      new MouseEvent('mouseup', { bubbles: true, button: 0, clientX: 30, clientY: 30 }),
    );

    expect(chosen).toEqual([]);
  });
});

describe('choosing an area on a touch screen, by a drag while selecting', () => {
  const pointer = (type: string, [x, y]: [number, number]) =>
    new PointerEvent(type, { bubbles: true, isPrimary: true, button: 0, clientX: x, clientY: y });

  /** Selecting turned on, and a finger's drag from one point on the screen to another. */
  function select(minimum: number, from: [number, number], to: [number, number]): AreaSelection {
    const map = mapOnPage();
    const selection = new AreaSelection(minimum, { color: '#ffeb3b' });
    selection.attach(map);
    selection.toggle();
    const container = map.getContainer();
    container.dispatchEvent(pointer('pointerdown', from));
    container.dispatchEvent(pointer('pointermove', to));
    container.dispatchEvent(pointer('pointerup', to));
    return selection;
  }

  it("keeps the area drawn, in the game's centimetres, for the page to offer", () => {
    const selection = select(200, [15, 30], [10, 10]);

    expect(selection.drawn()).toEqual({ x0: 1000, y0: 1000, x1: 1500, y1: 3000 });
  });

  it('keeps nothing for a drag shorter than the minimum a side: it counts as a tap', () => {
    const selection = select(1000, [10, 10], [15, 30]);

    expect(selection.drawn()).toBeNull();
    expect(selection.selecting()).toBe(true);
  });
});
