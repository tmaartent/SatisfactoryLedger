import { Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';

import { Api } from '../api/api';
import { defineFactory } from '../api/fn/world/define-factory';
import { getOutputBelts } from '../api/fn/world/get-output-belts';
import { OutputBeltSuggestion } from '../api/models/output-belt-suggestion';
import { CatalogNames } from '../shared/catalog-names';
import { positionInMetres } from '../shared/display';
import { problemMessage } from '../shared/problem';

export interface DefineFactoryDialogData {
  snapshotId: string;
  world: string;
}

/** Defines a factory by its output belt (one of the snapshot's candidates, BR-W12) and its input items (BR-W2). */
@Component({
  selector: 'app-define-factory-dialog',
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatProgressBarModule, MatSelectModule],
  templateUrl: './define-factory-dialog.html',
  styles: `
    mat-form-field { width: 100%; }
    .error { color: var(--mat-sys-error); }
  `,
})
export class DefineFactoryDialog {
  private readonly api = inject(Api);
  private readonly dialogRef = inject(MatDialogRef<DefineFactoryDialog, boolean>);
  protected readonly data = inject<DefineFactoryDialogData>(MAT_DIALOG_DATA);
  protected readonly names = inject(CatalogNames);

  protected readonly belts = resource({ loader: () => this.api.invoke(getOutputBelts, { id: this.data.snapshotId }) });

  protected readonly name = signal('');
  protected readonly outputBelt = signal<string | null>(null);
  protected readonly inputItems = signal<string[]>([]);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly canDefine = computed(() => this.name().trim() !== '' && this.outputBelt() !== null && !this.saving());

  /** "Conveyor Belt Mk3 · Iron Plate · from 4 × Constructor · at 123, −5, 0 m" */
  protected describe(belt: OutputBeltSuggestion): string {
    const items = belt.items.map((item) => this.names.item(item)).join(', ');
    const machines = Object.entries(belt.machines)
      .map(([className, count]) => `${count} × ${this.names.building(className)}`)
      .join(', ');
    return `${this.names.building(belt.className)} · ${items} · from ${machines} · at ${positionInMetres(belt.position)}`;
  }

  protected chooseBelt(belt: string): void {
    this.outputBelt.set(belt);
    // A name to start from: what the belt carries.
    const chosen = this.belts.value()?.find((suggestion) => suggestion.belt === belt);
    if (chosen && this.name().trim() === '') {
      this.name.set(chosen.items.map((item) => this.names.item(item)).join(', '));
    }
  }

  protected async define(): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    try {
      await this.api.invoke(defineFactory, {
        body: { world: this.data.world, name: this.name().trim(), outputBelt: this.outputBelt(), inputItems: this.inputItems() },
      });
      this.dialogRef.close(true);
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
}
