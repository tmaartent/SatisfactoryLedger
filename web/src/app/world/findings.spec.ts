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
  generators: [
    {
      id: 'plant',
      className: 'Build_GeneratorNuclear_C',
      fuel: 'Desc_NuclearFuelRod_C',
      utilisation: 0.5,
      limit: 'starved',
      limitingItem: 'Desc_Water_C',
      position: null,
      approximate: false,
    },
  ],
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

  it('lists each generator with its fuel and why it runs below full power', async () => {
    const fixture = await render();
    const generatorsTab = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('[role="tab"]')).find((tab) =>
      tab.textContent?.includes('Generators'),
    ) as HTMLElement;

    generatorsTab.click();
    await fixture.whenStable();

    // Only the open tab: the machines tab's rows may still be in the page.
    const shown = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.mat-mdc-tab-body-active tr.mat-mdc-row')).map(
      (row) => row.textContent ?? '',
    );
    expect(shown).toHaveLength(1);
    expect(shown[0]).toContain('Nuclear Fuel Rod');
    expect(shown[0]).toContain('Not enough Water');
  });
});
