import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { Api } from '../api/api';
import { getBuildables } from '../api/fn/catalog/get-buildables';
import { getExtractors } from '../api/fn/catalog/get-extractors';
import { getItems } from '../api/fn/catalog/get-items';
import { getRecipes } from '../api/fn/catalog/get-recipes';
import { CatalogNames } from './catalog-names';

describe('CatalogNames', () => {
  async function loaded() {
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
        [{ id: 'Build_TrainDockingStation_C', name: 'Freight Platform', inGame: true }],
      ],
      [getExtractors, [{ id: 'Build_MinerMk3_C', name: 'Miner Mk.3', form: 'solid' }]],
    ]);
    const api = { invoke: vi.fn((fn: unknown) => Promise.resolve(answers.get(fn))) };
    TestBed.configureTestingModule({ providers: [{ provide: Api, useValue: api }] });
    const names = TestBed.inject(CatalogNames);
    names.building('Build_TrainDockingStation_C');
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
});
