import { TemplateRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { CatalogNames } from '../../shared/catalog-names';
import { SidebarTabs } from '../../sidebar/sidebar-tabs';
import { Assistant, ChatModel, Explanation, ExplanationRequest } from './assistant';
import { Explainer } from './explainer';

const local: ChatModel = { key: 'local', model: 'qwen2.5:7b' };

const answer = (text: string): Explanation => ({
  text,
  model: 'qwen2.5:7b',
  facts: {
    tool: 'trace_upstream',
    arguments: {},
    answer: { building: 'Constructor', recipe: 'Iron Plate', machines: 3, traced: 2, starts: [] },
  },
  unsupportedNumbers: [],
});

const plates = {
  className: 'Build_ConstructorMk1_C',
  recipe: 'Recipe_IronPlate_C',
  limit: 'starved',
};
const smelters = { className: 'Build_SmelterMk1_C', recipe: null, limit: 'outputBlocked' };

/** Lets the explainer's calls answer: they're promises the test resolves at once. */
const settled = () => new Promise((resolve) => setTimeout(resolve));

describe('explaining a held-back machine, kept beside every page', () => {
  function explainer(
    explain: (request: ExplanationRequest) => Promise<Explanation> = () =>
      Promise.resolve(answer('Too few smelters.')),
  ): Explainer {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', children: [] }]),
        {
          provide: Assistant,
          useValue: { models: () => Promise.resolve([local]), explain: vi.fn(explain) },
        },
        {
          provide: CatalogNames,
          useValue: {
            icon: () => null,
            iconOfItemNamed: () => null,
            building: (id: string) =>
              ({ Build_ConstructorMk1_C: 'Constructor', Build_SmelterMk1_C: 'Smelter' })[id] ?? id,
            recipe: (id: string) => ({ Recipe_IronPlate_C: 'Iron Plate' })[id] ?? id,
          },
        },
      ],
    });
    return TestBed.inject(Explainer);
  }

  it('explains starved and output-blocked machines, not running ones or ones without a recipe', () => {
    const explain = explainer();

    expect(
      ['starved', 'outputBlocked', 'none', 'noRecipe'].map((limit) =>
        explain.explainable({ limit }),
      ),
    ).toEqual([true, true, false, false]);
  });

  it('asks about the machines of its building and recipe, named as the game names them, with the first model', async () => {
    const explain = explainer();

    explain.explain(plates, 's1');
    await settled();

    expect(explain.question()).toEqual({
      finding: {
        snapshotId: 's1',
        building: 'Build_ConstructorMk1_C',
        recipe: 'Recipe_IronPlate_C',
        limit: 'starved',
      },
      machines: 'Constructor (Iron Plate)',
    });
    expect(TestBed.inject(Assistant).explain).toHaveBeenCalledWith({
      snapshotId: 's1',
      building: 'Build_ConstructorMk1_C',
      recipe: 'Recipe_IronPlate_C',
      limit: 'starved',
      model: 'local',
    });
    expect(explain.explanation()?.text).toBe('Too few smelters.');
  });

  it('asks nothing about a machine that is not held back', () => {
    const explain = explainer();

    explain.explain({ ...plates, limit: 'none' }, 's1');

    expect(explain.question()).toBeNull();
  });

  it('replaces the explanation with a new question, and drops the answer to the old one when it comes late', async () => {
    let answerPlates: (explanation: Explanation) => void = () => undefined;
    const explain = explainer((request) =>
      request.building === 'Build_ConstructorMk1_C'
        ? new Promise((resolve) => (answerPlates = resolve))
        : Promise.resolve(answer('Nothing takes the ingots.')),
    );

    explain.explain(plates, 's1');
    await settled();
    explain.explain(smelters, 's1');
    await settled();
    answerPlates(answer('Too few smelters.'));
    await settled();

    expect(explain.question()?.machines).toBe('Smelter');
    expect(explain.explanation()?.text).toBe('Nothing takes the ingots.');
  });

  it("shows the sidebar's Explanation tab, also when the sidebar was collapsed", () => {
    const explain = explainer();
    const sidebar = TestBed.inject(SidebarTabs);
    sidebar.add({
      key: 'info',
      label: 'Info',
      icon: 'info',
      order: 0,
      template: {} as TemplateRef<unknown>,
    });
    sidebar.add({
      key: 'explanation',
      label: 'Explanation',
      icon: 'lightbulb',
      order: 9,
      template: {} as TemplateRef<unknown>,
    });
    sidebar.collapsed.set(true);

    explain.explain(plates, 's1');

    expect(sidebar.collapsed()).toBe(false);
    expect(sidebar.current()?.key).toBe('explanation');
  });

  it('stays while the player follows its links within the snapshot, and to pages of no snapshot', async () => {
    const explain = explainer();
    explain.explain(plates, 's1');

    await TestBed.inject(Router).navigateByUrl('/snapshots/s1/map?focus=smelter-1');
    await TestBed.inject(Router).navigateByUrl('/catalog');

    expect(explain.question()).not.toBeNull();
  });

  it('closes when the player moves to another snapshot, where its links would lead into another world', async () => {
    const explain = explainer();
    explain.explain(plates, 's1');

    await TestBed.inject(Router).navigateByUrl('/snapshots/s2/map');

    expect(explain.question()).toBeNull();
  });
});
