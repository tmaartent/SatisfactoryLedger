// The factories of a world in the 3D view, on the map and in the findings, apart from three.js: who owns which machine, and each factory's edge colour.

/** A factory as traced in a snapshot: only what the 3D view needs of it. */
export interface TracedFactory {
  factoryId: string;
  name: string;
  machines: { id: string }[];
}

/** A factory as the 3D view outlines it. */
export interface OutlinedFactory {
  factoryId: string;
  name: string;
  colour: string;
}

/** Edge colours, apart from the status colours, the blue of chosen machines and the yellow of the one looked at. */
export const outlineColours = ['#ff4081', '#b388ff', '#18ffff', '#eeff41', '#ff9e80', '#f5f5f5', '#69f0ae', '#ea80fc'];

/** Each factory with its colour, from its place among all the world's factories: the same in every area. */
function outlined(factories: readonly TracedFactory[]): (OutlinedFactory & { machines: { id: string }[] })[] {
  return factories.map((factory, i) => ({ ...factory, colour: outlineColours[i % outlineColours.length] }));
}

/**
 * The factories each machine is in, by machine id. One at most since a machine belongs to one factory (BR-W5); a
 * machine stored in more than one, from before that rule, lists them all, so the view can show the overlap.
 */
export function machineOwners(factories: readonly TracedFactory[]): Map<string, OutlinedFactory[]> {
  const owners = new Map<string, OutlinedFactory[]>();
  for (const { machines, ...factory } of outlined(factories)) {
    for (const machine of machines) owners.set(machine.id, [...(owners.get(machine.id) ?? []), factory]);
  }
  return owners;
}

/** The factories with at least one machine among the buildings in the area, for the legend. */
export function factoriesInArea(factories: readonly TracedFactory[], inArea: ReadonlySet<string>): OutlinedFactory[] {
  return outlined(factories)
    .filter((factory) => factory.machines.some((machine) => inArea.has(machine.id)))
    .map(({ machines: _, ...factory }) => factory);
}

/** The colour a factory's machines are outlined in, for the map's highlight; undefined when the world has no such factory. */
export function factoryColour(factories: readonly TracedFactory[], factoryId: string): string | undefined {
  return outlined(factories).find((factory) => factory.factoryId === factoryId)?.colour;
}
