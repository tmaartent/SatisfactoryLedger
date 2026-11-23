import { holdToExtent } from './hold-to-extent';

describe('holding a map to the map picture', () => {
  function map(size = { x: 800, y: 600 }) {
    return {
      getSize: () => size,
      getBoundsZoom: vi.fn(() => -2.5),
      setMinZoom: vi.fn(),
      setMaxBounds: vi.fn(),
    };
  }
  const extent: [[number, number], [number, number]] = [
    [-10, -20],
    [10, 20],
  ];

  it('zooms out no further than the whole picture and moves no further than its edges', () => {
    const held = map();

    holdToExtent(held, extent, -4);

    expect(held.setMinZoom).toHaveBeenCalledWith(-2.5);
    expect(held.setMaxBounds).toHaveBeenCalledWith(
      expect.objectContaining({ _southWest: expect.anything() }),
    );
  });

  it('lets go without a picture: the whole world can be seen, down to the least zoom', () => {
    const held = map();

    holdToExtent(held, null, -4);

    expect(held.setMaxBounds).toHaveBeenCalledWith();
    expect(held.setMinZoom).toHaveBeenCalledWith(-4);
  });

  it('waits while the map has no size yet: the zoom that fits is known once it has', () => {
    const held = map({ x: 0, y: 0 });

    holdToExtent(held, extent, -4);

    expect(held.setMaxBounds).not.toHaveBeenCalled();
  });
});
