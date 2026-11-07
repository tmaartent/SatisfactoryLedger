import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';

import { AnalysisResponse } from '../api/models/analysis-response';
import { GeneratorFinding } from '../api/models/generator-finding';
import { MachineFinding } from '../api/models/machine-finding';
import { MachineLimit } from '../api/models/machine-limit';
import { UnknownSupplyFinding } from '../api/models/unknown-supply-finding';
import { CatalogNames } from '../shared/catalog-names';
import { perMinute, percent, positionInMetres } from '../shared/display';

/**
 * What the world's analysis and a factory's analysis have in common (BR-A9 to A13). Only the world's has generators
 * (BR-A15): a generator is never part of a factory.
 */
export type Findings = Pick<AnalysisResponse, 'summary' | 'machines' | 'bottlenecks' | 'unusedSupply' | 'unknownSupply' | 'settled'> & {
  generators?: GeneratorFinding[];
};

/** The findings of an analysis: a summary, then a tab per kind of finding. The API orders each list worst first. */
@Component({
  selector: 'app-findings',
  imports: [
    DecimalPipe,
    MatButtonToggleModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatTableModule,
    MatTabsModule,
    MatTooltipModule,
  ],
  templateUrl: './findings.html',
  styleUrl: './findings.scss',
})
export class FindingsView {
  protected readonly names = inject(CatalogNames);
  readonly findings = input.required<Findings>();

  protected readonly limitFilter = signal<MachineLimit | 'all'>('all');
  protected readonly page = signal<PageEvent>({ pageIndex: 0, pageSize: 25, length: 0 });

  protected readonly machines = computed(() => {
    const filter = this.limitFilter();
    return this.findings().machines.filter((machine) => filter === 'all' || machine.limit === filter);
  });
  protected readonly machinesOnPage = computed(() => {
    const { pageIndex, pageSize } = this.page();
    return this.machines().slice(pageIndex * pageSize, (pageIndex + 1) * pageSize);
  });

  private readonly limits: Record<MachineLimit, string> = {
    none: 'Running',
    starved: 'Starved',
    outputBlocked: 'Output blocked',
    noRecipe: 'No recipe',
  };

  protected readonly machineColumns = ['machine', 'recipe', 'utilisation', 'limit', 'position'];
  protected readonly bottleneckColumns = ['belt', 'flow', 'offeredMore', 'position'];
  protected readonly unusedColumns = ['source', 'item', 'delivered', 'offered', 'position'];
  protected readonly unknownColumns = ['source', 'delivered', 'stock', 'position'];
  protected readonly generatorColumns = ['generator', 'fuel', 'utilisation', 'limit', 'position'];

  protected readonly percent = percent;
  protected readonly perMinute = perMinute;
  protected readonly position = positionInMetres;

  protected filterBy(limit: MachineLimit | 'all'): void {
    this.limitFilter.set(limit);
    this.page.update((page) => ({ ...page, pageIndex: 0 }));
  }

  // Material's table rows are untyped (let m is any) in the template: these methods type them.
  protected limitLabel(machine: MachineFinding): string {
    return this.limits[machine.limit];
  }

  protected deliveries(source: UnknownSupplyFinding): { item: string; rate: number }[] {
    return Object.entries(source.delivered).map(([item, rate]) => ({ item, rate }));
  }

  // Generator rows are untyped too.
  protected generatorLimit(generator: GeneratorFinding): string {
    return this.limits[generator.limit];
  }

  /** Why a machine or generator runs below 100%, in words. */
  protected why(machine: Pick<MachineFinding, 'limit' | 'limitingItem'>): string {
    switch (machine.limit) {
      case 'starved':
        return `Not enough ${this.names.item(machine.limitingItem)}`;
      case 'outputBlocked':
        return `${this.names.item(machine.limitingItem)} can't leave`;
      case 'noRecipe':
        return 'No recipe set';
      default:
        return '';
    }
  }
}
