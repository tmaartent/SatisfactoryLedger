import { ResourceNodeResponse } from '../../api/models/resource-node-response';
import { Purities, nodeKey, nodeKinds, shownNodes } from './node-filter';

const node = (
  resource: string,
  purity: ResourceNodeResponse['purity'],
  name = `${resource}-${purity}`,
): ResourceNodeResponse => ({
  name,
  resource,
  purity,
  kind: 'resourceNode',
  inGame: true,
  position: { x: 0, y: 0, z: 0 },
});

describe('filtering the resource nodes by kind', () => {
  const names: Record<string, string> = { Desc_OreIron_C: 'Iron Ore', Desc_Coal_C: 'Coal' };
  const name = (resource: string) => names[resource] ?? resource;

  it('goes from impure to pure', () => {
    expect(Purities).toEqual(['impure', 'normal', 'pure']);
  });

  it('has a row per resource, by name, counting its nodes of each purity', () => {
    const nodes = [
      node('Desc_OreIron_C', 'pure', 'a'),
      node('Desc_OreIron_C', 'pure', 'b'),
      node('Desc_Coal_C', 'impure'),
      node('Desc_OreIron_C', 'normal'),
    ];

    expect(nodeKinds(nodes, name)).toEqual([
      { resource: 'Desc_Coal_C', counts: { impure: 1, normal: 0, pure: 0 } },
      { resource: 'Desc_OreIron_C', counts: { impure: 0, normal: 1, pure: 2 } },
    ]);
  });

  it('shows every node whose resource and purity are not hidden', () => {
    const nodes = [
      node('Desc_OreIron_C', 'pure'),
      node('Desc_OreIron_C', 'impure'),
      node('Desc_Coal_C', 'pure'),
    ];

    const shown = shownNodes(nodes, new Set([nodeKey('Desc_OreIron_C', 'pure')]));

    expect(shown.map((n) => n.name)).toEqual(['Desc_OreIron_C-impure', 'Desc_Coal_C-pure']);
  });
});
