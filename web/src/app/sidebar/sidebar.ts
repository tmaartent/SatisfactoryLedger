import { NgTemplateOutlet } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

import { SidebarTabs } from './sidebar-tabs';

/**
 * The sidebar on the right (asked 2026-11-22): the open tab, and the rail with an icon per tab. Clicking the open tab's
 * icon collapses it to the rail. What a tab shows is the page's (SidebarTab); this only lays it out.
 */
@Component({
  selector: 'app-sidebar',
  imports: [NgTemplateOutlet, MatButtonModule, MatIconModule],
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.scss',
})
export class Sidebar {
  protected readonly tabs = inject(SidebarTabs);
}
