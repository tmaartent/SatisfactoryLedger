import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { ExplainPanel } from './world/explain/explain-panel';
import { Explainer } from './world/explain/explainer';

/** The shell: a toolbar with the two areas, World (its map) and Catalog, the page below it, and the explanation asked last beside it. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatToolbarModule, MatButtonModule, MatIconModule, ExplainPanel],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly explainer = inject(Explainer);
}
