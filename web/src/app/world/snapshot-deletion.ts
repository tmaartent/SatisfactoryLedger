import { formatDate } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { firstValueFrom } from 'rxjs';

import { Api } from '../api/api';
import { deleteSnapshot } from '../api/fn/world/delete-snapshot';
import { ConfirmDialog, ConfirmDialogData } from '../shared/confirm-dialog';
import { problemMessage } from '../shared/problem';

/** Deleting a snapshot and its analysis, after asking. The world's factories stay (BR-W2). */
@Injectable({ providedIn: 'root' })
export class SnapshotDeletion {
  private readonly api = inject(Api);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  /** True once it's deleted; false when the player cancelled or the API refused. */
  async confirmAndDelete(snapshot: { id: string; sessionName: string; savedAt: string }): Promise<boolean> {
    const data: ConfirmDialogData = {
      title: `Delete this snapshot of ${snapshot.sessionName}?`,
      message:
        `The snapshot saved ${formatDate(snapshot.savedAt, 'medium', 'en')} and its findings are deleted. ` +
        `The factories of ${snapshot.sessionName} stay.`,
      confirm: 'Delete',
    };
    const confirmed = await firstValueFrom(this.dialog.open(ConfirmDialog, { data }).afterClosed());
    if (!confirmed) return false;

    try {
      await this.api.invoke(deleteSnapshot, { id: snapshot.id });
      return true;
    } catch (error) {
      this.snackBar.open(`The snapshot couldn't be deleted: ${problemMessage(error)}`, 'Close');
      return false;
    }
  }
}
