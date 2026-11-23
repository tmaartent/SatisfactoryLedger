import { Component } from '@angular/core';
import { TemplateRef } from '@angular/core';
import { DeferBlockState, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { App } from './app';
import { SidebarTabs } from './sidebar/sidebar-tabs';
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
          { path: 'catalog', component: EmptyPage },
        ]),
        { provide: Assistant, useValue: { models: () => Promise.resolve([]), explain: () => new Promise(() => undefined) } },
        { provide: CatalogNames, useValue: { icon: () => null, iconOfItemNamed: () => null, building: () => 'Constructor', recipe: () => 'Iron Plate' } },
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

  it('goes back to the world on the map of the snapshot last looked at', async () => {
    const { fixture, element } = await render('/snapshots/s1/map');
    await TestBed.inject(Router).navigateByUrl('/catalog');
    await fixture.whenStable();

    expect(element.querySelector('mat-toolbar nav a')?.getAttribute('href')).toBe('/snapshots/s1/map');
  });

  it('goes to the world with no snapshot before one is looked at', async () => {
    const { element } = await render('/catalog');

    expect(element.querySelector('mat-toolbar nav a')?.getAttribute('href')).toBe('/snapshots');
  });

  it('shows the snapshots in the sidebar when going back to the world', async () => {
    const { fixture, element } = await render('/catalog');
    const tabs = TestBed.inject(SidebarTabs);

    (element.querySelector('mat-toolbar nav a') as HTMLElement).click();
    await fixture.whenStable();
    // The map's tabs, as it adds them.
    const template = {} as TemplateRef<unknown>;
    tabs.add({ key: 'info', label: 'Info', icon: 'info', order: -1, template });
    tabs.add({ key: 'snapshots', label: 'Snapshots', icon: 'folder_open', order: 1, template });

    expect(tabs.current()?.key).toBe('snapshots');
  });

  it('has no sidebar in the Catalog, which has no tabs', async () => {
    const { sidebar } = await render('/catalog');

    expect(sidebar()).toBeNull();
  });
});
