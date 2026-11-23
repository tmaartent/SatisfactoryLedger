// Which resource nodes the map shows (asked 2026-11-23): by resource and purity, picked in the Resource nodes tab.

import { Purity } from '../../api/models/purity';
import { ResourceNodeResponse } from '../../api/models/resource-node-response';

/** The purities, from impure to pure: the tab's columns. */
export const Purities: readonly Purity[] = ['impure', 'normal', 'pure'];

/** A resource's row in the tab: how many nodes of each purity it has. */
export interface NodeKind {
  resource: string;
  counts: Record<Purity, number>;
}

/** One kind of node, a resource at a purity, as the hidden ones are kept: "Desc_OreIron_C:pure". */
export function nodeKey(resource: string, purity: Purity): string {
  return `${resource}:${purity}`;
}

/** A row per resource, by its name, with its nodes counted per purity. */
export function nodeKinds(
  nodes: readonly ResourceNodeResponse[],
  name: (resource: string) => string,
): NodeKind[] {
  const kinds = new Map<string, NodeKind>();
  for (const node of nodes) {
    const kind = kinds.get(node.resource) ?? {
      resource: node.resource,
      counts: { impure: 0, normal: 0, pure: 0 },
    };
    kind.counts[node.purity] += 1;
    kinds.set(node.resource, kind);
  }
  return [...kinds.values()].sort((a, b) => name(a.resource).localeCompare(name(b.resource)));
}

/** The nodes of every kind not hidden. */
export function shownNodes(
  nodes: readonly ResourceNodeResponse[],
  hidden: ReadonlySet<string>,
): ResourceNodeResponse[] {
  return nodes.filter((node) => !hidden.has(nodeKey(node.resource, node.purity)));
}
