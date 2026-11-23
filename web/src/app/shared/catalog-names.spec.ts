import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { HttpErrorResponse } from '@angular/common/http';

import { Api } from '../api/api';
import { getBuildables } from '../api/fn/catalog/get-buildables';
import { getExtractors } from '../api/fn/catalog/get-extractors';
import { getIcons } from '../api/fn/catalog/get-icons';
import { getItems } from '../api/fn/catalog/get-items';
import { getRecipes } from '../api/fn/catalog/get-recipes';
import { CatalogNames } from './catalog-names';

describe('CatalogNames', () => {
  async function loaded(iconsImported = true) {
    const answers = new Map<unknown, unknown>([
      [
        getItems,
        [
          { id: 'Desc_Water_C', name: 'Water', form: 'fluid', fluidColour: '#7ab0d4' },
          { id: 'Desc_OreIron_C', name: 'Iron Ore', form: 'solid', fluidColour: null },
        ],
      ],
      [getRecipes, []],
      [
        getBuildables,
        [{ id: 'Build_TrainDockingStation_C', name: 'Freight Platform', inGame: true }, { id: 'Build_MinerMk3_C', name: 'Miner Mk.3', inGame: true }],
      ],
      [getExtractors, [{ id: 'Build_MinerMk3_C', name: 'Miner Mk.3', form: 'solid' }]],
      [getIcons, { gameVersion: '1.2', descriptors: ['Desc_OreIron_C', 'Desc_MinerMk3_C'] }],
    ]);
    const api = {
      invoke: vi.fn((fn: unknown) =>
        fn === getIcons && !iconsImported
          ? // Before the icons are imported, the API has none.
            Promise.reject(new HttpErrorResponse({ status: 404 }))
          : Promise.resolve(answers.get(fn)),
      ),
    };
    TestBed.configureTestingModule({ providers: [{ provide: Api, useValue: api }] });
    const names = TestBed.inject(CatalogNames);
    names.building('Build_TrainDockingStation_C');
    names.icon('Desc_OreIron_C');
    await TestBed.inject(ApplicationRef).whenStable();
    return names;
  }

  it('names a building as the game does', async () => {
    const names = await loaded();

    expect(names.building('Build_TrainDockingStation_C')).toBe('Freight Platform');
  });

  it('names a building the catalog does not know by its class name, in words', async () => {
    const names = await loaded();

    expect(names.building('Build_SomethingNew_C')).toBe('Something New');
  });

  it('gives a fluid the colour the game gives it, and a solid none', async () => {
    const names = await loaded();

    expect(names.fluidColour('Desc_Water_C')).toBe('#7ab0d4');
    expect(names.fluidColour('Desc_OreIron_C')).toBeNull();
  });

  it('knows what an extractor takes from a node, and that a station is none', async () => {
    const names = await loaded();

    expect(names.extractorForm('Build_MinerMk3_C')).toBe('solid');
    expect(names.extractorForm('Build_TrainDockingStation_C')).toBeNull();
  });

  it("gives an item the game's icon", async () => {
    const names = await loaded();

    expect(names.icon('Desc_OreIron_C')).toBe('/api/v1/catalog/icons/Desc_OreIron_C');
  });

  it('gives a building the icon of the descriptor it is built from', async () => {
    const names = await loaded();

    expect(names.icon('Build_MinerMk3_C')).toBe('/api/v1/catalog/icons/Build_MinerMk3_C');
  });

  it('gives no icon for what the game has none for', async () => {
    const names = await loaded();

    expect(names.icon('Desc_Water_C')).toBeNull();
    expect(names.icon('Build_TrainDockingStation_C')).toBeNull();
  });

  it('gives no icons before they are imported, and still names everything', async () => {
    const names = await loaded(false);

    expect(names.icon('Desc_OreIron_C')).toBeNull();
    expect(names.building('Build_TrainDockingStation_C')).toBe('Freight Platform');
  });

  it("finds an item's icon by its name, as a trace names it", async () => {
    const names = await loaded();

    expect(names.iconOfItemNamed('Iron Ore')).toBe('/api/v1/catalog/icons/Desc_OreIron_C');
    expect(names.iconOfItemNamed('Water')).toBeNull();
  });

  it("finds a building's icon by its name, as a trace names it", async () => {
    const names = await loaded();

    expect(names.iconOfBuildingNamed('Miner Mk.3')).toBe('/api/v1/catalog/icons/Build_MinerMk3_C');
    expect(names.iconOfBuildingNamed('Freight Platform')).toBeNull();
  });
});
