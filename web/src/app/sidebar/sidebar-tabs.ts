import { computed, Injectable, signal, TemplateRef } from '@angular/core';

import { SheetHeight } from './sheet';

/** A tab of the sidebar: its icon on the rail, and what it shows, a template the page declares (see SidebarTab). */
export interface Tab {
  key: string;
  label: string;
  icon: string;
  /**
   * Lower first, as the page orders its tabs: a tab inside an @if block is added after one outside it, so the order
   * they're added in says nothing. The explanation, which every World page has, last.
   */
  order: number;
  template: TemplateRef<unknown>;
}

/**
 * The sidebar on the right of every World page (asked 2026-11-22): a rail of icons, one per tab, and the tab open beside
 * it. Each page brings its own tabs and takes them away when it leaves; the explanation stays across pages. Open by
 * default, collapsed on a touch screen; collapsed, only the rail is left and the page gets its width back.
 */
@Injectable({ providedIn: 'root' })
export class SidebarTabs {
  private readonly added = signal<Tab[]>([]);
  /** The tab last asked for; when the page shown has none by that key, its first tab is shown. */
  private readonly chosen = signal<string | null>(null);

  readonly tabs = computed(() => [...this.added()].sort((a, b) => a.order - b.order));
  readonly current = computed(
    () => this.tabs().find((tab) => tab.key === this.chosen()) ?? this.tabs()[0] ?? null,
  );
  /** Collapsed from the start on a touch screen (asked 2026-11-24), where the open tab lies over the page; a tap opens it. */
  readonly collapsed = signal(window.matchMedia?.('(hover: none)').matches ?? false);
  /**
   * How high the open tab stands as a sheet on a phone (asked 2026-11-24); elsewhere it fills the sidebar's height. Here,
   * not in the sidebar, as the map centres what it zooms to on what the tab leaves in view.
   */
  readonly sheet = signal<SheetHeight>('half');

  add(tab: Tab): void {
    this.added.update((tabs) => [...tabs, tab]);
  }

  remove(tab: Tab): void {
    this.added.update((tabs) => tabs.filter((candidate) => candidate !== tab));
  }

  /** Shows a tab, opening the sidebar when it was collapsed: a click in the page that the tab answers. */
  show(key: string): void {
    this.chosen.set(key);
    this.collapsed.set(false);
  }

  /**
   * Before a 3D view (asked 2026-11-24): where the open tab would lie over the view, it starts closed. On a phone it's a
   * sheet over the lower part of the screen; up to 960 px wide it lies over the page's right side (sidebar.scss), and on
   * a tablet over the 3D view's select button (found by the layout tests, 2026-11-24). A click on what's in the view
   * opens it again on the Info tab.
   */
  closeWhereItCovers(): void {
    if (window.matchMedia?.('(max-width: 960px)').matches) this.collapsed.set(true);
  }

  /**
   * Before the arrangement's 3D view (asked 2026-11-25): with a mouse, the view's own tab opens beside it; on a touch
   * screen the tabs close, so the view can be seen, and a tap on a tab's icon opens it.
   */
  showBeside3d(key: string): void {
    if (window.matchMedia?.('(hover: none)').matches) this.collapsed.set(true);
    else this.show(key);
  }

  /** A click on a tab's icon: the open tab collapses; any other opens. */
  toggle(key: string): void {
    if (this.current()?.key === key && !this.collapsed()) this.collapsed.set(true);
    else this.show(key);
  }
}
