import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { MachineFinding } from '../api/models/machine-finding';
import { CatalogNames } from '../shared/catalog-names';
import { classNameAsWords } from '../shared/display';
import { Findings, FindingsView } from './findings';

const machine = (id: string, limit: MachineFinding['limit'], limitingItem: string | null = null, onStock: string | null = null): MachineFinding => ({
  id,
  className: 'Build_ConstructorMk1_C',
  recipe: 'Recipe_IronPlate_C',
  utilisation: limit === 'none' ? 1 : 0.5,
  limit,
  limitingItem,
  approximate: false,
  position: { x: 0, y: 0, z: 0 },
  onStock,
  stockMinutes: onStock ? 123.4 : null,
  starvedOf: limitingItem && limit === 'starved' ? { [limitingItem]: 0.5 } : {},
  blockedOn: {},
});

const findings: Findings = {
  // As the API sends them: every machine, the one running worst first.
  summary: { machines: 3, running: 1, starved: 1, outputBlocked: 1, noRecipe: 0 },
  generatorSummary: { generators: 0, running: 0, starved: 0, outputBlocked: 0, fuelUnknown: 0 },
  machines: [machine('a', 'starved', 'Desc_IronIngot_C'), machine('b', 'outputBlocked', 'Desc_IronPlate_C'), machine('c', 'none', null, 'Desc_PlutoniumWaste_C')],
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
      starvedOf: { Desc_Water_C: 0.5 },
      blockedOn: {},
    },
  ],
  dronePorts: [
    {
      id: 'hub',
      className: 'Build_DroneStation_C',
      fuel: 'Desc_PackagedRocketFuel_C',
      burns: 4.4567,
      supplied: 1.25,
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
  async function render(shown: Findings = findings) {
    TestBed.configureTestingModule({
      imports: [FindingsView],
      providers: [{ provide: CatalogNames, useValue: catalogNames }, provideRouter([])],
    });
    const fixture = TestBed.createComponent(FindingsView);
    fixture.componentRef.setInput('findings', shown);
    fixture.componentRef.setInput('snapshotId', 's1');
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

  async function openTab(fixture: Awaited<ReturnType<typeof render>>, label: string): Promise<string[]> {
    const tab = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('[role="tab"]')).find((candidate) =>
      candidate.textContent?.includes(label),
    ) as HTMLElement;

    tab.click();
    await fixture.whenStable();

    // Only the open tab: the machines tab's rows may still be in the page.
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.mat-mdc-tab-body-active tr.mat-mdc-row')).map(
      (row) => row.textContent ?? '',
    );
  }

  const summaries = (element: HTMLElement) =>
    Array.from(element.querySelectorAll('.summary')).map((row) =>
      Array.from(row.querySelectorAll('mat-card')).map((card) => (card.textContent ?? '').replace(/(\d)(\D)/, '$1 $2').trim()),
    );

  it('counts the generators in a row of their own, next to the machines', async () => {
    const fixture = await render({
      ...findings,
      generatorSummary: { generators: 360, running: 300, starved: 60, outputBlocked: 0, fuelUnknown: 0 },
    });

    expect(summaries(fixture.nativeElement)[1]).toEqual(['360 generators', '300 at 100%', '60 starved', '0 output blocked', '0 fuel unknown']);
  });

  it('shows no generator row without generators', async () => {
    const fixture = await render();

    expect(summaries(fixture.nativeElement)).toHaveLength(1);
  });

  it('offers to delete a factory from the overview of factories', async () => {
    const fixture = await render();
    const plates = { factoryId: 'f1', name: 'Iron plates', traceable: true, machines: [], logistics: 0, outputs: [], inputs: [], missing: [] };
    fixture.componentRef.setInput('factories', [plates]);
    await fixture.whenStable();
    const deleted: string[] = [];
    fixture.componentInstance.deleteFactory.subscribe((factory) => deleted.push(factory.factoryId));

    await openTab(fixture, 'Factories');
    const button = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.mat-mdc-tab-body-active button')).find(
      (candidate) => candidate.getAttribute('aria-label') === 'Delete Iron plates',
    ) as HTMLButtonElement;
    button.click();

    expect(deleted).toEqual(['f1']);
  });

  it('lists each generator with its fuel and why it runs below full power', async () => {
    const fixture = await render();

    const shown = await openTab(fixture, 'Generators');

    expect(shown).toHaveLength(1);
    expect(shown[0]).toContain('Nuclear Fuel Rod');
    expect(shown[0]).toContain('Not enough Water');
  });

  it('lists each drone port with what its drones burn, what reaches it, and how much is short', async () => {
    const fixture = await render();

    const shown = await openTab(fixture, 'Drone ports');

    expect(shown).toHaveLength(1);
    expect(shown[0]).toContain('Packaged Rocket Fuel');
    expect(shown[0]).toContain('4.46/min');
    expect(shown[0]).toContain('1.25/min');
    expect(shown[0]).toContain('3.21/min short');
  });

  it("links where a machine stands to that machine on the snapshot's map", async () => {
    const fixture = await render();

    const link = (fixture.nativeElement as HTMLElement).querySelector('tr.mat-mdc-row a') as HTMLAnchorElement;

    expect(link.getAttribute('href')).toBe('/snapshots/s1/map?focus=a');
  });

  it('says what a machine running on stock runs on, and how long its stock lasts', async () => {
    const fixture = await render();

    await filterOn(fixture, 'Running');

    expect(rows(fixture.nativeElement)[0]).toContain('On stock: Plutonium Waste, lasts 123 min');
  });
});
