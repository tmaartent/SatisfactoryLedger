import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';

import { Explainer } from './explainer';
import { whereItStarts } from './trace-starts';

/**
 * A finding explained in words by a chat model (AI enhancement 3), beside every page so it stays while the player
 * follows its links (asked 2026-11-22). With what it stands on: the model, the tool whose answer it was given, where
 * that answer says it starts (linked to the map), and the numbers in the text that the answer doesn't hold. It
 * collapses to a strip, to give the page its width back.
 */
@Component({
  selector: 'app-explain-panel',
  imports: [DecimalPipe, NgTemplateOutlet, MatButtonModule, MatButtonToggleModule, MatIconModule, MatProgressBarModule, MatTooltipModule, RouterLink],
  templateUrl: './explain-panel.html',
  styleUrl: './explain-panel.scss',
  host: { '[class.collapsed]': 'explainer.collapsed()' },
})
export class ExplainPanel {
  protected readonly explainer = inject(Explainer);

  protected readonly held = computed(() => (this.explainer.question()?.finding.limit === 'starved' ? 'starved' : 'output blocked'));
  /** The model being asked, by what it runs. */
  protected readonly modelName = computed(() => this.explainer.models().find((model) => model.key === this.explainer.model())?.model ?? 'the model');
  protected readonly source = computed(() => {
    const explanation = this.explainer.explanation();
    if (!explanation) return '';
    return `${explanation.model ? `Written by ${explanation.model}` : "The trace's own note"}, from ${explanation.facts.tool}.`;
  });
  protected readonly starts = computed(() => whereItStarts(this.explainer.explanation()?.facts.answer.starts ?? []));
}
