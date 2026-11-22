import { DeferBlockState, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { App } from './app';
import { CatalogNames } from './shared/catalog-names';
import { Assistant } from './world/explain/assistant';
import { Explainer } from './world/explain/explainer';

describe('App', () => {
  async function render() {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        { provide: Assistant, useValue: { models: () => Promise.resolve([]), explain: () => new Promise(() => undefined) } },
        { provide: CatalogNames, useValue: { building: () => 'Constructor', recipe: () => 'Iron Plate' } },
      ],
    });
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    return fixture;
  }

  it('offers the world and the catalog', async () => {
    const fixture = await render();

    const links = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('nav a')).map((a) => a.textContent?.trim());
    expect(links).toEqual(['World', 'Catalog']);
  });

  it('shows the explanation asked beside the page, and none before', async () => {
    const fixture = await render();
    const panel = () => (fixture.nativeElement as HTMLElement).querySelector('.shell > app-explain-panel');
    expect(panel()).toBeNull();

    TestBed.inject(Explainer).explain({ className: 'Build_ConstructorMk1_C', recipe: 'Recipe_IronPlate_C', limit: 'starved' }, 's1');
    await fixture.whenStable();
    // The panel's code loads with the first question (@defer): load it now.
    const [deferred] = await fixture.getDeferBlocks();
    await deferred.render(DeferBlockState.Complete);

    expect(panel()?.textContent).toContain('Why are the Constructor (Iron Plate) machines starved?');
  });
});
