import { HttpErrorResponse } from '@angular/common/http';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { Api } from '../../api/api';
import { MapArt } from './map-art';

describe("the game's map picture, for every map", () => {
  /** The picture as the service has it once the Catalog answered: a loading resource keeps the app unstable. */
  async function load(answer: () => Promise<unknown>) {
    TestBed.configureTestingModule({ providers: [{ provide: Api, useValue: { invoke: answer } }] });
    const art = TestBed.inject(MapArt).art;
    await TestBed.inject(ApplicationRef).whenStable();
    return art.value();
  }

  it('is the picture the Catalog has', async () => {
    const picture = { gameVersion: '1.2', x0: 0, y0: 0, x1: 1, y1: 1, tiles: [] };

    expect(await load(() => Promise.resolve(picture))).toEqual(picture);
  });

  it('is none before it is imported, when the Catalog answers 404', async () => {
    expect(await load(() => Promise.reject(new HttpErrorResponse({ status: 404 })))).toBeNull();
  });
});
