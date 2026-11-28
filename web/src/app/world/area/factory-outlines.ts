// The factories of a world in the 3D view, on the map and in the findings, apart from three.js: who owns which machine, and each factory's edge colour.

/** A factory as traced in a snapshot: only what the 3D view needs of it. */
export interface TracedFactory {
  factoryId: string;
  name: string;
  /** Each machine with the factory that owns it world-wide, as the back end decides it (BR-W5). */
  machines: { id: string; owner: string }[];
}

/** A factory as the 3D view outlines it. */
export interface OutlinedFactory {
  factoryId: string;
  name: string;
  colour: string;
}

/** Edge colours, apart from the status colours, the blue of chosen machines and the yellow of the one looked at. */
export const outlineColours = [
  '#ff4081',
  '#b388ff',
  '#18ffff',
  '#eeff41',
  '#ff9e80',
  '#f5f5f5',
  '#69f0ae',
  '#ea80fc',
];

/** Each factory with its colour, from its place among all the world's factories: the same in every area. */
function outlined(
  factories: readonly TracedFactory[],
): (OutlinedFactory & Pick<TracedFactory, 'machines'>)[] {
  return factories.map((factory, i) => ({
    ...factory,
    colour: outlineColours[i % outlineColours.length],
  }));
}

/** Who a machine belongs to: its owner, and the other factories stored with it from before BR-W5, to show the overlap. */
export interface MachineOwner {
  owner: OutlinedFactory;
  alsoIn: OutlinedFactory[];
}

/** The owner of each machine some factory has, by machine id, as the back end says (BR-W5). */
export function machineOwners(factories: readonly TracedFactory[]): Map<string, MachineOwner> {
  const outlinedFactories = outlined(factories);
  const byId = new Map(
    outlinedFactories.map(({ machines: _, ...factory }) => [factory.factoryId, factory]),
  );
  const owners = new Map<string, MachineOwner>();
  for (const { machines, ...factory } of outlinedFactories) {
    for (const machine of machines) {
      const owner = owners.get(machine.id) ?? {
        owner: byId.get(machine.owner) ?? factory,
        alsoIn: [],
      };
      if (factory.factoryId !== owner.owner.factoryId) owner.alsoIn.push(factory);
      owners.set(machine.id, owner);
    }
  }
  return owners;
}

/** The factories with at least one machine among the buildings in the area, by name, for the legend. */
export function factoriesInArea(
  factories: readonly TracedFactory[],
  inArea: ReadonlySet<string>,
): OutlinedFactory[] {
  return outlined(factories)
    .filter((factory) => factory.machines.some((machine) => inArea.has(machine.id)))
    .map(({ machines: _, ...factory }) => factory)
    .sort((one, other) => one.name.localeCompare(other.name));
}

/** The colour a factory's machines are outlined in, for the map's highlight; undefined when the world has no such factory. */
export function factoryColour(
  factories: readonly TracedFactory[],
  factoryId: string,
): string | undefined {
  return outlined(factories).find((factory) => factory.factoryId === factoryId)?.colour;
}

/**
 * The machines that can't be chosen (BR-W5): every factory's, or, while editing a factory, the other factories'. Its
 * own machines can be taken out and put back.
 */
export function blockedMachines(
  owners: ReadonlyMap<string, MachineOwner>,
  editing: string | null,
): Set<string> {
  return new Set(
    [...owners]
      .filter(([, { owner, alsoIn }]) =>
        [owner, ...alsoIn].some((factory) => factory.factoryId !== editing),
      )
      .map(([id]) => id),
  );
}
