import { Component, inject, input, resource, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';

import { Api } from '../api/api';
import { analyseFactory } from '../api/fn/world/analyse-factory';
import { FactoryInput } from '../api/models/factory-input';
import { FactoryOutput } from '../api/models/factory-output';
import { InputSource } from '../api/models/input-source';
import { InputSourceKind } from '../api/models/input-source-kind';
import { OutputDestination } from '../api/models/output-destination';
import { OutputDestinationKind } from '../api/models/output-destination-kind';
import { CatalogNames } from '../shared/catalog-names';
import { megawatts, perMinute } from '../shared/display';
import { problemMessage, whenAnalysed } from '../shared/problem';
import { AreaPage } from './area/area-page';
import { FactoryDeletion } from './factory-deletion';
import { FindingsSummary } from './findings-summary';
import { GameIcon } from '../shared/game-icon';
import { loaded } from '../shared/loaded';

/**
 * A factory's findings in one snapshot: the overview of its machines and power, the factory in 3D with all the 3D view
 * can do, then what comes in (BR-A19) and what leaves: what it makes now and at 100%, where that goes, what its
 * receivers would take, what's sunk and what can't leave (BR-A18).
 */
@Component({
  selector: 'app-factory-page',
  imports: [
    GameIcon,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    MatTooltipModule,
    FindingsSummary,
    AreaPage,
  ],
  templateUrl: './factory-page.html',
  styles: `
    .output {
      margin-bottom: 24px;
    }
    .destinations {
      display: flex;
      flex-wrap: wrap;
      gap: 4px 12px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .destinations li {
      display: inline-flex;
      gap: 4px;
    }
    .muted {
      color: var(--mat-sys-on-surface-variant);
    }
    .power {
      margin: 0 0 16px;
    }
    app-area-page {
      display: block;
      margin-bottom: 24px;
    }
    .short {
      color: var(--mat-sys-error);
    }
    .error {
      color: var(--mat-sys-error);
    }
    .name {
      flex: 1;
      min-width: 0;
      padding: 4px 8px;
      border: 1px solid var(--mat-sys-outline);
      border-radius: 8px;
      background: transparent;
      color: inherit;
      font: var(--mat-sys-headline-small);
    }

    // What comes in and what leaves, at the top of the 3D view's Info tab (asked 2026-11-23). It's drawn there, but the
    // template is this page's, so these styles reach it.
    .totals {
      padding-bottom: 12px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);

      h3 {
        margin: 4px 0;
        font: var(--mat-sys-title-small);
      }

      ul {
        display: grid;
        gap: 4px;
        margin: 0 0 8px;
        padding: 0;
        list-style: none;
      }

      .rate {
        margin-left: 4px;
        font-weight: 600;
      }
    }
  `,
})
export class FactoryPage {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly deletion = inject(FactoryDeletion);
  protected readonly names = inject(CatalogNames);

  /** From the route: /snapshots/:id/factories/:factoryId. */
  readonly id = input.required<string>();
  readonly factoryId = input.required<string>();

  protected readonly analysis = resource({
    params: () => ({ id: this.id(), factoryId: this.factoryId() }),
    // A link can arrive before the analysis is ready: then the API answers 202 until it is.
    loader: ({ params, abortSignal }) =>
      whenAnalysed(() => this.api.invoke$Response(analyseFactory, params), abortSignal),
  });

  protected readonly outputColumns = [
    'item',
    'now',
    'atFullSpeed',
    'receiversNeed',
    'othersBring',
    'short',
    'sunk',
    'cannotLeave',
    'destinations',
  ];

  private readonly destinationLabels: Record<OutputDestinationKind, string> = {
    factory: 'Factory',
    noFactory: 'Machines of no factory',
    sink: 'AWESOME Sink',
    consumer: 'Consumer',
    storage: 'Storage and stations',
    nowhere: 'A belt or pipe that goes nowhere',
  };

  // Material's table rows are untyped (let o is any) in the template: these methods type them.
  protected readonly inputColumns = ['item', 'now', 'atFullSpeed', 'short', 'sources'];

  private readonly sourceLabels: Record<InputSourceKind, string> = {
    factory: 'Factory',
    noFactory: 'Machines of no factory',
    building: 'Building',
    nowhere: 'A belt or pipe from nowhere',
  };

  protected sources(input: FactoryInput): InputSource[] {
    return input.sources;
  }

  protected sourceLabel(source: InputSource): string {
    if (source.building) {
      return this.names.building(source.building);
    }
    return source.factory ?? this.sourceLabels[source.kind];
  }

  protected destinations(output: FactoryOutput): OutputDestination[] {
    return output.destinations;
  }

  protected destinationLabel(destination: OutputDestination): string {
    if (destination.building) {
      return this.names.building(destination.building);
    }
    return destination.factory ?? this.destinationLabels[destination.kind];
  }

  /** The factory in 3D: its Edit button edits it there. */
  protected readonly area = viewChild<AreaPage>('area');

  /** After asking; then there's nothing left to show of it, so back to the map. */
  protected async delete(): Promise<void> {
    const name = loaded(this.analysis)?.name;
    if (!name || !(await this.deletion.confirmAndDelete({ factoryId: this.factoryId(), name })))
      return;
    await this.router.navigate(['/snapshots', this.id(), 'map']);
  }

  protected readonly perMinute = perMinute;
  protected readonly megawatts = megawatts;
  protected readonly problem = problemMessage;
  protected readonly loaded = loaded;
}
