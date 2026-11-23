import { inject, Injectable, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';

import { CatalogNames } from '../../shared/catalog-names';
import { problemMessage } from '../../shared/problem';
import { SidebarTabs } from '../../sidebar/sidebar-tabs';
import { Assistant, ChatModel, Explanation, ExplanationRequest } from './assistant';

/** A machine as the findings and the 3D view know it: its building, recipe and how it runs. */
export interface HeldBackMachine {
  className: string;
  recipe?: string | null;
  limit: string;
}

/** What was asked: the finding, and the machines by name as the question shows them, "Constructor (Iron Plate)". */
export interface Question {
  finding: Omit<ExplanationRequest, 'model'>;
  machines: string;
}

/**
 * The explanation of a held-back machine (ADR-0030), asked from the findings table, the 3D view or the map, and kept for
 * the whole app: the sidebar's Explanation tab shows it, so it stays while the player follows its links to the map and the
 * 3D view (asked 2026-11-22). One at a time: a new question replaces it. It closes when the player moves to another snapshot,
 * where its links would lead into another world.
 */
@Injectable({ providedIn: 'root' })
export class Explainer {
  private readonly assistant = inject(Assistant);
  private readonly names = inject(CatalogNames);
  private readonly sidebar = inject(SidebarTabs);

  readonly question = signal<Question | null>(null);
  readonly models = signal<ChatModel[]>([]);
  readonly model = signal<string | null>(null);
  readonly asking = signal(false);
  readonly explanation = signal<Explanation | null>(null);
  readonly error = signal<string | null>(null);

  /** Counts the questions asked, so an answer that arrives after a newer question was asked is dropped. */
  private asked = 0;
  private modelsLoaded: Promise<void> | null = null;

  constructor() {
    inject(Router).events.subscribe((event) => {
      if (!(event instanceof NavigationEnd)) return;
      const snapshot = /^\/snapshots\/([^/?#]+)/.exec(event.urlAfterRedirects)?.[1];
      if (snapshot && snapshot !== this.question()?.finding.snapshotId) this.close();
    });
  }

  /** Whether a machine is held back in a way the Assistant explains: starved or output blocked. */
  explainable(machine: Pick<HeldBackMachine, 'limit'>): boolean {
    return machine.limit === 'starved' || machine.limit === 'outputBlocked';
  }

  /** Explains why the machines of this one's building and recipe are held back; the trace takes them together. */
  explain(machine: HeldBackMachine, snapshotId: string): void {
    if (machine.limit !== 'starved' && machine.limit !== 'outputBlocked') return;
    const recipe = machine.recipe ? ` (${this.names.recipe(machine.recipe)})` : '';
    this.question.set({
      finding: {
        snapshotId,
        building: machine.className,
        recipe: machine.recipe ?? null,
        limit: machine.limit,
      },
      machines: `${this.names.building(machine.className)}${recipe}`,
    });
    this.sidebar.show('explanation');
    void this.start();
  }

  /** Asks again, with the model given: the one the player picked, or the same one. */
  async ask(model: string | null = this.model()): Promise<void> {
    const question = this.question();
    if (!question) return;
    const asked = ++this.asked;
    this.model.set(model);
    this.asking.set(true);
    this.error.set(null);
    this.explanation.set(null);
    try {
      const explanation = await this.assistant.explain(
        model ? { ...question.finding, model } : question.finding,
      );
      if (asked === this.asked) this.explanation.set(explanation);
    } catch (error) {
      if (asked === this.asked) this.error.set(problemMessage(error));
    } finally {
      if (asked === this.asked) this.asking.set(false);
    }
  }

  close(): void {
    this.asked++;
    this.question.set(null);
    this.explanation.set(null);
    this.error.set(null);
    this.asking.set(false);
  }

  /** The models first, once, for the choice between them; without them the Assistant's default answers. */
  private async start(): Promise<void> {
    this.modelsLoaded ??= this.loadModels();
    await this.modelsLoaded;
    await this.ask();
  }

  private async loadModels(): Promise<void> {
    try {
      const models = await this.assistant.models();
      this.models.set(models);
      this.model.set(models[0]?.key ?? null);
    } catch {
      // The explanation's own call says what's wrong; the next question tries again.
      this.modelsLoaded = null;
    }
  }
}
