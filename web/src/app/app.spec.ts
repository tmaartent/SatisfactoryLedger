import { Component } from '@angular/core';
import { DeferBlockState, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { Api } from './api/api';
import { getSnapshots } from './api/fn/world/get-snapshots';
import { App } from './app';
import { CatalogNames } from './shared/catalog-names';
import { Assistant } from './world/explain/assistant';
import { Explainer } from './world/explain/explainer';

@Component({ template: '' })
class EmptyPage {}

describe('App', () => {
  beforeEach(() => localStorage.clear());

  async function render(url = '/snapshots/s1') {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([
          { path: 'snapshots', component: EmptyPage },
          { path: 'snapshots/:id', component: EmptyPage },
          { path: 'snapshots/:id/map', component: EmptyPage },
          { path: 'snapshots/:id/arrangement', component: EmptyPage },
          { path: 'snapshots/:id/factories/:factoryId', component: EmptyPage },
          { path: 'catalog', component: EmptyPage },
        ]),
        // The snapshots the picker above the sidebar lists.
        {
          provide: Api,
          useValue: {
            invoke: (fn: unknown) =>
              Promise.resolve(fn === getSnapshots ? [{ id: 's1', sessionName: 'Learning' }] : []),
          },
        },
        {
          provide: Assistant,
          useValue: {
            models: () => Promise.resolve([]),
            explain: () => new Promise(() => undefined),
          },
        },
        {
          provide: CatalogNames,
          useValue: {
            icon: () => null,
            iconOfItemNamed: () => null,
            building: () => 'Constructor',
            recipe: () => 'Iron Plate',
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(App);
    await TestBed.inject(Router).navigateByUrl(url);
    await fixture.whenStable();
    const element: HTMLElement = fixture.nativeElement;
    return { fixture, element, sidebar: () => element.querySelector('.shell > app-sidebar') };
  }

  /** The Explanation tab's code loads when it's first shown (@defer): load it now. */
  async function loadExplanation(fixture: Awaited<ReturnType<typeof render>>['fixture']) {
    const [deferred] = await fixture.getDeferBlocks();
    await deferred.render(DeferBlockState.Complete);
  }

  it('offers the world and the catalog', async () => {
    const { element } = await render();

    const links = Array.from(element.querySelectorAll('mat-toolbar nav a')).map((a) =>
      a.textContent?.trim(),
    );
    expect(links).toEqual(['World', 'Catalog']);
  });

  it('has the sidebar on a World page, with the Explanation tab saying where to ask', async () => {
    const { fixture, sidebar } = await render();

    await loadExplanation(fixture);

    expect(sidebar()?.querySelector('.rail button')?.getAttribute('aria-label')).toBe(
      'Explanation',
    );
    expect(sidebar()?.textContent).toContain(
      'Ask why a starved or output-blocked machine runs as it does',
    );
  });

  it('shows the question asked in the Explanation tab', async () => {
    const { fixture, sidebar } = await render();

    TestBed.inject(Explainer).explain(
      { className: 'Build_ConstructorMk1_C', recipe: 'Recipe_IronPlate_C', limit: 'starved' },
      's1',
    );
    await fixture.whenStable();
    await loadExplanation(fixture);

    expect(sidebar()?.textContent).toContain(
      'Why are the Constructor (Iron Plate) machines starved?',
    );
  });

  it('goes back to the world on the map of the snapshot last looked at', async () => {
    const { fixture, element } = await render('/snapshots/s1/map');
    await TestBed.inject(Router).navigateByUrl('/catalog');
    await fixture.whenStable();

    expect(element.querySelector('mat-toolbar nav a')?.getAttribute('href')).toBe(
      '/snapshots/s1/map',
    );
  });

  it('goes to the world with no snapshot before one is looked at', async () => {
    const { element } = await render('/catalog');

    expect(element.querySelector('mat-toolbar nav a')?.getAttribute('href')).toBe('/snapshots');
  });

  it("has the snapshot picker above the sidebar's tabs on a World page, naming the snapshot shown", async () => {
    const { fixture, sidebar } = await render('/snapshots/s1/map');
    // Its code loads once the page is up (@defer): load it now.
    for (const deferred of await fixture.getDeferBlocks())
      await deferred.render(DeferBlockState.Complete);
    await fixture.whenStable();

    const picker = sidebar()?.querySelector('app-snapshot-picker');
    expect(picker?.closest('.tab')).toBeNull();
    expect(picker?.querySelector('button.current')?.textContent).toContain('Learning');
  });

  it('has no snapshot picker on the optimal arrangement, which is about the snapshot it was opened from', async () => {
    const { fixture, sidebar } = await render('/snapshots/s1/arrangement');
    for (const deferred of await fixture.getDeferBlocks())
      await deferred.render(DeferBlockState.Complete);
    await fixture.whenStable();

    expect(sidebar()?.querySelector('app-snapshot-picker') ?? null).toBeNull();
  });

  it('has no Explanation tab on the optimal arrangement, which has no findings to explain (asked 2026-11-25)', async () => {
    const { sidebar } = await render('/snapshots/s1/arrangement');

    expect(sidebar()?.querySelector('button[aria-label="Explanation"]') ?? null).toBeNull();
  });

  it("has no snapshot picker on a factory's page, which is about one factory of its snapshot", async () => {
    const { fixture, sidebar } = await render('/snapshots/s1/factories/f1');
    for (const deferred of await fixture.getDeferBlocks())
      await deferred.render(DeferBlockState.Complete);
    await fixture.whenStable();

    expect(sidebar()?.querySelector('app-snapshot-picker')).toBeNull();
  });

  it('has no sidebar in the Catalog, which has no tabs', async () => {
    const { sidebar } = await render('/catalog');

    expect(sidebar()).toBeNull();
  });
});
