import {
  areaBetween,
  beltEnds,
  beltLine,
  beltPath3d,
  beltStretches3d,
  carries,
  factoryTooltip,
  levelText,
  beltTooltip,
  ceilingHeights,
  ceilingMetres,
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

  it('gives the ceiling of the belt floor in foundation heights of 4 metres', () => {
    expect([ceilingMetres(8), ceilingHeights(32)]).toEqual([32, 8]);
  });

  it('draws a routed belt through its corners, as the map places buildings', () => {
    expect(
      beltLine([
        { x: 100, y: 200 },
        { x: 900, y: 200 },
        { x: 900, y: 1000 },
      ]),
    ).toEqual([
      [-2, 1],
      [-2, 9],
      [-10, 9],
    ]);
  });

  it('tells which end of a routed belt is a spot: a miner feeding a factory starts at its spot', () => {
    const factories = new Set(['smelting', 'plates']);
    expect([
      beltEnds({ from: 'smelting', to: 'plates' }, factories),
      beltEnds({ from: 'miner', to: 'smelting' }, factories),
      beltEnds({ from: 'plates', to: 'freight platform' }, factories),
    ]).toEqual([
      { factory: 'smelting', spot: null },
      { factory: 'smelting', spot: 'start' },
      { factory: 'plates', spot: 'end' },
    ]);
  });

  describe('a routed belt in 3D, in metres from the middle, height up', () => {
    const middle = { x: 0, y: 0 };
    const at = (x: number, y: number) => ({ x: x * 100, y: y * 100 });

    it('comes down its lift from the factory floor, runs at its height, and goes up its lift at the other end', () => {
      const belt = { corners: [at(0, 0), at(40, 0)], heights: [6], lifts: [] };

      expect(beltPath3d(belt, middle, 32)).toEqual([
        [0, 32, 0],
        [0, 6, 0],
        [40, 6, 0],
        [40, 32, 0],
      ]);
    });

    it('ramps 2 metres over the last 4 before it turns', () => {
      const belt = { corners: [at(0, 0), at(40, 0), at(40, 40)], heights: [0, 2], lifts: [] };

      expect(beltPath3d(belt, middle, 32).slice(2, 4)).toEqual([
        [36, 0, 0],
        [40, 2, 0],
      ]);
    });

    it('goes straight up or down where it turns on a lift', () => {
      const belt = {
        corners: [at(0, 0), at(40, 0), at(40, 40)],
        heights: [6, 0],
        lifts: [at(40, 0)],
      };

      expect(beltPath3d(belt, middle, 32).slice(2, 4)).toEqual([
        [40, 6, 0],
        [40, 0, 0],
      ]);
    });

    it('runs at the floor when its run is from before levels', () => {
      const belt = { corners: [at(0, 0), at(40, 0)], heights: [], lifts: [] };

      expect(beltPath3d(belt, middle, 32)[1]).toEqual([0, 0, 0]);
    });
  });

  describe('what a routed belt carries, as its tooltip says it', () => {
    const name = (item: string) =>
      ({ Desc_IronIngot_C: 'Iron Ingot', Desc_Water_C: 'Water' })[item] ?? item;

    it('names the item and its rate', () => {
      expect(carries({ item: 'Desc_IronIngot_C', perMinute: 12.5, pipe: false }, name)).toBe(
        'Iron Ingot · 12.5/min',
      );
    });

    it('says so for a pipe', () => {
      expect(carries({ item: 'Desc_Water_C', perMinute: 500, pipe: true }, name)).toBe(
        'Water · 500/min · pipe',
      );
    });

    it('says nothing for a run from before loads', () => {
      expect(carries({ item: null, perMinute: null, pipe: false }, name)).toBeNull();
    });
  });

  describe('what a proposed factory says when pointed at', () => {
    it('names it and how far it moves', () => {
      expect(factoryTooltip('Steel Beams', 160)).toBe('<b>Steel Beams</b><br>moves 160 m');
    });

    it('says when it stays', () => {
      expect(factoryTooltip('Steel Beams', 0)).toBe('<b>Steel Beams</b><br>stays');
    });
  });

  describe('the level a routed belt runs on, from 0 on the floor', () => {
    it('is one level when it keeps it all the way', () => {
      expect(levelText([2, 2, 2])).toBe('level 2');
    });

    it('is each level in turn where it changes', () => {
      expect(levelText([0, 0, 1, 3])).toBe('levels 0 → 1 → 3');
    });

    it('says nothing for a run from before levels', () => {
      expect(levelText([])).toBeNull();
    });
  });

  it('tells what a belt carries and its level when pointed at', () => {
    const name = () => 'Iron Ingot';
    expect(
      beltTooltip({ item: 'Desc_IronIngot_C', perMinute: 12.5, pipe: false, levels: [1, 1] }, name),
    ).toBe('Iron Ingot · 12.5/min · level 1');
  });

  describe('a routed belt in 3D in stretches, each on the level it belongs to', () => {
    const middle = { x: 0, y: 0 };
    const at = (x: number, y: number) => ({ x: x * 100, y: y * 100 });

    it('is on its level all the way when it keeps it, its lifts too', () => {
      const belt = { corners: [at(0, 0), at(40, 0)], heights: [2], levels: [1], lifts: [] };

      expect(beltStretches3d(belt, middle, 32).map((stretch) => stretch.level)).toEqual([1, 1, 1]);
    });

    it('puts a ramp on the lower level it joins, and what follows on the higher', () => {
      const belt = {
        corners: [at(0, 0), at(40, 0), at(40, 40)],
        heights: [0, 2],
        levels: [0, 1],
        lifts: [],
      };

      // Down its lift, along at 0, up the ramp, along at 1, up its lift.
      expect(beltStretches3d(belt, middle, 32).map((stretch) => stretch.level)).toEqual([
        0, 0, 0, 1, 1,
      ]);
    });

    it('is on the floor for a run from before levels', () => {
      const belt = { corners: [at(0, 0), at(40, 0)], heights: [], levels: [], lifts: [] };

      expect(beltStretches3d(belt, middle, 32).map((stretch) => stretch.level)).toEqual([0, 0, 0]);
    });
  });
});
