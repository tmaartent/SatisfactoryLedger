import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';

import { Assistant, ChatModel, Explanation } from './assistant';
import { ExplainDialog, ExplainDialogData } from './explain-dialog';

const data: ExplainDialogData = {
  finding: { snapshotId: 's1', building: 'Build_ConstructorMk1_C', recipe: 'Recipe_IronPlate_C', limit: 'starved' },
  machines: 'Constructor (Iron Plate)',
};

const explanation = (unsupportedNumbers: string[] = []): Explanation => ({
  text: 'In the model, the 2 Constructors are short of Iron Ingot: the one Smelter makes 30 a minute.',
  model: 'qwen2.5:7b',
  facts: {
    tool: 'trace_upstream',
    arguments: {},
    answer: {
      building: 'Constructor',
      recipe: 'Iron Plate',
      machines: 3,
      traced: 2,
      starts: [
        {
          cause: 'tooFewProducers', causeTerm: 'too few producers',
          item: 'Iron Ingot',
          perMinute: 30,
          at: [{ building: 'Smelter', recipe: 'Iron Ingot', count: 1, ids: ['smelter-1'] }],
          path: [{ building: 'Constructor', recipe: 'Iron Plate', machines: 2, ids: ['constructor-1', 'constructor-2'], shortOf: 'Iron Ingot', perMinute: 30 }],
        },
      ],
    },
  },
  unsupportedNumbers,
});

const local: ChatModel = { key: 'local', model: 'qwen2.5:7b' };
const claude: ChatModel = { key: 'claude', model: 'claude-sonnet-5-5' };

describe('explaining a finding', () => {
  async function render(assistant: { models: () => Promise<ChatModel[]>; explain: Assistant['explain'] }) {
    TestBed.configureTestingModule({
      imports: [ExplainDialog],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: { close: vi.fn() } },
        provideRouter([]),
        { provide: Assistant, useValue: assistant },
      ],
    });
    const fixture = TestBed.createComponent(ExplainDialog);
    await settled(fixture);
    return fixture;
  }

  /** Lets the dialog's calls answer: they're promises the test resolves at once. */
  async function settled(fixture: ComponentFixture<ExplainDialog>) {
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
  }

  const text = (fixture: ComponentFixture<ExplainDialog>) => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('asks the first model and says which model wrote it, from which facts', async () => {
    const explain = vi.fn(() => Promise.resolve(explanation()));
    const fixture = await render({ models: () => Promise.resolve([local]), explain });

    expect(explain).toHaveBeenCalledWith({ ...data.finding, model: 'local' });
    expect(text(fixture)).toContain('Why are the Constructor (Iron Plate) machines starved?');
    expect(text(fixture)).toContain('the one Smelter makes 30 a minute');
    expect(text(fixture)).toContain('Written by qwen2.5:7b, from trace_upstream');
    expect(text(fixture)).not.toContain('Not in the facts');
  });

  it('lists where it starts, each place linked to the map, instead of the raw facts', async () => {
    const fixture = await render({ models: () => Promise.resolve([local]), explain: () => Promise.resolve(explanation()) });

    expect(text(fixture)).toContain('Too few producers');
    expect(text(fixture)).toContain('Smelter (Iron Ingot)');
    const links = [...(fixture.nativeElement as HTMLElement).querySelectorAll<HTMLAnchorElement>('.starts a')];
    expect(links.find((link) => link.textContent?.includes('Smelter'))?.getAttribute('href')).toBe('/snapshots/s1/map?focus=smelter-1');
    expect(text(fixture)).not.toContain('"cause"');
  });

  it('says beside each start which machines it holds back first and what the buildings it starts at do, with the rate', async () => {
    const fixture = await render({ models: () => Promise.resolve([local]), explain: () => Promise.resolve(explanation()) });

    expect(text(fixture)).toContain('Short of it');
    expect(text(fixture)).toContain('Constructor (Iron Plate)');
    expect(text(fixture)).toMatch(/Made at full speed by\s*·\s*30\/min/);
    const links = [...(fixture.nativeElement as HTMLElement).querySelectorAll<HTMLAnchorElement>('.starts a')].map((link) => link.getAttribute('href'));
    expect(links).toContain('/snapshots/s1/map?focus=constructor-1');
  });

  it('names the items on the way to a start further up', async () => {
    const further = explanation();
    further.facts.answer.starts[0].path = [
      { building: 'Assembler', recipe: 'Reinforced Iron Plate', machines: 1, ids: ['assembler-1'], shortOf: 'Iron Plate', perMinute: 30 },
      { building: 'Constructor', recipe: 'Iron Plate', machines: 2, ids: ['constructor-1', 'constructor-2'], shortOf: 'Iron Ingot', perMinute: 30 },
    ];
    const fixture = await render({ models: () => Promise.resolve([local]), explain: () => Promise.resolve(further) });

    expect(text(fixture)).toMatch(/Via\s*Iron Plate/);
  });

  it('flags the numbers that are not in the facts', async () => {
    const fixture = await render({ models: () => Promise.resolve([local]), explain: () => Promise.resolve(explanation(['45', '15'])) });

    expect(text(fixture)).toContain('Not in the facts: 45, 15');
  });

  it("says why the ledger couldn't answer", async () => {
    const refused = new HttpErrorResponse({ status: 422, error: { detail: 'This snapshot is still being analysed.' } });
    const fixture = await render({ models: () => Promise.resolve([local]), explain: () => Promise.reject(refused) });

    expect(text(fixture)).toContain('This snapshot is still being analysed.');
  });

  it('asks another model when the player picks one', async () => {
    const explain = vi.fn(() => Promise.resolve(explanation()));
    const fixture = await render({ models: () => Promise.resolve([local, claude]), explain });

    const toggles = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('mat-button-toggle button'));
    expect(toggles.map((toggle) => toggle.textContent?.trim())).toEqual(['qwen2.5:7b', 'claude-sonnet-5-5']);
    toggles[1].click();
    await settled(fixture);

    expect(explain).toHaveBeenLastCalledWith({ ...data.finding, model: 'claude' });
  });

  it('offers no choice of model when there is one', async () => {
    const fixture = await render({ models: () => Promise.resolve([local]), explain: () => Promise.resolve(explanation()) });

    expect((fixture.nativeElement as HTMLElement).querySelector('mat-button-toggle')).toBeNull();
  });
});
