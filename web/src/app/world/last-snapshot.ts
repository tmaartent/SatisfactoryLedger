import { Injectable, signal } from '@angular/core';

/** Where it's kept in the browser; per player and browser, a convenience only. */
const Key = 'satisfactory-ledger.last-snapshot';

/**
 * The snapshot last looked at, so that going back to the World opens its map (asked 2026-11-23). Kept in the browser,
 * so it's there when the site is opened again; forgotten when it's deleted. Without browser storage, only while the
 * site is open.
 */
@Injectable({ providedIn: 'root' })
export class LastSnapshot {
  private readonly last = signal<string | null>(read());
  readonly id = this.last.asReadonly();

  remember(id: string): void {
    this.last.set(id);
    write(id);
  }

  /** A snapshot deleted: when it's the one remembered, none is. */
  forget(id: string): void {
    if (this.last() !== id) return;
    this.last.set(null);
    write(null);
  }
}

function read(): string | null {
  try {
    return localStorage.getItem(Key);
  } catch {
    return null;
  }
}

function write(id: string | null): void {
  try {
    if (id) localStorage.setItem(Key, id);
    else localStorage.removeItem(Key);
  } catch {
    // Blocked storage: remembered while the site is open, as the signal holds it.
  }
}
