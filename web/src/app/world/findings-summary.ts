import { Component, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

import { AnalysisResponse } from '../api/models/analysis-response';

/**
 * The overview of a world's or a factory's findings, in cards: its machines and how many run, are starved, output
 * blocked or without a recipe; its generators alike; and whether part of the network didn't settle.
 */
@Component({
  selector: 'app-findings-summary',
  imports: [MatCardModule, MatIconModule, MatTooltipModule],
  templateUrl: './findings-summary.html',
  styleUrl: './findings-summary.scss',
})
export class FindingsSummary {
  readonly findings =
    input.required<Pick<AnalysisResponse, 'summary' | 'generatorSummary' | 'settled'>>();
}
