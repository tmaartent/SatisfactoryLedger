import { Injectable, signal } from '@angular/core';

import { toggle } from '../area/selection';

/** Where it's kept in the browser; per player and browser, a convenience only. */
const Key = 'satisfactory-ledger.hidden-nodes';

/**
 * The kinds of resource node hidden on the map, by resource and purity (asked 2026-11-23). What's hidden is kept, not
 * what's shown, so a resource the game adds shows at first. Kept in the browser, so the map looks the same when the
 * site is opened again; without browser storage, only while the site is open.
 */
@Injectable({ providedIn: 'root' })
export class HiddenNodes {
  private readonly kinds = signal<ReadonlySet<string>>(read());
  readonly hidden = this.kinds.asReadonly();

  /** Hides a kind shown, or shows a kind hidden (a key from nodeKey). */
  toggle(key: string): void {
    this.save(toggle(this.kinds(), key));
  }

  hideAll(keys: readonly string[]): void {
    this.save(new Set(keys));
  }

  showAll(): void {
    this.save(new Set());
  }

  private save(kinds: ReadonlySet<string>): void {
    this.kinds.set(kinds);
    try {
      localStorage.setItem(Key, JSON.stringify([...kinds]));
    } catch {
      // Blocked storage: kept while the site is open, as the signal holds it.
    }
  }
}

function read(): ReadonlySet<string> {
  try {
    const kept: unknown = JSON.parse(localStorage.getItem(Key) ?? '[]');
    return new Set(
      Array.isArray(kept) ? kept.filter((key): key is string => typeof key === 'string') : [],
    );
  } catch {
    return new Set();
  }
}
