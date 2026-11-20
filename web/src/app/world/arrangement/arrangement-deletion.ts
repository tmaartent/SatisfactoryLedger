import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { firstValueFrom } from 'rxjs';

import { Api } from '../../api/api';
import { deleteArrangement } from '../../api/fn/world/delete-arrangement';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/confirm-dialog';
import { problemMessage } from '../../shared/problem';

/** Deleting a run of the optimal arrangement, after asking; one still being computed too. */
@Injectable({ providedIn: 'root' })
export class ArrangementDeletion {
  private readonly api = inject(Api);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  /** True once it's deleted; false when the player cancelled or the API refused. */
  async confirmAndDelete(run: { id: string; requested: string }): Promise<boolean> {
    const data: ConfirmDialogData = {
      title: 'Delete this run?',
      message: `The run requested ${run.requested} is deleted, with its proposal and belts. Your factories don't change.`,
      confirm: 'Delete',
    };
    const confirmed = await firstValueFrom(this.dialog.open(ConfirmDialog, { data }).afterClosed());
    if (!confirmed) return false;

    try {
      await this.api.invoke(deleteArrangement, { id: run.id });
      return true;
    } catch (error) {
      this.snackBar.open(`The run couldn't be deleted: ${problemMessage(error)}`, 'Close');
      return false;
    }
  }
}
