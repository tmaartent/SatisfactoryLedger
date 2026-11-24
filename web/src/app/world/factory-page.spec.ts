import { NgTemplateOutlet } from '@angular/common';
import { Component, TemplateRef, input, output, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { Api } from '../api/api';
import { FactoryAnalysisResponse } from '../api/models/factory-analysis-response';
import { CatalogNames } from '../shared/catalog-names';
import { FactoryDeletion } from './factory-deletion';
import { AreaPage } from './area/area-page';
import { FactoryPage } from './factory-page';

/** The 3D view, without WebGL (jsdom has none): what the factory page gives it, and what it says back. */
@Component({
  selector: 'app-area-page',
  imports: [NgTemplateOutlet],
  // What the page puts at the top of the 3D view's Info tab.
  template: '<ng-container *ngTemplateOutlet="infoTop()" />',
})
class AreaPageStub {
  readonly infoTop = input<TemplateRef<unknown> | null>(null);
  readonly id = input<string>();
  readonly factory = input<string>();
  readonly embedded = input(false);
  readonly saved = output<string>();
  // Editing, as the 3D view keeps it: the page reads it and drives it from its header.
  readonly defining = signal(false);
  readonly factoryName = signal('');
  readonly canSave = signal(true);
  readonly saveError = signal<string | null>(null);
  readonly edit = vi.fn(() => {
    this.defining.set(true);
    this.factoryName.set('Rubber');
  });
  readonly save = vi.fn();
  readonly stopDefining = vi.fn(() => this.defining.set(false));
}

const rubber: FactoryAnalysisResponse = {
  factoryId: 'f-1',
  name: 'Rubber',
  summary: { machines: 4, running: 1, starved: 1, outputBlocked: 1, noRecipe: 1 },
  generatorSummary: { generators: 0, running: 0, starved: 0, outputBlocked: 0, fuelUnknown: 0 },
  settled: true,
  machines: [],
  generators: [],
  bottlenecks: [],
  unusedSupply: [],
  unknownSupply: [],
  // Ore comes in and plates leave: the ingots made and taken inside are neither.
  input: [{ item: 'Desc_OreIron_C', now: 60, atFullSpeed: 120, short: 60, sources: [] }],
  output: [
    {
      item: 'Desc_IronPlate_C',
      now: 20,
      atFullSpeed: 40,
      short: 0,
      sunk: 0,
      cannotLeave: 0,
      receiversNeed: 0,
      othersBring: 0,
      destinations: [],
    },
  ],
  power: null,
} as unknown as FactoryAnalysisResponse;

describe("a factory's page", () => {
  async function render(confirmed = true) {
    const deletion = { confirmAndDelete: vi.fn(() => Promise.resolve(confirmed)) };
    const api = { invoke$Response: vi.fn(() => Promise.resolve({ status: 200, body: rubber })) };
    TestBed.configureTestingModule({
      imports: [FactoryPage],
      providers: [
        provideRouter([]),
        { provide: Api, useValue: api },
        {
          provide: CatalogNames,
          useValue: {
            icon: () => null,
            iconOfItemNamed: () => null,
            item: (item: string) => item,
            building: (name: string) => name,
          },
        },
        { provide: FactoryDeletion, useValue: deletion },
      ],
    });
    TestBed.overrideComponent(FactoryPage, {
      remove: { imports: [AreaPage] },
      add: { imports: [AreaPageStub] },
    });
    const fixture = TestBed.createComponent(FactoryPage);
    fixture.componentRef.setInput('id', 'snapshot-1');
    fixture.componentRef.setInput('factoryId', 'f-1');
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const area = () =>
      fixture.debugElement.query((node) => node.componentInstance instanceof AreaPageStub)
        ?.componentInstance as AreaPageStub;
    const button = (label: string) =>
      [...element.querySelectorAll('.page-header button, .page-header a')].find((b) =>
        b.textContent?.includes(label),
      ) as HTMLElement | undefined;
    return { fixture, api, element, area, deletion, button };
  }

  it('shows the overview, then the factory in 3D, then what comes in and what leaves', async () => {
    const { element } = await render();

    const order = [...element.querySelectorAll('.summary, app-area-page, h2')].map((node) =>
      node.matches('.summary')
        ? 'overview'
        : node.matches('app-area-page')
          ? '3D'
          : node.textContent?.trim(),
    );
    expect(order).toEqual(['overview', '3D', 'Input', 'Output']);
  });

  it('sums up at the top of the Info tab what comes in from outside and what leaves, now and at 100%', async () => {
    const { element } = await render();

    const totals = element.querySelector('app-area-page .totals') as HTMLElement;
    const rows = (kind: string) =>
      [...totals.querySelectorAll(`.${kind} li`)].map((row) =>
        (row.textContent ?? '').replace(/\s+/g, ' ').trim(),
      );
    expect(rows('in')).toEqual(['Desc_OreIron_C 60/min · 120/min at 100%']);
    expect(rows('out')).toEqual(['Desc_IronPlate_C 20/min · 40/min at 100%']);
  });

  it('shows how many of its machines are starved, output blocked and without a recipe', async () => {
    const { element } = await render();

    const text = element.querySelector('.summary')?.textContent?.replace(/\s+/g, ' ') ?? '';
    expect(text).toContain('1 starved');
    expect(text).toContain('1 output blocked');
    expect(text).toContain('1 without a recipe');
  });

  it('lets its tables scroll sideways, rather than widen the page past a phone screen', async () => {
    const { element } = await render();

    const tables = [...element.querySelectorAll('table')];
    expect(tables.length).toBeGreaterThan(0);
    expect(tables.every((table) => table.parentElement?.classList.contains('table-scroll'))).toBe(
      true,
    );
  });

  it("no longer lists the factory's findings at the bottom", async () => {
    const { element } = await render();

    expect(element.querySelector('app-findings')).toBeNull();
  });

  it('opens the 3D view on the factory, with all it can do', async () => {
    const { area } = await render();

    expect([area().id(), area().factory(), area().embedded()]).toEqual(['snapshot-1', 'f-1', true]);
  });

  it('asks for its findings again once its machines are changed in 3D', async () => {
    const { fixture, api, area } = await render();
    api.invoke$Response.mockClear();

    area().saved.emit('f-1');
    await fixture.whenStable();

    expect(api.invoke$Response).toHaveBeenCalled();
  });

  it('goes back to the map with the factory highlighted, not to the findings', async () => {
    const { element } = await render();

    const back = element.querySelector('.page-header a[aria-label="Back to the map"]');
    expect(back?.getAttribute('href')).toBe('/snapshots/snapshot-1/map?factory=f-1');
  });

  it('offers editing and deleting the factory at the top, and no way to the map', async () => {
    const { button } = await render();

    expect(button('Edit')).toBeDefined();
    expect(button('Delete')).toBeDefined();
    expect(button('Map')).toBeUndefined();
  });

  it('edits the factory in its 3D view', async () => {
    const { fixture, area, button } = await render();

    button('Edit')!.click();
    await fixture.whenStable();

    expect(area().edit).toHaveBeenCalledWith('f-1');
  });

  it('goes to the map once the factory is deleted, after asking', async () => {
    const { fixture, deletion, button } = await render(true);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    button('Delete')!.click();
    await fixture.whenStable();

    expect(deletion.confirmAndDelete).toHaveBeenCalledWith({ factoryId: 'f-1', name: 'Rubber' });
    expect(navigate).toHaveBeenCalledWith(['/snapshots', 'snapshot-1', 'map']);
  });

  it('stays when the player cancels the deleting', async () => {
    const { fixture, button } = await render(false);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    button('Delete')!.click();
    await fixture.whenStable();

    expect(navigate).not.toHaveBeenCalled();
  });

  describe('while editing', () => {
    async function editing() {
      const rendered = await render();
      rendered.button('Edit')!.click();
      await rendered.fixture.whenStable();
      return rendered;
    }

    it('makes the name at the top editable', async () => {
      const { fixture, element, area } = await editing();
      const name = element.querySelector('.page-header input') as HTMLInputElement;

      expect(name.value).toBe('Rubber');
      name.value = 'Rubber and plastic';
      name.dispatchEvent(new Event('input'));
      await fixture.whenStable();

      expect(area().factoryName()).toBe('Rubber and plastic');
      expect(element.querySelector('.page-header h1')).toBeNull();
    });

    it('offers Save and Cancel at the top instead of Edit', async () => {
      const { button } = await editing();

      expect(button('Edit')).toBeUndefined();
      expect(button('Save')).toBeDefined();
      expect(button('Cancel')).toBeDefined();
    });

    it('saves in the 3D view', async () => {
      const { fixture, area, button } = await editing();

      button('Save')!.click();
      await fixture.whenStable();

      expect(area().save).toHaveBeenCalled();
    });

    it('stops editing on Cancel, and shows the name again', async () => {
      const { fixture, element, area, button } = await editing();

      button('Cancel')!.click();
      await fixture.whenStable();

      expect(area().stopDefining).toHaveBeenCalled();
      expect(element.querySelector('.page-header h1')?.textContent).toContain('Rubber');
    });
  });
});
