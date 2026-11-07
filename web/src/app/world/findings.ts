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
import { RouterLink } from '@angular/router';

import { AnalysisResponse } from '../api/models/analysis-response';
import { DronePortFinding } from '../api/models/drone-port-finding';
import { GeneratorFinding } from '../api/models/generator-finding';
import { MachineFinding } from '../api/models/machine-finding';
import { MachineLimit } from '../api/models/machine-limit';
import { UnknownSupplyFinding } from '../api/models/unknown-supply-finding';
import { CatalogNames } from '../shared/catalog-names';
import { perMinute, percent, positionInMetres } from '../shared/display';
import { LimitFacts, limitText } from './limit-text';

/**
 * What the world's analysis and a factory's analysis have in common (BR-A9 to A13). Only the world's has generators
 * (BR-A15) and drone ports (BR-A16): neither is ever part of a factory.
 */
export type Findings = Pick<AnalysisResponse, 'summary' | 'machines' | 'bottlenecks' | 'unusedSupply' | 'unknownSupply' | 'settled'> & {
  generators?: GeneratorFinding[];
  dronePorts?: DronePortFinding[];
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
    RouterLink,
  ],
  templateUrl: './findings.html',
  styleUrl: './findings.scss',
})
export class FindingsView {
  protected readonly names = inject(CatalogNames);
  readonly findings = input.required<Findings>();
  /** The snapshot the findings are of: where they stand links to its map. Without it, positions are plain text. */
  readonly snapshotId = input<string>();

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
  protected readonly dronePortColumns = ['port', 'fuel', 'burns', 'supplied', 'position'];

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

  /** What a drone port gets of the fuel its drones burn, as a fraction of 1. */
  protected suppliedShare(port: DronePortFinding): number {
    return port.burns > 0 ? Math.min(1, port.supplied / port.burns) : 1;
  }

  /** How much fuel a drone port is short of, in words; empty when it gets all it burns. */
  protected shortBy(port: DronePortFinding): string {
    const short = port.burns - port.supplied;
    return short > 0.005 ? `${perMinute(short)} short` : '';
  }

  /** Why a machine or generator runs below 100%, in words; and when it runs on what it holds (BR-A17), on what. */
  protected why(machine: LimitFacts): string {
    return limitText(machine, (item) => this.names.item(item));
  }
}
