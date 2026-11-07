import { Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';

import { Api } from '../api/api';
import { getExtractors } from '../api/fn/catalog/get-extractors';
import { getItems } from '../api/fn/catalog/get-items';
import { getRecipes } from '../api/fn/catalog/get-recipes';
import { getResourceNodes } from '../api/fn/catalog/get-resource-nodes';
import { getTransportTiers } from '../api/fn/catalog/get-transport-tiers';
import { importCatalog } from '../api/fn/catalog/import-catalog';
import { importResourceNodes } from '../api/fn/catalog/import-resource-nodes';
import { ImportResponse } from '../api/models/import-response';
import { CatalogNames } from '../shared/catalog-names';
import { perMinute, positionInMetres } from '../shared/display';
import { problemMessage } from '../shared/problem';

/** The game data (ADR-0005) and the resource nodes (ADR-0015): their imports, and what's in them. */
@Component({
  selector: 'app-catalog-page',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTableModule,
    MatTabsModule,
  ],
  templateUrl: './catalog-page.html',
  styleUrl: './catalog-page.scss',
})
export class CatalogPage {
  private readonly api = inject(Api);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly names = inject(CatalogNames);

  protected readonly items = resource({ loader: () => this.api.invoke(getItems) });
  protected readonly recipes = resource({ loader: () => this.api.invoke(getRecipes) });
  protected readonly tiers = resource({ loader: () => this.api.invoke(getTransportTiers) });
  protected readonly extractors = resource({ loader: () => this.api.invoke(getExtractors) });
  protected readonly nodes = resource({ loader: () => this.api.invoke(getResourceNodes) });

  protected readonly gameVersion = signal('1.2');
  protected readonly importing = signal(false);
  protected readonly lastImport = signal<ImportResponse | null>(null);
  protected readonly search = signal('');

  /** What the last import changed, per kind. */
  protected readonly importCounts = computed(() => {
    const i = this.lastImport();
    if (!i) return [];
    return [
      { kind: 'Items', counts: i.items },
      { kind: 'Recipes', counts: i.recipes },
      { kind: 'Buildings', counts: i.buildings },
      { kind: 'Extractors', counts: i.extractors },
      { kind: 'Transport tiers', counts: i.transportTiers },
      { kind: 'Footprints', counts: i.footprints },
      { kind: 'Generators', counts: i.generators },
      { kind: 'Drone ports', counts: i.dronePorts },
      { kind: 'Resource nodes', counts: i.resourceNodes },
    ];
  });

  private readonly matches = (name: string) => name.toLowerCase().includes(this.search().trim().toLowerCase());
  protected readonly shownItems = computed(() => (this.items.value() ?? []).filter((item) => this.matches(item.name)));
  protected readonly shownRecipes = computed(() => (this.recipes.value() ?? []).filter((recipe) => this.matches(recipe.name)));
  protected readonly shownNodes = computed(() =>
    (this.nodes.value() ?? []).filter((node) => this.matches(this.names.item(node.resource)) || this.matches(node.name)),
  );

  protected readonly itemColumns = ['name', 'form', 'raw', 'id'];
  protected readonly recipeColumns = ['name', 'building', 'inputs', 'outputs'];
  protected readonly tierColumns = ['name', 'form', 'capacity'];
  protected readonly extractorColumns = ['name', 'form', 'rate'];
  protected readonly nodeColumns = ['resource', 'purity', 'kind', 'position'];

  protected readonly perMinute = perMinute;
  protected readonly position = positionInMetres;

  protected async importDocs(files: FileList | null): Promise<void> {
    const file = files?.item(0);
    if (file) await this.run(() => this.api.invoke(importCatalog, { body: { file, gameVersion: this.gameVersion() } }));
  }

  protected async importNodes(files: FileList | null): Promise<void> {
    const file = files?.item(0);
    if (file) await this.run(() => this.api.invoke(importResourceNodes, { body: { file } }));
  }

  private async run(importing: () => Promise<ImportResponse>): Promise<void> {
    this.importing.set(true);
    try {
      this.lastImport.set(await importing());
      for (const list of [this.items, this.recipes, this.tiers, this.extractors, this.nodes]) list.reload();
      this.names.reload();
    } catch (error) {
      this.snackBar.open(`The import failed: ${problemMessage(error)}`, 'Close');
    } finally {
      this.importing.set(false);
    }
  }
}
