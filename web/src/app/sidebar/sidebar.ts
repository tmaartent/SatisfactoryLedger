import { NgTemplateOutlet } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

import { afterSwipe } from './sheet';
import { SidebarTabs } from './sidebar-tabs';

/**
 * The sidebar on the right (asked 2026-11-22): the open tab, and the rail with an icon per tab. Clicking the open tab's
 * icon collapses it to the rail. What a tab shows is the page's (SidebarTab); this only lays it out. On a phone (asked
 * 2026-11-24) the rail is a bar along the bottom and the open tab a sheet above it, at half height or full, moved by
 * swiping or tapping its handle.
 */
@Component({
  selector: 'app-sidebar',
  imports: [NgTemplateOutlet, MatButtonModule, MatIconModule],
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.scss',
  // Closed to its rail: on a phone, only the bar of tabs shows.
  host: { '[class.closed]': 'tabs.collapsed()' },
})
export class Sidebar {
  protected readonly tabs = inject(SidebarTabs);
  /** Where the finger went down on the handle, from the top of the window. */
  private from: number | null = null;

  protected grab(event: PointerEvent): void {
    this.from = event.clientY;
    // Its moves and its lifting are the handle's, wherever the finger goes.
    if (event.target instanceof Element) event.target.setPointerCapture?.(event.pointerId);
  }

  protected letGo(event: PointerEvent): void {
    if (this.from === null) return;
    const next = afterSwipe(this.tabs.sheet(), event.clientY - this.from);
    this.from = null;
    if (next === 'closed') {
      // Closed: it opens at half height again.
      this.tabs.collapsed.set(true);
      this.tabs.sheet.set('half');
    } else this.tabs.sheet.set(next);
  }
}
