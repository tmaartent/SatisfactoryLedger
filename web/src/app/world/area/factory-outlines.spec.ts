import { factoriesInArea, factoryColour, machineOwners, outlineColours } from './factory-outlines';

const factory = (factoryId: string, name: string, ...machines: string[]) => ({ factoryId, name, machines: machines.map((id) => ({ id })) });

describe('factories in the 3D view', () => {
  const plates = factory('f1', 'Iron plates', 'smelter', 'constructor');
  const rods = factory('f2', 'Iron rods', 'rod maker');

  describe('the owners of a machine', () => {
    it('are the factories it is in', () => {
      const owners = machineOwners([plates, rods]);

      expect([owners.get('smelter')?.map((f) => f.name), owners.get('rod maker')?.map((f) => f.name)]).toEqual([['Iron plates'], ['Iron rods']]);
    });

    it('are none for a machine no factory has', () => {
      expect(machineOwners([plates]).get('assembler')).toBeUndefined();
    });

    it('are all of them for a machine stored in more than one factory, from before a machine had one at most', () => {
      const shared = factory('f3', 'Smelting', 'smelter');

      expect(machineOwners([plates, shared]).get('smelter')?.map((f) => f.name)).toEqual(['Iron plates', 'Smelting']);
    });
  });

  describe('the factories shown', () => {
    it('are those with a machine in the area', () => {
      expect(factoriesInArea([plates, rods], new Set(['constructor', 'belt'])).map((f) => f.name)).toEqual(['Iron plates']);
    });

    it('keep their colour whichever area they are seen in', () => {
      const [colourOfRods] = factoriesInArea([plates, rods], new Set(['rod maker'])).map((f) => f.colour);

      expect(colourOfRods).toBe(outlineColours[1]);
    });

    it('share colours again when there are more factories than colours', () => {
      const many = outlineColours.map((_, i) => factory(`f${i}`, `F${i}`, `m${i}`));
      const extra = factory('extra', 'One more', 'm-extra');

      expect(factoriesInArea([...many, extra], new Set(['m-extra']))[0].colour).toBe(outlineColours[0]);
    });
  });

  describe('a factory highlighted on the map', () => {
    it('has the colour its machines are outlined in, in 3D', () => {
      expect(factoryColour([plates, rods], rods.factoryId)).toBe(factoriesInArea([plates, rods], new Set(['rod maker']))[0].colour);
    });

    it('has none when the world has no such factory', () => {
      expect(factoryColour([plates, rods], 'gone')).toBeUndefined();
    });
  });
});
