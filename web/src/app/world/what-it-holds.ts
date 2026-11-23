import { Component, input } from '@angular/core';

import { SnapshotCounts } from '../api/models/snapshot-counts';

/**
 * What a snapshot holds: how many machines, logistics, providers, consumers and connections, each said in words. Under
 * the overview on the map's Info tab (asked 2026-11-23); it was a tab of the findings page, gone since.
 */
@Component({
  selector: 'app-what-it-holds',
  template: `
    <ul>
      <li>
        <strong>{{ counts().machines }}</strong> machines
      </li>
      <li>
        <strong>{{ counts().logistics }}</strong> belts, pipes, splitters and mergers
      </li>
      <li>
        <strong>{{ counts().providers }}</strong> providers: miners, extractors and stations that
        bring items in
      </li>
      <li>
        <strong>{{ counts().consumers }}</strong> consumers: sinks, generators and stations that
        load
      </li>
      <li>
        <strong>{{ counts().connections }}</strong> connections
      </li>
    </ul>
  `,
  styles: `
    ul {
      display: grid;
      gap: 6px;
      margin: 0;
      padding: 0;
      list-style: none;
    }

    strong {
      font: var(--mat-sys-title-medium);
      margin-right: 4px;
    }
  `,
})
export class WhatItHolds {
  readonly counts = input.required<SnapshotCounts>();
}
