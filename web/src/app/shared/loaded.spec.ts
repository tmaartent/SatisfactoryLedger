import { ApplicationRef, Injector, resource } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { loaded } from './loaded';

describe('loaded', () => {
  async function settled<T>(loader: () => Promise<T>) {
    const loading = resource({ loader, injector: TestBed.inject(Injector) });
    await TestBed.inject(ApplicationRef).whenStable();
    return loading;
  }

  it('gives what a resource loaded', async () => {
    expect(loaded(await settled(() => Promise.resolve(['Development'])))).toEqual(['Development']);
  });

  it('gives nothing while it loads', () => {
    const loading = resource({
      loader: () => new Promise<string[]>(() => undefined),
      injector: TestBed.inject(Injector),
    });

    expect(loaded(loading)).toBeUndefined();
  });

  it('gives nothing when it failed to load, where its value throws', async () => {
    const failed = await settled<string[]>(() => Promise.reject(new Error('503')));

    expect(() => failed.value()).toThrow();
    expect(loaded(failed)).toBeUndefined();
  });
});
