import { TestBed } from '@angular/core/testing';

import { MachineFinding } from '../api/models/machine-finding';
import { CatalogNames } from '../shared/catalog-names';
import { classNameAsWords } from '../shared/display';
import { Findings, FindingsView } from './findings';

const machine = (id: string, limit: MachineFinding['limit'], limitingItem: string | null = null): MachineFinding => ({
  id,
  className: 'Build_ConstructorMk1_C',
  recipe: 'Recipe_IronPlate_C',
  utilisation: limit === 'none' ? 1 : 0.5,
  limit,
  limitingItem,
  approximate: false,
  position: { x: 0, y: 0, z: 0 },
});

const findings: Findings = {
  // As the API sends them: every machine, the one running worst first.
  summary: { machines: 3, running: 1, starved: 1, outputBlocked: 1, noRecipe: 0 },
  machines: [machine('a', 'starved', 'Desc_IronIngot_C'), machine('b', 'outputBlocked', 'Desc_IronPlate_C'), machine('c', 'none')],
  bottlenecks: [],
  unusedSupply: [],
  unknownSupply: [],
  settled: true,
};

// The catalog's names, without the API: Iron Ingot for Desc_IronIngot_C.
const catalogNames: Partial<CatalogNames> = {
  item: (id) => (id ? classNameAsWords(id) : '—'),
  recipe: (id) => (id ? classNameAsWords(id) : '—'),
  building: classNameAsWords,
};

describe('FindingsView', () => {
  async function render() {
    TestBed.configureTestingModule({
      imports: [FindingsView],
      providers: [{ provide: CatalogNames, useValue: catalogNames }],
    });
    const fixture = TestBed.createComponent(FindingsView);
    fixture.componentRef.setInput('findings', findings);
    await fixture.whenStable();
    return fixture;
  }

  const rows = (element: HTMLElement) => Array.from(element.querySelectorAll('tr.mat-mdc-row')).map((row) => row.textContent ?? '');

  it('says why a starved machine runs below 100%, by the name of what it lacks', async () => {
    const fixture = await render();

    expect(rows(fixture.nativeElement)[0]).toContain('Not enough Iron Ingot');
  });

  async function filterOn(fixture: Awaited<ReturnType<typeof render>>, label: string) {
    const toggle = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('mat-button-toggle button')).find(
      (button) => button.textContent?.trim() === label,
    ) as HTMLButtonElement;
    toggle.click();
    await fixture.whenStable();
  }

  it('shows the running machines on their own, at 100% and without a cause', async () => {
    const fixture = await render();

    await filterOn(fixture, 'Running');

    const shown = rows(fixture.nativeElement);
    expect(shown).toHaveLength(1);
    expect(shown[0]).toContain('100%');
    expect(shown[0]).not.toContain("can't leave");
    expect(shown[0]).not.toContain('Not enough');
  });

  it('shows only the machines held back the way the filter says', async () => {
    const fixture = await render();

    await filterOn(fixture, 'Output blocked');

    const shown = rows(fixture.nativeElement);
    expect(shown).toHaveLength(1);
    expect(shown[0]).toContain("Iron Plate can't leave");
  });
});
