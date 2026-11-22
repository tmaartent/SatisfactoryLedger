import { Component } from '@angular/core';
import { DeferBlockState, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { App } from './app';
import { CatalogNames } from './shared/catalog-names';
import { Assistant } from './world/explain/assistant';
import { Explainer } from './world/explain/explainer';

@Component({ template: '' })
class EmptyPage {}

describe('App', () => {
  async function render(url = '/snapshots/s1') {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([
          { path: 'snapshots/:id', component: EmptyPage },
          { path: 'catalog', component: EmptyPage },
        ]),
        { provide: Assistant, useValue: { models: () => Promise.resolve([]), explain: () => new Promise(() => undefined) } },
        { provide: CatalogNames, useValue: { building: () => 'Constructor', recipe: () => 'Iron Plate' } },
      ],
    });
    const fixture = TestBed.createComponent(App);
    await TestBed.inject(Router).navigateByUrl(url);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    return { fixture, element, sidebar: () => element.querySelector('.shell > app-sidebar') };
  }

  /** The Explanation tab's code loads when it's first shown (@defer): load it now. */
  async function loadExplanation(fixture: Awaited<ReturnType<typeof render>>['fixture']) {
    const [deferred] = await fixture.getDeferBlocks();
    await deferred.render(DeferBlockState.Complete);
  }

  it('offers the world and the catalog', async () => {
    const { element } = await render();

    const links = Array.from(element.querySelectorAll('mat-toolbar nav a')).map((a) => a.textContent?.trim());
    expect(links).toEqual(['World', 'Catalog']);
  });

  it('has the sidebar on a World page, with the Explanation tab saying where to ask', async () => {
    const { fixture, sidebar } = await render();

    await loadExplanation(fixture);

    expect(sidebar()?.querySelector('.rail button')?.getAttribute('aria-label')).toBe('Explanation');
    expect(sidebar()?.textContent).toContain('Ask why a starved or output-blocked machine runs as it does');
  });

  it('shows the question asked in the Explanation tab', async () => {
    const { fixture, sidebar } = await render();

    TestBed.inject(Explainer).explain({ className: 'Build_ConstructorMk1_C', recipe: 'Recipe_IronPlate_C', limit: 'starved' }, 's1');
    await fixture.whenStable();
    await loadExplanation(fixture);

    expect(sidebar()?.textContent).toContain('Why are the Constructor (Iron Plate) machines starved?');
  });

  it('has no sidebar in the Catalog, which has no tabs', async () => {
    const { sidebar } = await render('/catalog');

    expect(sidebar()).toBeNull();
  });
});
