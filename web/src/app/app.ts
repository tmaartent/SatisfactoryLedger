import { Component, inject } from '@angular/core';
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
  /** Whether the page is the World's: its snapshots, their maps, areas and factories. */
  protected readonly inWorld = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map(() => this.router.url.startsWith('/snapshots')),
    ),
    { initialValue: false },
  );
}
