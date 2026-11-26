import { TraceBuildings, TraceStart, TraceStep } from './assistant';
import { whereItStarts } from './trace-starts';

const buildings = (
  building: string,
  ids: string[],
  extra: Partial<TraceBuildings> = {},
): TraceBuildings => ({ building, count: ids.length, ids, ...extra });
const short = (building: string, recipe: string, shortOf: string, ids: string[]): TraceStep => ({
  building,
  recipe,
  machines: ids.length,
  ids,
  shortOf,
  perMinute: 1,
});
const constructors = short('Constructor', 'Iron Plate', 'Iron Ingot', ['k1']);

describe('where a shortage starts, as the dialog lists it', () => {
  it('names the cause in words and the buildings by building and recipe, linked by their first id', () => {
    const start: TraceStart = {
      cause: 'tooFewProducers',
      causeTerm: 'too few producers',
      item: 'Dark Matter Residue',
      perMinute: 531.25,
      at: [buildings('Converter', ['c1', 'c2'], { recipe: 'Dark Matter Residue' })],
      path: [short('Quantum Encoder', 'Superposition Oscillator', 'Dark Matter Residue', ['q1'])],
    };

    expect(whereItStarts([start])).toEqual([
      {
        cause: 'Too few producers',
        item: 'Dark Matter Residue',
        perMinute: 531.25,
        atLabel: 'Made at full speed by',
        at: [
          {
            name: 'Converter (Dark Matter Residue)',
            building: 'Converter',
            count: 2,
            perMinute: null,
            focus: 'c1',
          },
        ],
        heldLabel: 'Short of it',
        held: [
          {
            name: 'Quantum Encoder (Superposition Oscillator)',
            building: 'Quantum Encoder',
            count: 1,
            perMinute: null,
            focus: 'q1',
          },
        ],
        via: null,
        othersLabel: null,
        others: [],
        needs: null,
      },
    ]);
  });

  it('puts starts with the same cause and item on one row, counting each machine once', () => {
    const platform = (id: string): TraceStart => ({
      cause: 'unknownSupply',
      causeTerm: 'unknown supply',
      item: 'AI Limiter',
      perMinute: 0,
      at: [buildings('Freight Platform', [id])],
      path: [constructors],
    });
    const again: TraceStart = { ...platform('p1') };

    const [row, ...rest] = whereItStarts([platform('p1'), platform('p2'), platform('p3'), again]);

    expect(rest).toEqual([]);
    expect(row.at).toEqual([
      {
        name: 'Freight Platform',
        building: 'Freight Platform',
        count: 3,
        perMinute: null,
        focus: 'p1',
      },
    ]);
    expect(row.perMinute).toBe(0);
  });

  it('counts a group by its count, as the trace gives only its first ten ids', () => {
    const tenIds = Array.from({ length: 10 }, (_, i) => `a${i}`);
    const rods = (): TraceStart => ({
      cause: 'tooFewProducers',
      causeTerm: 'too few producers',
      item: 'AI Limiter',
      perMinute: 4,
      at: [buildings('Assembler', tenIds, { recipe: 'Electromagnetic Control Rod', count: 20 })],
      path: [constructors],
    });

    const [row] = whereItStarts([rods(), rods()]);

    expect(row.at).toEqual([
      {
        name: 'Assembler (Electromagnetic Control Rod)',
        building: 'Assembler',
        count: 20,
        perMinute: null,
        focus: 'a0',
      },
    ]);
  });

  it('leaves out the rate of a row whose starts give different rates', () => {
    const start = (perMinute: number, id: string): TraceStart => ({
      cause: 'beltFull',
      causeTerm: 'bottleneck',
      item: 'Iron Ore',
      perMinute,
      at: [buildings('Conveyor Belt', [id])],
      path: [constructors],
    });

    expect(whereItStarts([start(780, 'b1'), start(480, 'b2')])[0].perMinute).toBeNull();
  });

  describe('what a bottleneck needs', () => {
    const full = (id: string, lines: number, carriesPerMinute: number): TraceStart => ({
      cause: 'beltFull',
      causeTerm: 'bottleneck',
      item: 'Quickwire',
      perMinute: 1200,
      at: [buildings('Conveyor Belt Mk.6', [id])],
      path: [constructors],
      moreCapacity: { tier: 'Conveyor Belt Mk.6', lines, carriesPerMinute },
    });

    it('says what carries all that is offered to it, in lines of the highest tier when one is not enough', () => {
      expect(whereItStarts([full('b1', 2, 1550)])[0].needs).toEqual({
        what: '2 lines of Conveyor Belt Mk.6 side by side',
        perMinute: 1550,
      });
    });

    it('names just the tier when one line of it is enough', () => {
      const upgrade: TraceStart = {
        ...full('b1', 1, 450),
        moreCapacity: { tier: 'Conveyor Belt Mk.4', lines: 1, carriesPerMinute: 450 },
      };

      expect(whereItStarts([upgrade])[0].needs).toEqual({
        what: 'Conveyor Belt Mk.4',
        perMinute: 450,
      });
    });

    it('keeps bottlenecks that need different things on rows of their own', () => {
      expect(
        whereItStarts([full('b1', 2, 1550), full('b2', 3, 2500)]).map(
          (row) => row.needs?.perMinute,
        ),
      ).toEqual([1550, 2500]);
    });

    it('needs nothing for other causes', () => {
      const start: TraceStart = {
        cause: 'takenByOthers',
        causeTerm: 'taken by others',
        item: 'Computer',
        perMinute: 50,
        at: [buildings('Assembler', ['a1'])],
        path: [constructors],
      };

      expect(whereItStarts([start])[0].needs).toBeNull();
    });
  });

  it("names the cause by the ledger's term, as the trace gives it", () => {
    const start: TraceStart = {
      cause: 'takenByOthers',
      causeTerm: 'taken by others',
      item: 'Computer',
      perMinute: 50,
      at: [buildings('Assembler', ['a1'])],
      path: [constructors],
    };

    expect(whereItStarts([start])[0].cause).toBe('Taken by others');
  });

  it('lists who else takes the item, with what they take', () => {
    const start: TraceStart = {
      cause: 'takenByOthers',
      causeTerm: 'taken by others',
      item: 'Crystal Oscillator',
      perMinute: 40.7718,
      at: [
        buildings('Manufacturer', ['m1'], { recipe: 'Alternate: Insulated Crystal Oscillator' }),
      ],
      others: [
        buildings('Assembler', ['a1', 'a2'], {
          recipe: 'Alternate: Crystal Computer',
          perMinute: 10.1917,
        }),
      ],
      path: [constructors],
    };

    const [row] = whereItStarts([start]);

    expect(row.othersLabel).toBe('Also taken by');
    expect(row.others).toEqual([
      {
        name: 'Assembler (Alternate: Crystal Computer)',
        building: 'Assembler',
        count: 2,
        perMinute: 10.1917,
        focus: 'a1',
      },
    ]);
  });

  describe('the way from the machines asked about to where it starts', () => {
    // Singularity Cells lack Nuclear Pasta, which lacks Pressure Conversion Cubes, which lack Radio Control Units, whose
    // manufacturer lacks Computers: the Crystal Computer assemblers make enough, but other machines get them first.
    const computers: TraceStart = {
      cause: 'takenByOthers',
      causeTerm: 'taken by others',
      item: 'Computer',
      perMinute: 50,
      at: [buildings('Assembler', ['a1'], { recipe: 'Alternate: Crystal Computer' })],
      others: [buildings('AWESOME Sink', ['s1'], { perMinute: 25 })],
      path: [
        short('Manufacturer', 'Singularity Cell', 'Nuclear Pasta', ['sc1', 'sc2']),
        short('Particle Accelerator', 'Nuclear Pasta', 'Pressure Conversion Cube', ['pa1']),
        short('Assembler', 'Pressure Conversion Cube', 'Radio Control Unit', ['pc1']),
        short('Manufacturer', 'Radio Control Unit', 'Computer', ['rc1']),
      ],
    };

    it('says which machines are short of the item, linked to the map', () => {
      const [row] = whereItStarts([computers]);

      expect(row.heldLabel).toBe('Short of it');
      expect(row.held).toEqual([
        {
          name: 'Manufacturer (Radio Control Unit)',
          building: 'Manufacturer',
          count: 1,
          perMinute: null,
          focus: 'rc1',
        },
      ]);
    });

    it('names the items on the way, so a start further up reads as part of the shortage asked about', () => {
      expect(whereItStarts([computers])[0].via).toBe(
        'Nuclear Pasta ← Pressure Conversion Cube ← Radio Control Unit',
      );
    });

    it('says what the buildings it starts at do with the item, beside the rate', () => {
      const [row] = whereItStarts([computers]);

      expect(row.atLabel).toBe('Sent by');
      expect(row.perMinute).toBe(50);
    });

    it('keeps starts with the same cause and item apart when they lie on different ways', () => {
      const direct: TraceStart = {
        ...computers,
        path: [short('Assembler', 'Supercomputer', 'Computer', ['sp1'])],
      };

      const rows = whereItStarts([computers, direct]);

      expect(rows.map((row) => row.via)).toEqual([
        'Nuclear Pasta ← Pressure Conversion Cube ← Radio Control Unit',
        null,
      ]);
    });

    it('counts a machine once when several starts on the same way name it', () => {
      const again: TraceStart = {
        ...computers,
        at: [buildings('Assembler', ['a2'], { recipe: 'Alternate: Crystal Computer' })],
      };

      const [row, ...rest] = whereItStarts([computers, again]);

      expect(rest).toEqual([]);
      expect(row.held).toEqual([
        {
          name: 'Manufacturer (Radio Control Unit)',
          building: 'Manufacturer',
          count: 1,
          perMinute: null,
          focus: 'rc1',
        },
      ]);
    });

    it('names the machines that cannot get the item away, and the way downstream, for a blockage', () => {
      const plates = (
        building: string,
        recipe: string,
        cannotLeave: string,
        id: string,
      ): TraceStep => ({ building, recipe, machines: 1, ids: [id], cannotLeave, perMinute: 4 });
      const blockage: TraceStart = {
        cause: 'tooFewConsumers',
        causeTerm: 'too few consumers',
        item: 'Reinforced Iron Plate',
        perMinute: 5,
        at: [buildings('Assembler', ['r1'], { recipe: 'Modular Frame' })],
        path: [
          plates('Constructor', 'Iron Plate', 'Iron Plate', 'k1'),
          plates('Assembler', 'Reinforced Iron Plate', 'Reinforced Iron Plate', 'a1'),
        ],
      };

      const [row] = whereItStarts([blockage]);

      expect(row.heldLabel).toBe("Can't get it away");
      expect(row.held.map((place) => place.name)).toEqual(['Assembler (Reinforced Iron Plate)']);
      expect(row.via).toBe('Iron Plate');
      expect(row.atLabel).toBe('Taken at full speed by');
    });

    it('leaves out a rate that is always nothing for its cause', () => {
      const nothing: TraceStart = {
        cause: 'nothingBringsIt',
        causeTerm: 'nothing brings it',
        item: 'Iron Ingot',
        perMinute: 0,
        at: [buildings('Conveyor Belt', ['b1'])],
        path: [constructors],
      };

      expect(whereItStarts([nothing])[0].perMinute).toBeNull();
    });

    it('names a drone port where no drone refuels as where it starts, without a rate', () => {
      const idle: TraceStart = {
        cause: 'noDroneRefuels',
        causeTerm: 'no drone refuels here',
        item: 'Battery',
        perMinute: 0,
        at: [buildings('Drone Port', ['p1'])],
        path: [constructors],
      };

      const row = whereItStarts([idle])[0];

      expect([row.cause, row.atLabel, row.perMinute]).toEqual([
        'No drone refuels here',
        'At',
        null,
      ]);
    });
  });
});
