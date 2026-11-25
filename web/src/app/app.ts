import { Component, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';

import { Sidebar } from './sidebar/sidebar';
import { SidebarTab } from './sidebar/sidebar-tab';
import { SidebarTabs } from './sidebar/sidebar-tabs';
import { ExplainPanel } from './world/explain/explain-panel';
import { LastSnapshot } from './world/last-snapshot';
import { SnapshotPicker } from './world/snapshot-picker';

/**
 * The shell: a toolbar with the two areas, World (its map) and Catalog, the page below it, and on its right the sidebar
 * with the page's tabs. Every World page has the Explanation tab too.
 */
@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    Sidebar,
    SidebarTab,
    ExplainPanel,
    SnapshotPicker,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly sidebar = inject(SidebarTabs);
  private readonly router = inject(Router);
  private readonly lastSnapshot = inject(LastSnapshot);
  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map(() => this.router.url),
    ),
    { initialValue: '' },
  );
  /** Whether the page is the World's: its snapshots, their maps, areas and factories. */
  protected readonly inWorld = computed(() => this.url().startsWith('/snapshots'));
  /** The World: the map of the snapshot last looked at (asked 2026-11-23), or the map of none before there is one. */
  protected readonly world = computed(() => {
    const last = this.lastSnapshot.id();
    return last ? ['/snapshots', last, 'map'] : ['/snapshots'];
  });

  /**
   * Whether the page has the snapshot picker above its tabs: every World page but those about one part of the snapshot
   * they were opened from, where picking another means nothing: the optimal arrangement (asked 2026-11-23) and a
   * factory's page (asked 2026-11-24).
   */
  protected readonly picksSnapshot = computed(
    () => this.inWorld() && !/^\/snapshots\/[^/?#]+\/(arrangement|factories)\b/.test(this.url()),
  );

  /**
   * Whether the page has the Explanation tab: every World page but the optimal arrangement, which has no findings to
   * explain (asked 2026-11-25).
   */
  protected readonly explains = computed(
    () => this.inWorld() && !/^\/snapshots\/[^/?#]+\/arrangement\b/.test(this.url()),
  );

  /** The snapshot the page is about, from its URL; null on the World's start and outside the World. */
  protected readonly snapshotId = computed(
    () => /^\/snapshots\/([^/?#]+)/.exec(this.url())?.[1] ?? null,
  );

  constructor() {
    // Any page of a snapshot, its map, 3D view or a factory, makes it the one looked at.
    effect(() => {
      const id = this.snapshotId();
      if (id) this.lastSnapshot.remember(id);
    });
  }
}
