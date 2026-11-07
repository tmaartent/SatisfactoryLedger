import { MachineFinding } from '../api/models/machine-finding';

/** What a machine's or generator's finding says about why it runs as it does (BR-A11, BR-A17). */
export type LimitFacts = Pick<MachineFinding, 'limit' | 'limitingItem'> &
  Partial<Pick<MachineFinding, 'starvedOf' | 'blockedOn' | 'onStock' | 'stockMinutes'>>;

/** A share as the game shows a percentage: whole numbers, never 100% for less than all. */
function share(fraction: number): string {
  return `${Math.min(99, Math.floor(fraction * 100))}%`;
}

/** The items with their share, worst first, as the API orders them: "Ore (50%), Sulfur (80%)". */
function listed(shares: Record<string, number>, name: (item: string) => string): string {
  return Object.entries(shares)
    .sort(([, a], [, b]) => a - b)
    .map(([item, fraction]) => `${name(item)} (${share(fraction)})`)
    .join(', ');
}

/**
 * Why it runs below 100%, in words: every input it's short of and every product that can't all leave, with their
 * shares; or what it runs on from stock. Empty when it runs on its supply. An analysis older than the lists names the
 * one limiting item.
 */
export function limitText(finding: LimitFacts, name: (item: string | null | undefined) => string): string {
  switch (finding.limit) {
    case 'starved':
      return finding.starvedOf && Object.keys(finding.starvedOf).length > 0
        ? `Not enough ${listed(finding.starvedOf, name)}`
        : `Not enough ${name(finding.limitingItem)}`;
    case 'outputBlocked':
      return finding.blockedOn && Object.keys(finding.blockedOn).length > 0
        ? `Can't all leave: ${listed(finding.blockedOn, name)}`
        : `${name(finding.limitingItem)} can't leave`;
    case 'noRecipe':
      return 'No recipe set';
    default:
      return finding.onStock ? `On stock: ${name(finding.onStock)}, lasts ${Math.round(finding.stockMinutes ?? 0)} min` : '';
  }
}
