import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
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
import { ConnectionSummary } from '../api/models/connection-summary';
import { DronePortFinding } from '../api/models/drone-port-finding';
import { FactoryTrace } from '../api/models/factory-trace';
import { GeneratorFinding } from '../api/models/generator-finding';
import { MachineFinding } from '../api/models/machine-finding';
import { MachineLimit } from '../api/models/machine-limit';
import { UnknownSupplyFinding } from '../api/models/unknown-supply-finding';
import { CatalogNames } from '../shared/catalog-names';
import { perMinute, percent, positionInMetres } from '../shared/display';
import { OutlinedFactory, factoryColour, machineOwners } from './area/factory-outlines';
import { LimitFacts, limitText } from './limit-text';

/**
 * What the world's analysis and a factory's analysis have in common (BR-A9 to A13). Generators (BR-A15) are in both:
 * a factory has those chosen as its machines (BR-W2). Only the world's has drone ports (BR-A16).
 */
export type Findings = Pick<AnalysisResponse, 'summary' | 'generatorSummary' | 'machines' | 'bottlenecks' | 'unusedSupply' | 'unknownSupply' | 'settled'> & {
  generators?: GeneratorFinding[];
  dronePorts?: DronePortFinding[];
};

/** The findings of an analysis: a summary, then a tab per kind of finding. The API orders each list worst first. */
@Component({
  selector: 'app-findings',
  imports: [
    DecimalPipe,
    MatButtonModule,
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
  /** The generators tab shows only when there are any: most factories have none. */
  protected readonly generators = computed(() => (this.findings().generators?.length ? this.findings().generators! : null));
  /** The snapshot the findings are of: where they stand links to its map. Without it, positions are plain text. */
  readonly snapshotId = input<string>();
  /**
   * The world's factories as traced in this snapshot. Given, there is a Factories tab, and each machine and generator
   * says which factory it is in. A factory's own page leaves them out: all its machines are in it.
   */
  readonly factories = input<FactoryTrace[]>();
  /** A factory the player wants deleted, from the Factories tab; the page asks first. */
  readonly deleteFactory = output<FactoryTrace>();

  /** Each factory with its colour, the one its machines are edged in on the map and in 3D. */
  protected readonly factoryRows = computed(() => {
    const factories = this.factories() ?? [];
    return factories.map((factory) => ({ ...factory, colour: factoryColour(factories, factory.factoryId) }));
  });
  private readonly owners = computed(() => machineOwners(this.factories() ?? []));

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

  protected readonly machineColumns = computed(() =>
    this.factories() ? ['machine', 'factory', 'recipe', 'utilisation', 'limit', 'position'] : ['machine', 'recipe', 'utilisation', 'limit', 'position'],
  );
  protected readonly factoryColumns = ['factory', 'machines', 'makes', 'needs', 'actions'];
  protected readonly bottleneckColumns = ['belt', 'flow', 'offeredMore', 'position'];
  protected readonly unusedColumns = ['source', 'item', 'delivered', 'offered', 'position'];
  protected readonly unknownColumns = ['source', 'delivered', 'stock', 'position'];
  protected readonly generatorColumns = computed(() =>
    this.factories() ? ['generator', 'factory', 'fuel', 'utilisation', 'limit', 'position'] : ['generator', 'fuel', 'utilisation', 'limit', 'position'],
  );
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

  /** The factory a machine or generator is in; the first, for one stored in two from before BR-W5. */
  protected factoryOf(id: string): OutlinedFactory | undefined {
    return this.owners().get(id)?.[0];
  }

  /** The items a factory's outputs or inputs carry, each once, by name. */
  protected itemsOf(connections: ConnectionSummary[]): string {
    return [...new Set(connections.flatMap((connection) => connection.items))].map((item) => this.names.item(item)).join(', ') || '—';
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
