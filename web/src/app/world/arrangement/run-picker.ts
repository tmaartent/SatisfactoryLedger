import { DatePipe, formatDate } from '@angular/common';
import { Component, LOCALE_ID, computed, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';

import { ArrangementSummary } from '../../api/models/arrangement-summary';
import { runStatus, runSummary } from './arrangement-model';

/**
 * The runs of the optimal arrangement for a world, as the snapshot picker lists snapshots (asked 2026-11-25): the run
 * shown names the button, and its list has every run, newest first, summed up in a line, each with a delete button. It
 * was a table above the map, which pushed the map down and, beside a Settings tab, read as unclear.
 */
@Component({
  selector: 'app-run-picker',
  imports: [DatePipe, MatButtonModule, MatIconModule, MatMenuModule],
  templateUrl: './run-picker.html',
  styleUrl: './run-picker.scss',
})
export class RunPicker {
  /** The world's runs, newest first. */
  readonly runs = input.required<readonly ArrangementSummary[]>();
  /** The run shown; none while a new area is drawn. */
  readonly selected = input.required<string | null>();

  readonly picked = output<string>();
  readonly deleted = output<ArrangementSummary>();

  protected readonly shown = computed(
    () => this.runs().find((run) => run.id === this.selected()) ?? null,
  );

  private readonly locale = inject(LOCALE_ID);

  /** What the button shows, and so what it's called: voice control goes by the words on it. */
  protected readonly label = computed(() => {
    const run = this.shown();
    if (run)
      return `${formatDate(run.requestedAt, 'd MMM, HH:mm', this.locale)} · ${runStatus(run)}`;
    const count = this.runs().length;
    return count > 0 ? `${count} earlier ${count === 1 ? 'run' : 'runs'}` : 'No runs yet';
  });

  protected readonly summary = runSummary;
}
