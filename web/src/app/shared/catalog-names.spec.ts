import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { Api } from '../api/api';
import { getBuildables } from '../api/fn/catalog/get-buildables';
import { getItems } from '../api/fn/catalog/get-items';
import { getRecipes } from '../api/fn/catalog/get-recipes';
import { CatalogNames } from './catalog-names';

describe('CatalogNames', () => {
  async function loaded() {
    const answers = new Map<unknown, unknown>([
      [getItems, []],
      [getRecipes, []],
      [getBuildables, [{ id: 'Build_TrainDockingStation_C', name: 'Freight Platform', inGame: true }]],
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
});
