import {
  areaBetween,
  beltEnds,
  beltLine,
  beltPath3d,
  beltStretches3d,
  carries,
  factoryTooltip,
  joinedWays,
  levelText,
  lineJoints,
  beltTooltip,
  ceilingHeights,
  ceilingMetres,
  foundations,
  mapBounds,
  metres,
  placedMachines,
  movedMetres,
  saving,
  shownFactories,
  levelShown,
  solverTime,
  solverTimeOf,
  relatedFactories,
  standsOut,
  toggled,
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

  it('takes the factories picked and those that feed them as related, not those they feed; none with none picked', () => {
    const links = [
      { from: 'a', to: 'b' },
      { from: 'c', to: 'a' },
      { from: 'b', to: 'd' },
    ];
    expect([...relatedFactories(new Set(['a']), links)!].sort()).toEqual(['a', 'c']);
    expect(relatedFactories(new Set(), links)).toBeNull();
  });

  it('picks a factory not picked, and unpicks one picked', () => {
    expect([...toggled(new Set(['a']), 'b')]).toEqual(['a', 'b']);
    expect([...toggled(new Set(['a', 'b']), 'a')]).toEqual(['b']);
  });

  it('lets a belt stand out when one of its ends is picked, or nothing is', () => {
    expect([
      standsOut(new Set(['a']), ['a', 'b']),
      standsOut(new Set(['a']), ['b', 'c']),
      standsOut(new Set(), ['b', 'c']),
    ]).toEqual([true, false, true]);
  });

  it('gives the solver a quarter of a minute, a minute or ten minutes', () => {
    expect([solverTime('fast'), solverTime('balanced'), solverTime('thorough')]).toEqual([
      15, 60, 600,
    ]);
  });

  it("takes a run's own solver time as the longest of the three it reaches", () => {
    expect([solverTimeOf(30), solverTimeOf(60), solverTimeOf(300), solverTimeOf(600)]).toEqual([
      'fast',
      'balanced',
      'balanced',
      'thorough',
    ]);
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

    it("lifts a belt up only into a factory: to a miner, station or drone port, or the floor's edge it leaves by, it goes down to the ground", () => {
      const belt = {
        from: 'miner',
        to: 'smelters',
        corners: [
          { x: 0, y: 0 },
          { x: 1600, y: 0 },
        ],
        heights: [4],
        lifts: [],
      };

      expect(beltPath3d(belt, middle, 32, new Set(['smelters']))).toEqual([
        [0, 0, 0],
        [0, 4, 0],
        [16, 4, 0],
        [16, 32, 0],
      ]);
    });

    it('stops a belt that ends on another at its level there, at the merger: no lift up into the factory', () => {
      const belt = {
        corners: [
          { x: 0, y: 0 },
          { x: 1600, y: 0 },
        ],
        heights: [4],
        lifts: [],
        endsOnBelt: true,
      };

      expect(beltPath3d(belt, middle, 32)).toEqual([
        [0, 32, 0],
        [0, 4, 0],
        [16, 4, 0],
      ]);
    });

    it('starts a belt that leaves another at its level there, at the splitter: no lift down from a factory', () => {
      const belt = {
        corners: [
          { x: 0, y: 0 },
          { x: 1600, y: 0 },
        ],
        heights: [4],
        lifts: [],
        startsOnBelt: true,
      };

      expect(beltPath3d(belt, middle, 32)).toEqual([
        [0, 4, 0],
        [16, 4, 0],
        [16, 32, 0],
      ]);
    });
  });

  describe('what a routed belt carries, as its tooltip says it', () => {
    const name = (item: string) =>
      ({ Desc_IronIngot_C: 'Iron Ingot', Desc_Water_C: 'Water' })[item] ?? item;

    it('names the item and its rate', () => {
      expect(
        carries({ item: 'Desc_IronIngot_C', perMinute: 12.5, pipe: false, injection: false }, name),
      ).toBe('Iron Ingot · 12.5/min');
    });

    it('says so for a pipe', () => {
      expect(
        carries({ item: 'Desc_Water_C', perMinute: 500, pipe: true, injection: false }, name),
      ).toBe('Water · 500/min · pipe');
    });

    it('says when it tops up a supply line by a priority merger', () => {
      expect(
        carries({ item: 'Desc_IronIngot_C', perMinute: 300, pipe: false, injection: true }, name),
      ).toBe('Iron Ingot · 300/min · into a priority merger');
    });

    it('says nothing for a run from before loads', () => {
      expect(
        carries({ item: null, perMinute: null, pipe: false, injection: false }, name),
      ).toBeNull();
    });
  });

  describe('the machines of an arranged factory', () => {
    // A footprint 10 metres along its x and 2 across, from its position forward.
    const footprints = new Map([
      [
        'Build_SmelterMk1_C',
        { id: 'Build_SmelterMk1_C', minX: 0, maxX: 1000, minY: -100, maxY: 100 },
      ],
    ]);
    const plot = { x0: 0, y0: 0, x1: 4800, y1: 4800 };

    it('stand in the middle of the proposed plot, as a blueprint fills it, keeping their places to each other', () => {
      const machines = [
        { className: 'Build_SmelterMk1_C', position: { x: 10000, y: 20000, z: 900 }, yaw: 0 },
        { className: 'Build_SmelterMk1_C', position: { x: 12000, y: 20000, z: 900 }, yaw: 0 },
      ];

      // Around them: x from 10000 to 13000, y from 19900 to 20100; the plot's middle is (2400, 2400).
      expect(placedMachines(machines, footprints, plot).map((machine) => machine.position)).toEqual(
        [
          { x: 900, y: 2400, z: 900 },
          { x: 2900, y: 2400, z: 900 },
        ],
      );
    });

    it('count their footprints turned the way they face', () => {
      // Turned a quarter, the footprint runs along y: from 0 to 1000 there, its middle 500 past its position.
      const machines = [
        { className: 'Build_SmelterMk1_C', position: { x: 0, y: 0, z: 0 }, yaw: 90 },
      ];

      const [placed] = placedMachines(machines, footprints, plot);

      expect(placed.position.x).toBeCloseTo(2400);
      expect(placed.position.y).toBeCloseTo(1900);
      expect(placed.yaw).toBe(90);
    });
  });

  describe('the splitters and priority mergers of supply lines', () => {
    const name = (item: string) => ({ Desc_OreIron_C: 'Iron Ore' })[item] ?? item;
    const belt = {
      corners: [
        { x: 0, y: 0 },
        { x: 800, y: 0 },
        { x: 800, y: 1600 },
      ],
      heights: [2, 6],
      levels: [1, 3],
      item: 'Desc_OreIron_C',
      perMinute: 300,
      pipe: false,
      injection: false,
      overflow: false,
    };

    it('puts a priority merger where a top-up belt ends', () => {
      expect(lineJoints([{ ...belt, injection: true }], name)).toEqual([
        {
          kind: 'merger',
          at: { x: 800, y: 1600 },
          height: null,
          level: 3,
          tooltip: '<b>Priority merger</b><br>Iron Ore · 300/min · into a priority merger',
        },
      ]);
    });

    it("puts a smart splitter where a belt carrying a top-up's overflow on starts", () => {
      expect(lineJoints([{ ...belt, overflow: true }], name)).toEqual([
        {
          kind: 'smart-splitter',
          at: { x: 0, y: 0 },
          height: null,
          level: 1,
          tooltip: '<b>Smart splitter</b><br>the overflow of Iron Ore runs on: 300/min',
        },
      ]);
    });

    it('puts both on an overflow that tops up the next merger', () => {
      expect(
        lineJoints([{ ...belt, injection: true, overflow: true }], name).map((joint) => joint.kind),
      ).toEqual(['smart-splitter', 'merger']);
    });

    it("puts them on the floor, at the belt's height, where the belts join there", () => {
      const joints = lineJoints(
        [{ ...belt, injection: true, overflow: true, startsOnBelt: true, endsOnBelt: true }],
        name,
      );

      expect(joints.map((joint) => [joint.kind, joint.height])).toEqual([
        ['smart-splitter', 2],
        ['merger', 6],
      ]);
    });

    it('puts a splitter on the floor where a line runs on past a factory', () => {
      expect(lineJoints([{ ...belt, startsOnBelt: true }], name)).toEqual([
        {
          kind: 'splitter',
          at: { x: 0, y: 0 },
          height: 2,
          level: 1,
          tooltip: '<b>Splitter</b><br>the line of Iron Ore runs on: 300/min',
        },
      ]);
    });

    it('puts none on a belt along a line that runs on from the factory', () => {
      expect(lineJoints([belt], name)).toEqual([]);
    });
  });

  describe('the existing belts outside the area that routed belts join', () => {
    const factories = new Set(['smelters']);
    const miner = 'Persistent_Level:PersistentLevel.Build_MinerMk2_C_2147339296';
    const names = {
      item: (item: string) => ({ Desc_Stone_C: 'Limestone' })[item] ?? item,
      building: (className: string) => ({ Build_MinerMk2_C: 'Miner Mk.2' })[className] ?? className,
    };
    // From the miner far west to where it comes in at the area's edge, x = 0; the floor ends 10 m inside it.
    const way = {
      spotId: miner,
      points: [
        { x: -5000, y: 0 },
        { x: 0, y: 0 },
      ],
    };
    const belt = {
      from: miner,
      to: 'smelters',
      leavesFloor: true,
      corners: [
        { x: 1000, y: 0 },
        { x: 3000, y: 0 },
      ],
      levels: [2],
      item: 'Desc_Stone_C',
      perMinute: 600,
      pipe: false,
      injection: false,
    };

    it('come into the area where the belts joining them leave the floor, across the strip off the floor', () => {
      expect(joinedWays([way], [belt], factories, names)).toEqual([
        {
          points: way.points,
          factory: 'smelters',
          entry: { x: 0, y: 0 },
          links: [
            [
              { x: 1000, y: 0 },
              { x: 0, y: 0 },
            ],
          ],
          tooltip: '<b>Existing belt</b> from Miner Mk.2<br>Limestone · 600/min',
          levels: [2],
        },
      ]);
    });

    it("are reached across the strip off the floor at right angles: across it, then along the area's edge", () => {
      const beside = { ...belt, corners: [{ x: 1000, y: 800 }, ...belt.corners.slice(1)] };

      expect(joinedWays([way], [beside], factories, names)[0].links).toEqual([
        [
          { x: 1000, y: 800 },
          { x: 0, y: 800 },
          { x: 0, y: 0 },
        ],
      ]);
    });

    it('name how many buildings merge onto them', () => {
      const merged = `${miner}+2`;

      expect(
        joinedWays([{ ...way, spotId: merged }], [{ ...belt, from: merged }], factories, names)[0]
          .tooltip,
      ).toBe('<b>Existing belt</b> from Miner Mk.2 and 2 more<br>Limestone · 600/min');
    });

    it('leave out a way no routed belt joins', () => {
      expect(joinedWays([way], [{ ...belt, leavesFloor: false }], factories, names)).toEqual([]);
    });
  });

  describe('a piece of a belt in 3D for the levels picked', () => {
    it('is drawn as it is with every level', () => {
      expect(levelShown(null, [3], [3, 6])).toBe('full');
    });

    it('is drawn as it is on a level picked', () => {
      expect(levelShown(new Set([3]), [3], [3, 6])).toBe('full');
    });

    it('is faded on a level not picked, when its belt runs on one picked too: it shows how the belt connects', () => {
      expect(levelShown(new Set([3]), [6], [3, 6])).toBe('faded');
    });

    it('is hidden when its belt runs on no level picked', () => {
      expect(levelShown(new Set([1]), [3], [3, 6])).toBe('hidden');
    });
  });

  describe('the factories shown in 3D for the levels picked', () => {
    const belts = [
      { from: 'smelters', to: 'plates', levels: [0, 1] },
      { from: 'miner', to: 'smelters', levels: [2] },
      { from: 'wire', to: 'cable', levels: [0] },
    ];

    it('are every one with every level picked', () => {
      expect(shownFactories(null, belts)).toBeNull();
    });

    it('are those a belt on a picked level comes from or goes to', () => {
      expect([...shownFactories(new Set([1]), belts)!].sort()).toEqual(['plates', 'smelters']);
    });

    it('are none with no level picked', () => {
      expect(shownFactories(new Set(), belts)!.size).toBe(0);
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
      beltTooltip(
        {
          item: 'Desc_IronIngot_C',
          perMinute: 12.5,
          pipe: false,
          injection: false,
          levels: [1, 1],
        },
        name,
      ),
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

    it('puts a lift down to the ground at a station on the level of its belt there', () => {
      const belt = {
        from: 'station',
        to: 'smelters',
        corners: [at(0, 0), at(40, 0)],
        heights: [14],
        levels: [7],
        lifts: [],
      };

      // Down to the station, along at 7, up into the smelters.
      expect(
        beltStretches3d(belt, middle, 32, new Set(['smelters'])).map((stretch) => stretch.level),
      ).toEqual([7, 7, 7]);
    });

    it('is on the floor for a run from before levels', () => {
      const belt = { corners: [at(0, 0), at(40, 0)], heights: [], levels: [], lifts: [] };

      expect(beltStretches3d(belt, middle, 32).map((stretch) => stretch.level)).toEqual([0, 0, 0]);
    });
  });
});
