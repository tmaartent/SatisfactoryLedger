import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, resource } from '@angular/core';

import { Api } from '../api/api';
import { getBuildables } from '../api/fn/catalog/get-buildables';
import { getExtractors } from '../api/fn/catalog/get-extractors';
import { getIcons } from '../api/fn/catalog/get-icons';
import { getItems } from '../api/fn/catalog/get-items';
import { getRecipes } from '../api/fn/catalog/get-recipes';
import { ItemForm } from '../api/models/item-form';
import { ItemResponse } from '../api/models/item-response';
import { classNameAsWords } from './display';

/**
 * The catalog's names for items, recipes and buildings, loaded once, with fluids' colours, what extractors take and the
 * game's icons (ADR-0032). The
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

  /** The descriptors that have an icon: none before the icons are imported (404), and nothing else waits on them. */
  private readonly icons = resource({
    loader: async () => {
      try {
        return new Set((await this.api.invoke(getIcons)).descriptors);
      } catch (error) {
        if (error instanceof HttpErrorResponse && error.status === 404) return new Set<string>();
        throw error;
      }
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

  /**
   * The game's icon of an item, a resource or a building, as a URL; null when there's none, or before the icons are
   * imported. A building shows its descriptor's icon, which the API looks up (IconSet.DescriptorOf): named alike,
   * Build_X_C and Desc_X_C.
   */
  icon(className: string | null | undefined): string | null {
    if (!className || !this.icons.hasValue()) return null;
    const descriptor = className.startsWith('Build_') ? `Desc_${className.slice('Build_'.length)}` : className;
    return this.icons.value().has(descriptor) ? `/api/v1/catalog/icons/${encodeURIComponent(className)}` : null;
  }

  /** An item's icon by its name, as a trace names items ("Iron Ore"); null when no item is named so, or it has none. */
  iconOfItemNamed(name: string): string | null {
    const item = this.catalog.value()?.items.find((candidate) => candidate.name === name);
    return item ? this.icon(item.id) : null;
  }

  /** After a new catalog import, or a new import of the icons. */
  reload(): void {
    this.catalog.reload();
    this.icons.reload();
  }
}
