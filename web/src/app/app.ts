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

/**
 * The shell: a toolbar with the two areas, World (its map) and Catalog, the page below it, and on its right the sidebar
 * with the page's tabs. Every World page has the Explanation tab too.
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatToolbarModule, MatButtonModule, MatIconModule, Sidebar, SidebarTab, ExplainPanel],
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

  constructor() {
    // Any page of a snapshot, its map, 3D view or a factory, makes it the one looked at.
    effect(() => {
      const id = /^\/snapshots\/([^/?#]+)/.exec(this.url())?.[1];
      if (id) this.lastSnapshot.remember(id);
    });
  }

  /** Back to the World, on the Snapshots tab: to pick another one (asked 2026-11-23). */
  protected goToWorld(): void {
    this.sidebar.show('snapshots');
  }
}
