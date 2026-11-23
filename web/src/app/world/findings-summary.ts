import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

import { AnalysisResponse } from '../api/models/analysis-response';
import { MachineLimit } from '../api/models/machine-limit';
import { ShownStatus } from './shown-status';

/** One card of the overview: a count, what it counts, and the status it can show, if any. */
interface Count {
  figure: number;
  label: string;
  tone?: 'good' | 'bad';
  status?: ShownStatus;
}

/**
 * The overview of a world's or a factory's findings, in cards: its machines and how many run, are starved, output
 * blocked or without a recipe; its generators alike; and whether part of the network didn't settle. Where something
 * can show them (the map, asked 2026-11-23), a count of a status is a button: clicked, it shows those buildings, and
 * clicked again, none.
 */
@Component({
  selector: 'app-findings-summary',
  imports: [NgTemplateOutlet, MatCardModule, MatIconModule, MatTooltipModule],
  templateUrl: './findings-summary.html',
  styleUrl: './findings-summary.scss',
})
export class FindingsSummary {
  readonly findings =
    input.required<Pick<AnalysisResponse, 'summary' | 'generatorSummary' | 'settled'>>();
  /** Whether a count of a status can be clicked to show its buildings. */
  readonly selectable = input(false);
  /** The status whose buildings are shown, if any. */
  readonly shown = input<ShownStatus | null>(null);
  readonly show = output<ShownStatus | null>();
  /** How many bottlenecks there are, for a row of their own; none where it isn't given (asked for the map 2026-11-23). */
  readonly bottlenecks = input<number | null>(null);

  protected readonly machines = computed<Count[]>(() => {
    const summary = this.findings().summary;
    const of = (limit: MachineLimit): ShownStatus => ({ of: 'machines', limit });
    return [
      { figure: summary.machines, label: 'machines' },
      { figure: summary.running, label: 'at 100%', tone: 'good', status: of('none') },
      { figure: summary.starved, label: 'starved', tone: 'bad', status: of('starved') },
      {
        figure: summary.outputBlocked,
        label: 'output blocked',
        tone: 'bad',
        status: of('outputBlocked'),
      },
      { figure: summary.noRecipe, label: 'without a recipe', status: of('noRecipe') },
    ];
  });

  protected readonly generators = computed<Count[]>(() => {
    const summary = this.findings().generatorSummary;
    const of = (limit: MachineLimit): ShownStatus => ({ of: 'generators', limit });
    return [
      { figure: summary.generators, label: 'generators' },
      { figure: summary.running, label: 'at 100%', tone: 'good', status: of('none') },
      { figure: summary.starved, label: 'starved', tone: 'bad', status: of('starved') },
      {
        figure: summary.outputBlocked,
        label: 'output blocked',
        tone: 'bad',
        status: of('outputBlocked'),
      },
      // Not shown on the map: a generator whose fuel isn't known has no status there.
      { figure: summary.fuelUnknown, label: 'fuel unknown' },
    ];
  });

  protected readonly bottleneckCounts = computed<Count[]>(() => {
    const bottlenecks = this.bottlenecks();
    return bottlenecks === null
      ? []
      : [
          {
            figure: bottlenecks,
            label: bottlenecks === 1 ? 'bottleneck' : 'bottlenecks',
            tone: bottlenecks > 0 ? 'bad' : 'good',
            status: { of: 'bottlenecks' },
          },
        ];
  });

  /** The count's status, when it can be clicked: there's something to show, and something to show it. */
  protected clickable(count: Count): ShownStatus | null {
    return this.selectable() && count.status && count.figure > 0 ? count.status : null;
  }

  protected isShown(count: Count): boolean {
    const shown = this.shown();
    return !!shown && !!count.status && sameStatus(shown, count.status);
  }

  protected toggle(count: Count): void {
    const status = this.clickable(count);
    if (status) this.show.emit(this.isShown(count) ? null : status);
  }
}

/** Two counts' statuses alike: of the same, and of the same status where it has one. */
function sameStatus(a: ShownStatus, b: ShownStatus): boolean {
  if (a.of === 'bottlenecks' || b.of === 'bottlenecks') return a.of === b.of;
  return a.of === b.of && a.limit === b.limit;
}
