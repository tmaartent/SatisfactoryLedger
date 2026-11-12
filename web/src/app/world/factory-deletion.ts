import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { firstValueFrom } from 'rxjs';

import { Api } from '../api/api';
import { deleteFactory } from '../api/fn/world/delete-factory';
import { ConfirmDialog, ConfirmDialogData } from '../shared/confirm-dialog';
import { problemMessage } from '../shared/problem';

/** Deleting a factory definition, after asking. Its machines belong to no factory again (BR-W5). */
@Injectable({ providedIn: 'root' })
export class FactoryDeletion {
  private readonly api = inject(Api);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  /** True once it's deleted; false when the player cancelled or the API refused. */
  async confirmAndDelete(factory: { factoryId: string; name: string }): Promise<boolean> {
    const data: ConfirmDialogData = {
      title: `Delete the factory ${factory.name}?`,
      message: 'Its definition is deleted in every snapshot of its world. Its machines stay, and belong to no factory again.',
      confirm: 'Delete',
    };
    const confirmed = await firstValueFrom(this.dialog.open(ConfirmDialog, { data }).afterClosed());
    if (!confirmed) return false;

    try {
      await this.api.invoke(deleteFactory, { id: factory.factoryId });
      return true;
    } catch (error) {
      this.snackBar.open(`The factory couldn't be deleted: ${problemMessage(error)}`, 'Close');
      return false;
    }
  }
}
