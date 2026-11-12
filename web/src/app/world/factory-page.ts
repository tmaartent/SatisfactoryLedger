import { Component, inject, input, resource } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';

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
import { FindingsView } from './findings';

/**
 * A factory's findings in one snapshot: what it makes now and at 100%, where that goes, what its receivers would take,
 * what's sunk and what can't leave (BR-A18), its generators' power, and why not more (BR-A14).
 */
@Component({
  selector: 'app-factory-page',
  imports: [RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, MatTableModule, MatTooltipModule, FindingsView],
  templateUrl: './factory-page.html',
  styles: `
    .output { margin-bottom: 24px; }
    .destinations { display: flex; flex-wrap: wrap; gap: 4px 12px; margin: 0; padding: 0; list-style: none; }
    .destinations li { display: inline-flex; gap: 4px; }
    .muted { color: var(--mat-sys-on-surface-variant); }
    .power { margin-bottom: 24px; }
    .short { color: var(--mat-sys-error); }
  `,
})
export class FactoryPage {
  private readonly api = inject(Api);
  protected readonly names = inject(CatalogNames);

  /** From the route: /snapshots/:id/factories/:factoryId. */
  readonly id = input.required<string>();
  readonly factoryId = input.required<string>();

  protected readonly analysis = resource({
    params: () => ({ id: this.id(), factoryId: this.factoryId() }),
    // A link can arrive before the analysis is ready: then the API answers 202 until it is.
    loader: ({ params, abortSignal }) => whenAnalysed(() => this.api.invoke$Response(analyseFactory, params), abortSignal),
  });

  protected readonly outputColumns = ['item', 'now', 'atFullSpeed', 'receiversNeed', 'othersBring', 'short', 'sunk', 'cannotLeave', 'destinations'];

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


  protected readonly perMinute = perMinute;
  protected readonly megawatts = megawatts;
  protected readonly problem = problemMessage;
}
