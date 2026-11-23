import { TestBed } from '@angular/core/testing';

import { Api } from '../api/api';
import { importIcons } from '../api/fn/catalog/import-icons';
import { importMapArt } from '../api/fn/catalog/import-map-art';
import { CatalogNames } from '../shared/catalog-names';
import { CatalogPage } from './catalog-page';

// What the API answers to a map art import: every count there, all zero; a map art import changes no entries.
const none = { added: 0, updated: 0, noLongerInGame: 0 };
const mapArtImport = {
  id: 'import-1',
  source: 'mapArt',
  gameVersion: '1.2',
  importedAt: '2026-11-20T00:00:00Z',
  noLongerInGame: [],
  ...Object.fromEntries(
    [
      'buildables',
      'buildings',
      'dronePorts',
      'extractors',
      'footprints',
      'generators',
      'items',
      'recipes',
      'resourceNodes',
      'storages',
      'transportTiers',
    ].map((kind) => [kind, none]),
  ),
};

describe('the catalog page', () => {
  async function render() {
    const api = {
      invoke: vi.fn((fn: unknown) =>
        Promise.resolve(fn === importMapArt ? mapArtImport : fn === importIcons ? { ...mapArtImport, source: 'icons' } : []),
      ),
    };
    TestBed.configureTestingModule({
      imports: [CatalogPage],
      providers: [
        { provide: Api, useValue: api },
        {
          provide: CatalogNames,
          useValue: { icon: () => null, iconOfItemNamed: () => null,
            reload: vi.fn(),
            item: (item: string) => item,
            building: (name: string) => name,
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(CatalogPage);
    await fixture.whenStable();
    return { fixture, api, element: fixture.nativeElement as HTMLElement };
  }

  it("imports the game's map picture the map tool wrote", async () => {
    const { fixture, api, element } = await render();
    const input = element.querySelector('input[type="file"].map-art') as HTMLInputElement;
    const file = new File(['{}'], 'map-art.json');
    Object.defineProperty(input, 'files', { value: { 0: file, length: 1, item: () => file } });

    input.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(api.invoke).toHaveBeenCalledWith(importMapArt, { body: { file } });
  });

  it("imports the game's icons the map tool wrote, and shows them from then on", async () => {
    const { fixture, api, element } = await render();
    const input = element.querySelector('input[type="file"].icons') as HTMLInputElement;
    const file = new File(['{}'], 'icons.json');
    Object.defineProperty(input, 'files', { value: { 0: file, length: 1, item: () => file } });

    input.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(api.invoke).toHaveBeenCalledWith(importIcons, { body: { file } });
    expect(TestBed.inject(CatalogNames).reload).toHaveBeenCalled();
  });
});
