import { Injectable, inject, resource } from '@angular/core';

import { Api } from '../api/api';
import { getItems } from '../api/fn/catalog/get-items';
import { getRecipes } from '../api/fn/catalog/get-recipes';
import { ItemResponse } from '../api/models/item-response';
import { classNameAsWords } from './display';

/**
 * The catalog's names for items and recipes, loaded once. The analysis speaks in class names
 * (Desc_IronPlate_C); the pages show the game's names (Iron Plate).
 */
@Injectable({ providedIn: 'root' })
export class CatalogNames {
  private readonly api = inject(Api);

  private readonly catalog = resource({
    loader: async () => {
      const [items, recipes] = await Promise.all([this.api.invoke(getItems), this.api.invoke(getRecipes)]);
      return {
        items,
        itemNames: new Map(items.map((item) => [item.id, item.name])),
        recipeNames: new Map(recipes.map((recipe) => [recipe.id, recipe.name])),
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

  /** Buildings have no names in the API yet: their class name, as words. */
  building(className: string): string {
    return classNameAsWords(className);
  }

  /** After a new catalog import. */
  reload(): void {
    this.catalog.reload();
  }
}
