import { inject, Injectable } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';

import { CatalogNames } from '../../shared/catalog-names';
import { ExplainDialog, ExplainDialogData } from './explain-dialog';

/** A machine as the findings and the 3D view know it: its building, recipe and how it runs. */
export interface HeldBackMachine {
  className: string;
  recipe?: string | null;
  limit: string;
}

/**
 * Explains a held-back machine (ADR-0030), from the findings table or the 3D view: whether it can be explained, and the
 * dialog that explains it. One place, so the two views ask the same question.
 */
@Injectable({ providedIn: 'root' })
export class Explainer {
  private readonly dialog = inject(MatDialog);
  private readonly names = inject(CatalogNames);

  /** Whether a machine is held back in a way the Assistant explains: starved or output blocked. */
  explainable(machine: Pick<HeldBackMachine, 'limit'>): boolean {
    return machine.limit === 'starved' || machine.limit === 'outputBlocked';
  }

  /** Explains why the machines of this one's building and recipe are held back; the trace takes them together. */
  explain(machine: HeldBackMachine, snapshotId: string): void {
    if (machine.limit !== 'starved' && machine.limit !== 'outputBlocked') return;
    const recipe = machine.recipe ? ` (${this.names.recipe(machine.recipe)})` : '';
    const data: ExplainDialogData = {
      finding: { snapshotId, building: machine.className, recipe: machine.recipe ?? null, limit: machine.limit },
      machines: `${this.names.building(machine.className)}${recipe}`,
    };
    this.dialog.open(ExplainDialog, { data, width: '640px' });
  }
}
