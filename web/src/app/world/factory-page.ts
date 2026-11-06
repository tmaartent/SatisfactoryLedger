import { Component, inject, input, resource } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';

import { Api } from '../api/api';
import { analyseFactory } from '../api/fn/world/analyse-factory';
import { CatalogNames } from '../shared/catalog-names';
import { perMinute } from '../shared/display';
import { problemMessage, whenAnalysed } from '../shared/problem';
import { FindingsView } from './findings';

/** A factory's findings in one snapshot: what it makes now and at 100%, and why not more (BR-A14). */
@Component({
  selector: 'app-factory-page',
  imports: [RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, MatTableModule, FindingsView],
  templateUrl: './factory-page.html',
  styles: `
    .output { max-width: 640px; margin-bottom: 24px; }
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

  protected readonly outputColumns = ['item', 'now', 'atFullSpeed'];
  protected readonly perMinute = perMinute;
  protected readonly problem = problemMessage;
}
