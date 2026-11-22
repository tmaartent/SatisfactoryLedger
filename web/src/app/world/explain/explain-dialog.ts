import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';

import { problemMessage } from '../../shared/problem';
import { Assistant, ChatModel, Explanation, ExplanationRequest } from './assistant';
import { whereItStarts } from './trace-starts';

export interface ExplainDialogData {
  finding: Omit<ExplanationRequest, 'model'>;
  /** The machines by name, as the question shows them: "Constructor (Iron Plate)". */
  machines: string;
}

/**
 * A finding explained in words by a chat model (AI enhancement 3), with what it stands on: the model, the tool whose
 * answer it was given, where that answer says it starts (listed by code, linked to the map), and the numbers in the
 * text that the answer doesn't hold.
 */
@Component({
  selector: 'app-explain-dialog',
  imports: [DecimalPipe, NgTemplateOutlet, MatButtonModule, MatButtonToggleModule, MatDialogModule, MatIconModule, MatProgressBarModule, RouterLink],
  templateUrl: './explain-dialog.html',
  styleUrl: './explain-dialog.scss',
})
export class ExplainDialog {
  protected readonly data = inject<ExplainDialogData>(MAT_DIALOG_DATA);
  private readonly assistant = inject(Assistant);

  protected readonly held = this.data.finding.limit === 'starved' ? 'starved' : 'output blocked';
  protected readonly models = signal<ChatModel[]>([]);
  protected readonly model = signal<string | null>(null);
  protected readonly asking = signal(false);
  protected readonly explanation = signal<Explanation | null>(null);
  protected readonly error = signal<string | null>(null);

  /** The model being asked, by what it runs. */
  protected readonly modelName = computed(() => this.models().find((model) => model.key === this.model())?.model ?? 'the model');
  protected readonly source = computed(() => {
    const explanation = this.explanation();
    if (!explanation) return '';
    return `${explanation.model ? `Written by ${explanation.model}` : "The trace's own note"}, from ${explanation.facts.tool}.`;
  });
  protected readonly starts = computed(() => whereItStarts(this.explanation()?.facts.answer.starts ?? []));

  constructor() {
    void this.start();
  }

  /** Asks again, with the model given: the one the player picked, or the same one. */
  protected async explain(model: string | null = this.model()): Promise<void> {
    this.model.set(model);
    this.asking.set(true);
    this.error.set(null);
    this.explanation.set(null);
    try {
      this.explanation.set(await this.assistant.explain(model ? { ...this.data.finding, model } : this.data.finding));
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.asking.set(false);
    }
  }

  /** The models first, for the choice between them; without them the Assistant's default answers. */
  private async start(): Promise<void> {
    try {
      const models = await this.assistant.models();
      this.models.set(models);
      this.model.set(models[0]?.key ?? null);
    } catch {
      // The explanation's own call says what's wrong.
    }
    await this.explain();
  }
}
