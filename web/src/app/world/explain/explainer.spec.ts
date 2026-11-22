import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';

import { CatalogNames } from '../../shared/catalog-names';
import { ExplainDialog } from './explain-dialog';
import { Explainer } from './explainer';

describe('explaining a held-back machine', () => {
  let dialog: { open: ReturnType<typeof vi.fn> };

  function explainer(): Explainer {
    dialog = { open: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        { provide: MatDialog, useValue: dialog },
        {
          provide: CatalogNames,
          useValue: { building: (id: string) => ({ Build_ConstructorMk1_C: 'Constructor' })[id] ?? id, recipe: (id: string) => ({ Recipe_IronPlate_C: 'Iron Plate' })[id] ?? id },
        },
      ],
    });
    return TestBed.inject(Explainer);
  }

  it('explains starved and output-blocked machines, not running ones or ones without a recipe', () => {
    const explain = explainer();

    expect(['starved', 'outputBlocked', 'none', 'noRecipe'].map((limit) => explain.explainable({ limit }))).toEqual([true, true, false, false]);
  });

  it('asks about the machines of its building and recipe, named as the game names them', () => {
    const explain = explainer();

    explain.explain({ className: 'Build_ConstructorMk1_C', recipe: 'Recipe_IronPlate_C', limit: 'starved' }, 's1');

    expect(dialog.open).toHaveBeenCalledWith(ExplainDialog, expect.objectContaining({
      data: {
        finding: { snapshotId: 's1', building: 'Build_ConstructorMk1_C', recipe: 'Recipe_IronPlate_C', limit: 'starved' },
        machines: 'Constructor (Iron Plate)',
      },
    }));
  });

  it('asks nothing about a machine that is not held back', () => {
    const explain = explainer();

    explain.explain({ className: 'Build_ConstructorMk1_C', recipe: 'Recipe_IronPlate_C', limit: 'none' }, 's1');

    expect(dialog.open).not.toHaveBeenCalled();
  });
});
