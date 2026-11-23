import { Component, WritableSignal, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

import { ArrangementMachinesResponse } from '../../api/models/arrangement-machines-response';
import { ArrangementResponse } from '../../api/models/arrangement-response';
import { pickedAlone } from './arrangement-model';
import { BeltFloor3d } from './belt-floor-3d';

export interface BeltFloor3dDialogData {
  arrangement: ArrangementResponse;
  colourOf: (factoryId: string) => string | undefined;
  /** The factories' machines, to draw on their plots; null until they're fetched. */
  machines: ArrangementMachinesResponse | null;
  /** The factories picked, shared with the map: picking one here picks it there too. */
  focused: WritableSignal<ReadonlySet<string>>;
}

/**
 * The belt floor of a run in 3D, over the page; on the right, the levels to show, one by one (decided 2026-11-18): only
 * their lines, and the factories those connect.
 */
@Component({
  selector: 'app-belt-floor-3d-dialog',
  imports: [
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatIconModule,
    MatSlideToggleModule,
    BeltFloor3d,
  ],
  template: `
    <div class="header">
      <h2 mat-dialog-title>The belt floor in 3D</h2>
      <button mat-icon-button mat-dialog-close aria-label="Close">
        <mat-icon>close</mat-icon>
      </button>
    </div>
    <p class="muted">
      Each belt and pipe at its height: a ramp where it turns 2 m up or down, a lift where it turns
      4 m or more; the factories on their plots at the ceiling, with their machines. Drag to turn,
      scroll to zoom; click factories to pick them: what doesn't feed them fades.
    </p>
    <div class="body">
      <app-belt-floor-3d
        class="floor"
        [arrangement]="data.arrangement"
        [colourOf]="data.colourOf"
        [levels]="levels()"
        [machines]="data.machines"
        [showMachines]="showMachines()"
        [focused]="data.focused()"
        (picked)="data.focused.set(pickedAlone(data.focused(), $event))"
      />
      <aside class="panel">
        @if (data.machines?.machines?.length) {
          <mat-slide-toggle [checked]="showMachines()" (change)="showMachines.set($event.checked)"
            >Machines</mat-slide-toggle
          >
        }
        @if (data.focused().size > 0) {
          <button mat-stroked-button type="button" (click)="data.focused.set(emptyPicks)">
            Unpick factories
          </button>
        }
        <h3>Levels</h3>
        <div class="buttons">
          <button mat-stroked-button type="button" (click)="picked.set(all())">All</button>
          <button mat-stroked-button type="button" (click)="picked.set([])">None</button>
        </div>
        @for (level of all(); track level) {
          <mat-checkbox
            [checked]="picked().includes(level)"
            (change)="toggle(level, $event.checked)"
          >
            Level {{ level }}{{ level === 0 ? ' (floor)' : '' }}
          </mat-checkbox>
        }
        <p class="muted hint">
          A line with a run on a level ticked shows whole: its runs there in full, the rest faded,
          with its splitters and mergers and the factories it connects.
        </p>
      </aside>
    </div>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      height: 100%;
      padding: 0 16px 16px;
      box-sizing: border-box;
    }
    .header {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .muted {
      margin: 0 0 8px 24px;
      color: var(--mat-sys-on-surface-variant);
    }
    .body {
      display: flex;
      flex: 1;
      min-height: 0;
      gap: 16px;
    }
    .floor {
      flex: 1;
      min-width: 0;
    }
    .panel {
      display: flex;
      flex-direction: column;
      width: 180px;
      overflow-y: auto;
    }
    .panel > button {
      margin-top: 12px;
    }
    .panel h3 {
      margin: 16px 0 8px;
    }
    .buttons {
      display: flex;
      gap: 8px;
      margin-bottom: 8px;
    }
    .hint {
      margin: 8px 0 0;
    }
  `,
})
export class BeltFloor3dDialog {
  protected readonly data = inject<BeltFloor3dDialogData>(MAT_DIALOG_DATA);

  /** Every level a belt or pipe runs on, from the floor up. */
  protected readonly all = computed(() =>
    [...new Set(this.data.arrangement.belts.flatMap((belt) => belt.levels))].sort((a, b) => a - b),
  );

  /** The levels ticked; every one at first. */
  protected readonly picked = signal<number[]>(this.all());

  /** The levels shown: null when every one is ticked, so every factory shows too. */
  protected readonly levels = computed(() =>
    this.all().every((level) => this.picked().includes(level)) ? null : new Set(this.picked()),
  );

  /** Whether the factories' machines show; only the flat slabs when not. */
  protected readonly showMachines = signal(true);

  protected readonly pickedAlone = pickedAlone;
  protected readonly emptyPicks: ReadonlySet<string> = new Set();

  protected toggle(level: number, on: boolean): void {
    this.picked.update((picked) =>
      on ? [...picked, level] : picked.filter((other) => other !== level),
    );
  }
}
