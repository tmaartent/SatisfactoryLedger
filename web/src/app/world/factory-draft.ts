import {
  Injectable,
  Provider,
  Signal,
  computed,
  effect,
  inject,
  resource,
  signal,
} from '@angular/core';

import { Subject } from 'rxjs';

import { Api } from '../api/api';
import { defineFactory } from '../api/fn/world/define-factory';
import { editFactory } from '../api/fn/world/edit-factory';
import { previewFactory } from '../api/fn/world/preview-factory';
import { problemMessage } from '../shared/problem';

import { TracedFactory, blockedMachines, machineOwners } from './area/factory-outlines';
import { toggle } from './area/selection';

/** What a draft is in: the snapshot, its world, and the world's factories as traced there. */
export interface DraftContext {
  snapshotId: Signal<string>;
  /** The snapshot's session name, once loaded: a factory is stored in its world (BR-W11). */
  world: Signal<string | undefined>;
  factories: Signal<readonly TracedFactory[] | undefined>;
}

/**
 * A factory being defined or edited and not saved yet: the machines chosen so far, its name and its preview. One per
 * page, never the app's: the factory's page and the 3D view inside it share it, and a draft doesn't outlive its page.
 */
@Injectable()
export class FactoryDraft {
  private readonly api = inject(Api);
  private readonly context = signal<DraftContext | null>(null);

  private readonly _defining = signal(false);
  private readonly _editing = signal<string | null>(null);
  private readonly _chosen = signal<ReadonlySet<string>>(new Set());
  private readonly saving = signal(false);
  private readonly _saveError = signal<string | null>(null);
  private readonly saves = new Subject<string>();

  readonly defining = this._defining.asReadonly();
  /** The factory being edited; null when a new one is defined. */
  readonly editing = this._editing.asReadonly();
  readonly chosen = this._chosen.asReadonly();
  readonly name = signal('');
  readonly saveError = this._saveError.asReadonly();
  /** A factory saved, new or edited: its id. An event, not a state: saving the same factory twice tells it twice. */
  readonly saved$ = this.saves.asObservable();
  /** The machines that can't be chosen: every factory's, or while editing one, the other factories' (BR-W5). */
  readonly blocked = computed(() =>
    blockedMachines(machineOwners(this.context()?.factories() ?? []), this._editing()),
  );
  readonly canSave = computed(
    () => this._chosen().size > 0 && this.name().trim() !== '' && !this.saving(),
  );

  /** The chosen machines once the choosing pauses: the preview follows them (part 2b(2)). */
  private readonly settled = signal<readonly string[]>([]);
  /** What the chosen machines would make a factory, traced without saving it: what it would own and hand over. */
  readonly preview = resource({
    params: () => {
      const snapshotId = this.context()?.snapshotId();
      return this._defining() && snapshotId && this.settled().length > 0
        ? { id: snapshotId, body: { machines: [...this.settled()] } }
        : undefined;
    },
    loader: ({ params }) => this.api.invoke(previewFactory, params),
  });

  constructor() {
    // The preview follows the choice once it pauses for a moment, not every machine a dragged box sweeps in.
    effect((onCleanup) => {
      const chosen = [...this._chosen()];
      const timer = setTimeout(() => this.settled.set(chosen), 300);
      onCleanup(() => clearTimeout(timer));
    });
  }

  /** Set once, by the 3D view that has these loaded. */
  within(context: DraftContext): void {
    this.context.set(context);
  }

  start(): void {
    this.reset(true, null, [], '');
  }

  /** Edits a factory as a new one is defined: its machines chosen, its name filled in. Not before its machines are known. */
  edit(factoryId: string): void {
    const factory = this.context()
      ?.factories()
      ?.find((candidate) => candidate.factoryId === factoryId);
    if (!factory) return;
    this.reset(
      true,
      factoryId,
      factory.machines.map((machine) => machine.id),
      factory.name,
    );
  }

  /** Adds a machine, or takes it out when it's chosen already; never another factory's (BR-W5). */
  choose(machineId: string): void {
    if (!this._defining() || this.blocked().has(machineId)) return;
    this._chosen.update((chosen) => toggle(chosen, machineId));
  }

  /** Adds or takes out every machine of a box drawn, but another factory's (BR-W5). */
  box(machineIds: readonly string[], mode: 'add' | 'remove'): void {
    if (!this._defining()) return;
    const blocked = this.blocked();
    const next = new Set(this._chosen());
    for (const id of machineIds.filter((id) => !blocked.has(id))) {
      if (mode === 'add') next.add(id);
      else next.delete(id);
    }
    this._chosen.set(next);
  }

  stop(): void {
    this.reset(false, null, [], '');
  }

  /** Every part of the draft at once, so starting, editing and stopping can't leave one behind. */
  private reset(
    defining: boolean,
    editing: string | null,
    chosen: readonly string[],
    name: string,
  ): void {
    this._editing.set(editing);
    this._chosen.set(new Set(chosen));
    this.name.set(name);
    this.settled.set([]);
    this._saveError.set(null);
    this._defining.set(defining);
  }

  /** Stores the factory, new or edited, in the snapshot's world, and is done; if the API refuses, says why and stays. */
  async save(): Promise<void> {
    const world = this.context()?.world();
    if (!world || !this.canSave()) return;
    this.saving.set(true);
    this._saveError.set(null);
    try {
      const name = this.name().trim();
      const machines = [...this._chosen()];
      const editing = this._editing();
      const factory = editing
        ? await this.api.invoke(editFactory, { id: editing, body: { name, machines } })
        : await this.api.invoke(defineFactory, { body: { world, name, machines } });
      this.stop();
      this.saves.next(factory.id);
    } catch (error) {
      this._saveError.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
}

/**
 * For a 3D view: the draft of the page around it, which edits from its own header, or one of its own on a page of its
 * own. Either way it goes with its page. Not a route's providers: a route's injector stays after the route is left.
 */
export const ownDraftOrThePages: Provider = {
  provide: FactoryDraft,
  useFactory: () => inject(FactoryDraft, { skipSelf: true, optional: true }) ?? new FactoryDraft(),
};
