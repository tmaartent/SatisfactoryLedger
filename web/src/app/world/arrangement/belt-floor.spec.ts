import { ArrangementResponse } from '../../api/models/arrangement-response';
import { RoutedBeltResponse } from '../../api/models/routed-belt-response';
import { beltFloor } from './belt-floor';

describe('what the belt floor shows, flat and in 3D', () => {
  const miner = 'Persistent_Level:PersistentLevel.Build_MinerMk3_C_2147327191';
  const station = 'Persistent_Level:PersistentLevel.Build_TrainDockingStation_C_7';
  const names = {
    item: (item: string) =>
      ({ Desc_OreIron_C: 'Iron Ore', Desc_IronIngot_C: 'Iron Ingot' })[item] ?? item,
    building: (className: string) =>
      ({ Build_MinerMk3_C: 'Miner Mk.3', Build_TrainDockingStation_C: 'Freight Platform' })[
        className
      ] ?? className,
    extractorForm: (className: string) =>
      className === 'Build_MinerMk3_C' ? ('solid' as const) : null,
    fluidColour: () => null,
    ownerOf: () => undefined,
  };
  const colours: Record<string, string> = { smelters: '#ff8a65', foundry: '#4db6ac' };
  const colourOf = (factoryId: string) => colours[factoryId];

  const belt = (fields: Partial<RoutedBeltResponse>): RoutedBeltResponse => ({
    from: miner,
    to: 'smelters',
    corners: [
      { x: 0, y: 0 },
      { x: 2000, y: 0 },
    ],
    heights: [2],
    levels: [0],
    lifts: [],
    item: 'Desc_OreIron_C',
    perMinute: 240,
    pipe: false,
    injection: false,
    overflow: false,
    startsOnBelt: false,
    endsOnBelt: false,
    leavesFloor: false,
    lastResortCrossings: [],
    ...fields,
  });
  // The miner feeds the smelters; the smelters feed the foundry.
  const ore = belt({});
  const ingots = belt({
    from: 'smelters',
    to: 'foundry',
    item: 'Desc_IronIngot_C',
    corners: [
      { x: 2000, y: 0 },
      { x: 2000, y: 3000 },
    ],
    levels: [1],
  });
  const arrangement = (fields: Partial<ArrangementResponse> = {}): ArrangementResponse => ({
    belts: [ore, ingots],
    factories: [
      {
        factoryId: 'smelters',
        name: 'Smelters',
        now: { x0: 0, y0: 0, x1: 1000, y1: 1000 },
        proposed: { x0: 0, y0: 0, x1: 1000, y1: 1000 },
      },
      {
        factoryId: 'foundry',
        name: 'Foundry',
        now: { x0: 0, y0: 0, x1: 1000, y1: 1000 },
        proposed: { x0: 3000, y0: 4000, x1: 4000, y1: 5000 },
      },
    ],
    fixed: [],
    links: [{ from: 'smelters', to: 'foundry', belts: 1 }],
    summary: {
      id: 'run-1',
      snapshotId: 'snapshot-1',
      world: 'Learning',
      requestedAt: '2026-11-25T10:00:00Z',
      computedAt: '2026-11-25T10:01:00Z',
      status: 'ready',
      quality: 'optimal',
      error: null,
      area: { x0: -1000, y0: -1000, x1: 5000, y1: 6000 },
      exclusions: [],
      plotWidthMetres: 48,
      plotDepthMetres: 48,
      roadXMetres: 32,
      roadYMetres: 32,
      ceilingMetres: 32,
      supplyLines: false,
      footprints: false,
      seconds: 60,
      spotWeight: 1,
      together: 0,
      duration: null,
      costNow: 1000,
      cost: 660,
      beltMetres: 50,
      levelsUsed: 2,
      unroutedBelts: 0,
      placements: 1,
      lastResortCrossings: 0,
    },
    unrouted: [],
    ways: [],
    ...fields,
  });
  const nothingPicked: ReadonlySet<string> = new Set();

  describe('the factories on their plots', () => {
    const floor = (focused: ReadonlySet<string>, levels: ReadonlySet<number> | null = null) =>
      beltFloor({ arrangement: arrangement(), colourOf, names, focused, levels }).factories;

    it('stand on the plot proposed, in their colour, saying how far they move', () => {
      expect(floor(nothingPicked)).toEqual([
        {
          factoryId: 'smelters',
          plot: { x0: 0, y0: 0, x1: 1000, y1: 1000 },
          colour: '#ff8a65',
          tooltip: '<b>Smelters</b><br>stays',
          standsOut: false,
          faded: false,
          hidden: false,
        },
        {
          factoryId: 'foundry',
          plot: { x0: 3000, y0: 4000, x1: 4000, y1: 5000 },
          colour: '#4db6ac',
          tooltip: '<b>Foundry</b><br>moves 50 m',
          standsOut: false,
          faded: false,
          hidden: false,
        },
      ]);
    });

    it('stand out when picked, with those that feed it; the rest fades', () => {
      expect(floor(new Set(['foundry'])).map(({ standsOut, faded }) => [standsOut, faded])).toEqual(
        [
          [false, false],
          [true, false],
        ],
      );
      expect(floor(new Set(['smelters'])).map(({ faded }) => faded)).toEqual([false, true]);
    });

    it('in 3D, show only where a belt on a level picked connects them', () => {
      // The ore runs on level 0, into the smelters; the ingots on level 1, on to the foundry.
      expect(floor(nothingPicked, new Set([0])).map(({ hidden }) => hidden)).toEqual([false, true]);
    });

    it('are left out without a plot proposed', () => {
      const unplaced = arrangement().factories.map((factory) => ({ ...factory, proposed: null }));
      expect(
        beltFloor({
          arrangement: arrangement({ factories: unplaced }),
          colourOf,
          names,
          focused: nothingPicked,
        }).factories,
      ).toEqual([]);
    });
  });

  describe('the routed belts', () => {
    it('are each in the colour of the factory they serve, saying what they carry and on which level', () => {
      const [first] = beltFloor({
        arrangement: arrangement(),
        colourOf,
        names,
        focused: nothingPicked,
      }).belts;

      expect(first).toMatchObject({
        belt: ore,
        colour: '#ff8a65',
        tooltip: 'Iron Ore · 240/min · level 0',
        standsOut: false,
        faded: false,
        hidden: false,
      });
    });

    it('stand out where they supply the factory picked, and the rest fades', () => {
      const belts = beltFloor({
        arrangement: arrangement(),
        colourOf,
        names,
        focused: new Set(['smelters']),
      }).belts;

      expect(belts.map(({ standsOut, faded }) => ({ standsOut, faded }))).toEqual([
        { standsOut: true, faded: false },
        { standsOut: true, faded: false },
      ]);
      const forTheMiner = beltFloor({
        arrangement: arrangement(),
        colourOf,
        names,
        focused: new Set(['foundry']),
      }).belts;
      expect(forTheMiner.map(({ standsOut, faded }) => ({ standsOut, faded }))).toEqual([
        { standsOut: false, faded: true },
        { standsOut: true, faded: false },
      ]);
    });

    it('between two factories, are flat in the colour of the one they come from and in 3D of the one they go to', () => {
      const [, between] = beltFloor({
        arrangement: arrangement(),
        colourOf,
        names,
        focused: nothingPicked,
      }).belts;

      expect([between.colour, between.colourIn3d]).toEqual(['#ff8a65', '#4db6ac']);
    });

    it('in 3D with levels picked, are hidden without a run on one, and faded on the levels not picked', () => {
      const climbing = belt({ levels: [0, 1] });
      const [low, high] = beltFloor({
        arrangement: arrangement({ belts: [climbing, ingots] }),
        colourOf,
        names,
        focused: nothingPicked,
        levels: new Set([0]),
      }).belts;

      expect([low.hidden, low.fadedOn(0), low.fadedOn(1)]).toEqual([false, false, true]);
      expect(high.hidden).toBe(true);
    });
  });

  describe('where a line starts or ends outside the arranged factories', () => {
    it('marks a miner red at the foot of its lift, saying what it is and what it sends', () => {
      const [fromMiner] = beltFloor({
        arrangement: arrangement(),
        colourOf,
        names,
        focused: nothingPicked,
      }).belts;

      expect(fromMiner.spot).toEqual({
        kind: 'miner',
        at: { x: 0, y: 0 },
        colour: '#e53935',
        tooltip: '<b>Miner Mk.3</b><br>Iron Ore · 240/min',
        faded: false,
        hidden: false,
      });
    });

    it('fades the mark with its belt, and in 3D on a level not picked', () => {
      const floor = (focused: ReadonlySet<string>, levels: ReadonlySet<number> | null) =>
        beltFloor({ arrangement: arrangement(), colourOf, names, focused, levels }).belts[0].spot;

      expect(floor(new Set(['foundry']), null)?.faded).toBe(true);
      expect(floor(nothingPicked, new Set([0]))?.faded).toBe(false);
      expect(
        beltFloor({
          arrangement: arrangement({ belts: [belt({ levels: [1, 0] }), ingots] }),
          colourOf,
          names,
          focused: nothingPicked,
          levels: new Set([0]),
        }).belts[0].spot?.faded,
      ).toBe(true);
    });

    it('marks nothing on a belt between two factories', () => {
      const [, between] = beltFloor({
        arrangement: arrangement(),
        colourOf,
        names,
        focused: nothingPicked,
      }).belts;

      expect(between.spot).toBeNull();
    });
  });

  describe('the existing belts outside the area that routed belts join', () => {
    // From the miner far west to where it comes into the area, at x = -1000; the belt leaves the floor at x = 0.
    const way = {
      spotId: miner,
      points: [
        { x: -6000, y: 0 },
        { x: -1000, y: 0 },
      ],
    };
    const joining = belt({ leavesFloor: true, levels: [1] });
    const floor = (focused: ReadonlySet<string>, levels: ReadonlySet<number> | null = null) =>
      beltFloor({
        arrangement: arrangement({ belts: [joining, ingots], ways: [way] }),
        colourOf,
        names,
        focused,
        levels,
      }).ways;

    it('come into the area in the colour of the factory they serve, with the building they start at marked', () => {
      expect(floor(nothingPicked)).toEqual([
        {
          points: way.points,
          entry: { x: -1000, y: 0 },
          links: [
            [
              { x: 0, y: 0 },
              { x: -1000, y: 0 },
            ],
          ],
          colour: '#ff8a65',
          tooltip: '<b>Existing belt</b> from Miner Mk.3<br>Iron Ore · 240/min',
          standsOut: false,
          faded: false,
          hidden: false,
          spot: {
            kind: 'miner',
            at: { x: -6000, y: 0 },
            colour: '#e53935',
            tooltip: '<b>Miner Mk.3</b><br>Iron Ore · 240/min',
          },
        },
      ]);
    });

    it('stand out with the belts joining them, and fade with them', () => {
      expect(
        floor(new Set(['smelters'])).map(({ standsOut, faded }) => [standsOut, faded]),
      ).toEqual([[true, false]]);
      expect(floor(new Set(['foundry'])).map(({ standsOut, faded }) => [standsOut, faded])).toEqual(
        [[false, true]],
      );
    });

    it('in 3D, show on the levels the belts joining them run on there', () => {
      expect(floor(nothingPicked, new Set([0])).map(({ hidden }) => hidden)).toEqual([true]);
      expect(floor(nothingPicked, new Set([1])).map(({ hidden }) => hidden)).toEqual([false]);
    });

    it('are left out where no routed belt joins them', () => {
      expect(
        beltFloor({
          arrangement: arrangement({ ways: [way] }),
          colourOf,
          names,
          focused: nothingPicked,
        }).ways,
      ).toEqual([]);
    });
  });

  describe("the supply lines' splitters and priority mergers", () => {
    // A top-up of ore ends on the line into the smelters, at its priority merger, on the floor.
    const line = belt({
      corners: [
        { x: 0, y: 0 },
        { x: 4000, y: 0 },
      ],
    });
    const topUp = belt({
      corners: [
        { x: 2000, y: 2000 },
        { x: 2000, y: 0 },
      ],
      heights: [4],
      levels: [1],
      injection: true,
      endsOnBelt: true,
    });
    const floor = (focused: ReadonlySet<string>, levels: ReadonlySet<number> | null = null) =>
      beltFloor({
        arrangement: arrangement({ belts: [line, topUp, ingots] }),
        colourOf,
        names,
        focused,
        levels,
      }).joints;

    it('stand where a top-up belt ends, orange, at its height, with the ways the items go', () => {
      expect(floor(nothingPicked)).toEqual([
        {
          kind: 'merger',
          at: { x: 2000, y: 0 },
          height: 4,
          colour: '#ff9800',
          tooltip: '<b>Priority merger</b><br>Iron Ore · 240/min · into a priority merger',
          arrows: [
            { at: { x: 2000, y: 400 }, along: { x: 0, y: -1 }, way: 'in' },
            { at: { x: 1600, y: 0 }, along: { x: 1, y: 0 }, way: 'in' },
            { at: { x: 2400, y: 0 }, along: { x: 1, y: 0 }, way: 'out' },
          ],
          faded: false,
          hidden: false,
        },
      ]);
    });

    it('fade with their belt, and in 3D are hidden without its run on a level picked', () => {
      expect(floor(new Set(['foundry']))[0].faded).toBe(true);
      expect(floor(nothingPicked, new Set([0]))[0].hidden).toBe(true);
    });
  });

  describe('the last-resort crossings', () => {
    const crossing = belt({
      corners: [
        { x: 0, y: 0 },
        { x: 2000, y: 0 },
        { x: 2000, y: 2000 },
      ],
      heights: [2, 6],
      levels: [0, 2],
      lastResortCrossings: [{ x: 2000, y: 1000 }],
    });
    const floor = (focused: ReadonlySet<string>, levels: ReadonlySet<number> | null = null) =>
      beltFloor({
        arrangement: arrangement({ belts: [crossing, ingots] }),
        colourOf,
        names,
        focused,
        levels,
      }).crossings;

    it('stand on the belt at its height there, saying it crosses another as a last resort', () => {
      expect(floor(nothingPicked)).toEqual([
        {
          at: { x: 2000, y: 1000 },
          height: 6,
          tooltip:
            '<b>Crosses another belt here</b><br>on another level: a last resort, where it could not get through otherwise',
          faded: false,
          hidden: false,
        },
      ]);
    });

    it('fade with their belt, and in 3D on a level not picked', () => {
      expect(floor(new Set(['foundry']))[0].faded).toBe(true);
      expect(floor(nothingPicked, new Set([0]))[0]).toMatchObject({ faded: true, hidden: false });
    });
  });

  describe('the lifts on the map', () => {
    it("stand at each end that doesn't join another belt: the spot's mark at a spot, the belt's colour at a factory", () => {
      const [fromMiner] = beltFloor({
        arrangement: arrangement(),
        colourOf,
        names,
        focused: nothingPicked,
      }).belts;

      expect(fromMiner.lifts).toEqual([
        { at: { x: 0, y: 0 }, colour: '#37474f', spot: fromMiner.spot, ring: false },
        { at: { x: 2000, y: 0 }, colour: '#ff8a65', spot: null, ring: false },
      ]);
    });

    it('are left out where a belt starts on another, at its splitter', () => {
      const [, between] = beltFloor({
        arrangement: arrangement({ belts: [ore, { ...ingots, startsOnBelt: true }] }),
        colourOf,
        names,
        focused: nothingPicked,
      }).belts;

      expect(between.lifts.map((lift) => lift.at)).toEqual([{ x: 2000, y: 3000 }]);
    });

    it('ring the end where a belt leaves the floor, when no existing belt is drawn coming in for it', () => {
      const leaving = belt({ from: station, leavesFloor: true });
      const floor = (ways: ArrangementResponse['ways']) =>
        beltFloor({
          arrangement: arrangement({ belts: [leaving], ways }),
          colourOf,
          names,
          focused: nothingPicked,
        }).belts[0].lifts[0];

      expect(floor([])).toEqual({ at: { x: 0, y: 0 }, colour: '#37474f', spot: null, ring: true });
      expect(floor([{ spotId: station, points: [] }]).ring).toBe(false);
    });
  });
});
