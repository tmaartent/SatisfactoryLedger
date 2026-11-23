import { MachineFinding } from '../api/models/machine-finding';

/** What a machine's or generator's finding says about why it runs as it does (BR-A11, BR-A17). */
export type LimitFacts = Pick<MachineFinding, 'limit' | 'limitingItem'> &
  Partial<Pick<MachineFinding, 'starvedOf' | 'blockedOn' | 'onStock' | 'stockMinutes'>>;

/** A share as the game shows a percentage: whole numbers, never 100% for less than all. */
function share(fraction: number): string {
  return `${Math.min(99, Math.floor(fraction * 100))}%`;
}

/** An item in why a machine runs as it does: with its share when the finding has one ("50%"). */
export interface LimitItem {
  item: string | null;
  share: string | null;
}

/** Why it runs as it does, as words around its items: so a page can show each item with its icon. */
export interface LimitParts {
  before: string;
  items: LimitItem[];
  after: string;
}

/** The items with their share, worst first, as the API orders them: Ore (50%), Sulfur (80%). */
function listed(shares: Record<string, number>): LimitItem[] {
  return Object.entries(shares)
    .sort(([, a], [, b]) => a - b)
    .map(([item, fraction]) => ({ item, share: share(fraction) }));
}

const one = (item: string | null | undefined): LimitItem[] => [{ item: item ?? null, share: null }];

/**
 * Why it runs below 100%: every input it's short of and every product that can't all leave, with their shares; or what
 * it runs on from stock. Nothing when it runs on its supply. An analysis older than the lists names the one limiting item.
 */
export function limitParts(finding: LimitFacts): LimitParts {
  switch (finding.limit) {
    case 'starved':
      return finding.starvedOf && Object.keys(finding.starvedOf).length > 0
        ? { before: 'Not enough ', items: listed(finding.starvedOf), after: '' }
        : { before: 'Not enough ', items: one(finding.limitingItem), after: '' };
    case 'outputBlocked':
      return finding.blockedOn && Object.keys(finding.blockedOn).length > 0
        ? { before: "Can't all leave: ", items: listed(finding.blockedOn), after: '' }
        : { before: '', items: one(finding.limitingItem), after: " can't leave" };
    case 'noRecipe':
      return { before: 'No recipe set', items: [], after: '' };
    default:
      return finding.onStock
        ? { before: 'On stock: ', items: one(finding.onStock), after: `, lasts ${Math.round(finding.stockMinutes ?? 0)} min` }
        : { before: '', items: [], after: '' };
  }
}

/** Why it runs below 100%, in words (see limitParts): "Not enough Ore (50%), Sulfur (80%)". Empty when it runs on its supply. */
export function limitText(finding: LimitFacts, name: (item: string | null | undefined) => string): string {
  const { before, items, after } = limitParts(finding);
  return before + items.map(({ item, share }) => (share ? `${name(item)} (${share})` : name(item))).join(', ') + after;
}
