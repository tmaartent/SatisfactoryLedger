import { Injectable, inject, resource } from '@angular/core';

import { Api } from '../api/api';
import { getBuildables } from '../api/fn/catalog/get-buildables';
import { getExtractors } from '../api/fn/catalog/get-extractors';
import { getItems } from '../api/fn/catalog/get-items';
import { getRecipes } from '../api/fn/catalog/get-recipes';
import { ItemForm } from '../api/models/item-form';
import { ItemResponse } from '../api/models/item-response';
import { classNameAsWords } from './display';

/**
 * The catalog's names for items, recipes and buildings, loaded once, with fluids' colours and what extractors take. The
 * analysis speaks in class names (Desc_IronPlate_C, Build_TrainDockingStation_C); the pages show the game's names (Iron
 * Plate, Freight Platform).
 */
@Injectable({ providedIn: 'root' })
export class CatalogNames {
  private readonly api = inject(Api);

  private readonly catalog = resource({
    loader: async () => {
      const [items, recipes, buildables, extractors] = await Promise.all([
        this.api.invoke(getItems),
        this.api.invoke(getRecipes),
        this.api.invoke(getBuildables),
        this.api.invoke(getExtractors),
      ]);
      return {
        items,
        itemNames: new Map(items.map((item) => [item.id, item.name])),
        fluidColours: new Map(items.map((item) => [item.id, item.fluidColour ?? null])),
        extractorForms: new Map(extractors.map((extractor) => [extractor.id, extractor.form])),
        recipeNames: new Map(recipes.map((recipe) => [recipe.id, recipe.name])),
        buildingNames: new Map(buildables.map((buildable) => [buildable.id, buildable.name])),
      };
    },
  });

  /** Every item in the catalog, by name. Empty until the catalog is loaded. */
  items(): ItemResponse[] {
    return [...(this.catalog.value()?.items ?? [])].sort((a, b) => a.name.localeCompare(b.name));
  }

  item(id: string | null | undefined): string {
    if (!id) return '—';
    return this.catalog.value()?.itemNames.get(id) ?? classNameAsWords(id);
  }

  recipe(id: string | null | undefined): string {
    if (!id) return '—';
    return this.catalog.value()?.recipeNames.get(id) ?? classNameAsWords(id);
  }

  /** The game's name for a building; one the catalog doesn't know, its class name as words. */
  building(className: string): string {
    return this.catalog.value()?.buildingNames.get(className) ?? classNameAsWords(className);
  }

  /** A fluid's colour in the game, "#rrggbb"; null for a solid, or one the catalog has none for. */
  fluidColour(id: string): string | null {
    return this.catalog.value()?.fluidColours.get(id) ?? null;
  }

  /** What an extractor building takes from a node, solid (a miner) or fluid; null for a building that isn't one. */
  extractorForm(className: string): ItemForm | null {
    return this.catalog.value()?.extractorForms.get(className) ?? null;
  }

  /** After a new catalog import. */
  reload(): void {
    this.catalog.reload();
  }
}
