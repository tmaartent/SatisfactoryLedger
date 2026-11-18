import {
  areaBetween,
  foundations,
  mapBounds,
  metres,
  movedMetres,
  saving,
} from './arrangement-model';

describe('optimal arrangement', () => {
  const rectangle = (x0: number, y0: number, x1: number, y1: number) => ({ x0, y0, x1, y1 });

  it('places a rectangle on the map as the map places buildings: metres, north up', () => {
    expect(mapBounds(rectangle(100, 200, 900, 1000))).toEqual([
      [-2, 1],
      [-10, 9],
    ]);
  });

  it('measures how far a factory moves between the centres, in metres', () => {
    expect(movedMetres(rectangle(0, 0, 1000, 1000), rectangle(3000, 4000, 4000, 5000))).toBe(50);
  });

  it('gives plots and roads in foundations of 8 metres', () => {
    expect(metres(6)).toBe(48);
  });

  it('shows a size in metres as foundations', () => {
    expect(foundations(32)).toBe(4);
  });

  it('says the belt work saved as a share of what it is now', () => {
    expect(saving(22016, 14580)).toBe('−34%');
  });

  it('says nothing is saved when nothing was to save', () => {
    expect(saving(0, 0)).toBe('—');
  });

  it('makes the area to arrange from two corners dragged on the map, whichever way', () => {
    // Map points are [-y, x] in metres: from (x 30 m, y 10 m) to (x 10 m, y 40 m).
    expect(areaBetween([-10, 30], [-40, 10])).toEqual({ x0: 1000, y0: 1000, x1: 3000, y1: 4000 });
  });
});
