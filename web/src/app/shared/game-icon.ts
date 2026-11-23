import { Component, computed, inject, input } from '@angular/core';

import { CatalogNames } from './catalog-names';

/**
 * The game's icon of an item, a resource or a building (ADR-0032), small, beside its name: decoration, so no alt text,
 * the name says it. Nothing when it has none, or before the icons are imported.
 */
@Component({
  selector: 'app-game-icon',
  template: `@if (src(); as src) {
    <img [src]="src" [attr.width]="size()" [attr.height]="size()" alt="" loading="lazy" />
  }`,
  styles: `
    :host {
      display: inline-flex;
      vertical-align: middle;
    }

    img {
      margin-right: 4px;
      object-fit: contain;
    }
  `,
})
export class GameIcon {
  private readonly names = inject(CatalogNames);

  /** The class name: Desc_IronPlate_C, or a building's Build_ConstructorMk1_C. */
  readonly of = input<string | null>();
  /** Or an item's name, as a trace names items. */
  readonly itemNamed = input<string | null>();
  /** Or a building's name, as a trace names buildings. */
  readonly buildingNamed = input<string | null>();
  readonly size = input(20);

  protected readonly src = computed(() => {
    const [item, building] = [this.itemNamed(), this.buildingNamed()];
    return item
      ? this.names.iconOfItemNamed(item)
      : building
        ? this.names.iconOfBuildingNamed(building)
        : this.names.icon(this.of());
  });
}
