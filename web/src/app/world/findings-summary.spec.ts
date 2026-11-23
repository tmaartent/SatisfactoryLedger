import { TestBed } from '@angular/core/testing';

import { FindingsSummary } from './findings-summary';
import { ShownStatus } from './shown-status';

const findings = {
  summary: { machines: 6, running: 3, starved: 2, outputBlocked: 0, noRecipe: 1 },
  generatorSummary: { generators: 2, running: 1, starved: 1, outputBlocked: 0, fuelUnknown: 0 },
  settled: true,
};

async function render(selectable: boolean, shown: ShownStatus | null = null) {
  TestBed.configureTestingModule({ imports: [FindingsSummary] });
  const fixture = TestBed.createComponent(FindingsSummary);
  fixture.componentRef.setInput('findings', findings);
  fixture.componentRef.setInput('selectable', selectable);
  fixture.componentRef.setInput('shown', shown);
  const asked: (ShownStatus | null)[] = [];
  fixture.componentInstance.show.subscribe((status) => asked.push(status));
  await fixture.whenStable();
  const element = fixture.nativeElement as HTMLElement;
  const card = (label: string, group = 0) =>
    [...element.querySelectorAll('.summary')[group].querySelectorAll('mat-card')].find((c) =>
      c.textContent?.includes(label),
    ) as HTMLElement;
  return { fixture, asked, card };
}

describe('the overview of the findings', () => {
  it('shows the machines without a recipe when their count is clicked', async () => {
    const { asked, card } = await render(true);

    card('without a recipe').click();

    expect(asked).toEqual([{ of: 'machines', limit: 'noRecipe' }]);
  });

  it('shows the generators of a status apart from the machines', async () => {
    const { asked, card } = await render(true);

    card('starved', 1).click();

    expect(asked).toEqual([{ of: 'generators', limit: 'starved' }]);
  });

  it('shows none again when the count shown is clicked again', async () => {
    const { asked, card } = await render(true, { of: 'machines', limit: 'starved' });

    expect(card('starved').getAttribute('aria-pressed')).toBe('true');
    card('starved').click();

    expect(asked).toEqual([null]);
  });

  it('offers no count of nothing, nor the totals', async () => {
    const { asked, card } = await render(true);

    card('output blocked').click();
    card('machines').click();

    expect(asked).toEqual([]);
    expect(card('output blocked').getAttribute('role')).toBeNull();
  });

  it('offers no count to click where nothing shows them', async () => {
    const { asked, card } = await render(false);

    card('without a recipe').click();

    expect(asked).toEqual([]);
    expect(card('without a recipe').getAttribute('role')).toBeNull();
  });
});
