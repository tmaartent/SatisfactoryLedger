import { Component, inject, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { Api } from '../api/api';
import { defineFactory } from '../api/fn/world/define-factory';
import { editFactory } from '../api/fn/world/edit-factory';
import { previewFactory } from '../api/fn/world/preview-factory';
import { TracedFactory } from './area/factory-outlines';
import { FactoryDraft, ownDraftOrThePages } from './factory-draft';

describe('a factory draft', () => {
  /** A draft in a snapshot whose world has the factories given, over an Api that answers as told. */
  function draft(
    factories: TracedFactory[] = [],
    invoke: (fn: unknown, params: unknown) => Promise<unknown> = () => new Promise(() => undefined),
  ) {
    const api = { invoke: vi.fn(invoke) };
    TestBed.configureTestingModule({ providers: [FactoryDraft, { provide: Api, useValue: api }] });
    const draft = TestBed.inject(FactoryDraft);
    draft.within({
      snapshotId: signal('snapshot-1'),
      world: signal('Ficsit'),
      factories: signal(factories),
    });
    return { draft, api };
  }

  it('is defining once started, with nothing chosen and no name', () => {
    const { draft: d } = draft();

    d.start();

    expect(d.defining()).toBe(true);
    expect(d.editing()).toBeNull();
    expect([...d.chosen()]).toEqual([]);
    expect(d.name()).toBe('');
  });

  it('adds a machine chosen, and takes it out when chosen again', () => {
    const { draft: d } = draft();
    d.start();

    d.choose('m-7');
    d.choose('m-8');
    d.choose('m-7');

    expect([...d.chosen()]).toEqual(['m-8']);
  });

  it('can be saved with a machine and a name, not with only one of them', () => {
    const { draft: d } = draft();
    d.start();

    d.choose('m-7');
    expect(d.canSave()).toBe(false);
    d.name.set('  ');
    expect(d.canSave()).toBe(false);
    d.name.set('Screws');
    expect(d.canSave()).toBe(true);
    d.choose('m-7');
    expect(d.canSave()).toBe(false);
  });

  it('chooses nothing while not defining', () => {
    const { draft: d } = draft();

    d.choose('m-7');

    expect([...d.chosen()]).toEqual([]);
  });

  it('is thrown away when stopped', () => {
    const { draft: d } = draft();
    d.start();
    d.choose('m-7');
    d.name.set('Screws');

    d.stop();

    expect(d.defining()).toBe(false);
    expect([...d.chosen()]).toEqual([]);
    expect(d.name()).toBe('');
  });

  describe('saving', () => {
    const stored = { id: 'f-9', name: 'Screws', machines: [], world: 'Ficsit' };
    /** The Api's answer to a save; a preview is never answered. */
    const answering =
      (save: () => Promise<unknown>) =>
      (fn: unknown): Promise<unknown> =>
        fn === defineFactory || fn === editFactory ? save() : new Promise(() => undefined);

    it('stores a new factory in the world, trimmed, and tells its id', async () => {
      const { draft: d, api } = draft(
        [],
        answering(() => Promise.resolve(stored)),
      );
      const saved: string[] = [];
      d.saved$.subscribe((id) => saved.push(id));
      d.start();
      d.choose('m-7');
      d.name.set(' Screws ');

      await d.save();

      expect(api.invoke).toHaveBeenCalledWith(defineFactory, {
        body: { world: 'Ficsit', name: 'Screws', machines: ['m-7'] },
      });
      expect(saved).toEqual(['f-9']);
      expect(d.defining()).toBe(false);
    });

    it('stores the factory edited under its own id', async () => {
      const rubber: TracedFactory = { factoryId: 'f-1', name: 'Rubber', machines: [{ id: 'm-1' }] };
      const { draft: d, api } = draft(
        [rubber],
        answering(() => Promise.resolve(stored)),
      );
      const saved: string[] = [];
      d.saved$.subscribe((id) => saved.push(id));
      d.edit('f-1');
      d.choose('m-2');

      await d.save();

      expect(api.invoke).toHaveBeenCalledWith(editFactory, {
        id: 'f-1',
        body: { name: 'Rubber', machines: ['m-1', 'm-2'] },
      });
      expect(saved).toEqual(['f-9']);
    });

    it("can't be saved again while saving", () => {
      const { draft: d, api } = draft(
        [],
        answering(() => new Promise(() => undefined)),
      );
      d.start();
      d.choose('m-7');
      d.name.set('Screws');

      void d.save();
      void d.save();

      expect(d.canSave()).toBe(false);
      expect(api.invoke.mock.calls.filter(([fn]) => fn === defineFactory)).toHaveLength(1);
    });

    it('keeps the draft and says why when the API refuses it', async () => {
      const { draft: d } = draft(
        [],
        answering(() => Promise.reject(new Error('A machine is in another factory'))),
      );
      const saved: string[] = [];
      d.saved$.subscribe((id) => saved.push(id));
      d.start();
      d.choose('m-7');
      d.name.set('Screws');

      await d.save();

      expect(d.saveError()).toBe('A machine is in another factory');
      expect(d.defining()).toBe(true);
      expect([...d.chosen()]).toEqual(['m-7']);
      expect(d.canSave()).toBe(true);
      expect(saved).toEqual([]);
      d.stop();
      expect(d.saveError()).toBeNull();
    });
  });

  describe('its preview', () => {
    beforeEach(() => vi.useFakeTimers());
    afterEach(() => vi.useRealTimers());

    const preview = {
      arriving: [],
      connected: [],
      inputs: [],
      leaving: [],
      logistics: ['belt-1'],
      outputs: [],
      supplies: [],
    };
    const previews = (api: { invoke: ReturnType<typeof vi.fn> }) =>
      api.invoke.mock.calls.filter(([fn]) => fn === previewFactory).map(([, params]) => params);

    it('follows the machines chosen once the choosing pauses for 300 ms', async () => {
      const { draft: d, api } = draft([], () => Promise.resolve(preview));
      d.start();

      d.choose('m-7');
      TestBed.tick();
      vi.advanceTimersByTime(200);
      d.choose('m-8');
      TestBed.tick();
      vi.advanceTimersByTime(299);
      TestBed.tick();
      expect(previews(api)).toEqual([]);

      vi.advanceTimersByTime(1);
      TestBed.tick();
      await vi.runAllTimersAsync();

      expect(previews(api)).toEqual([{ id: 'snapshot-1', body: { machines: ['m-7', 'm-8'] } }]);
      expect(d.preview.value()).toEqual(preview);
    });

    it('is asked for nothing while not defining, nor with nothing chosen', async () => {
      const { draft: d, api } = draft([], () => Promise.resolve(preview));

      d.start();
      TestBed.tick();
      await vi.runAllTimersAsync();
      d.stop();
      TestBed.tick();
      await vi.runAllTimersAsync();

      expect(previews(api)).toEqual([]);
    });
  });

  describe("other factories' machines (BR-W5)", () => {
    const rubber: TracedFactory = {
      factoryId: 'f-1',
      name: 'Rubber',
      machines: [{ id: 'm-1' }, { id: 'm-2' }],
    };
    const plastic: TracedFactory = { factoryId: 'f-2', name: 'Plastic', machines: [{ id: 'm-3' }] };

    it("can't be chosen in a new factory", () => {
      const { draft: d } = draft([rubber, plastic]);
      d.start();

      d.choose('m-1');
      d.choose('m-7');

      expect([...d.chosen()]).toEqual(['m-7']);
      expect([...d.blocked()].sort()).toEqual(['m-1', 'm-2', 'm-3']);
    });

    it('are left out of a box, which adds or takes out the rest', () => {
      const { draft: d } = draft([rubber]);
      d.start();

      d.box(['m-1', 'm-7', 'm-8'], 'add');
      expect([...d.chosen()]).toEqual(['m-7', 'm-8']);
      d.box(['m-1', 'm-8'], 'remove');
      expect([...d.chosen()]).toEqual(['m-7']);
    });

    it('stay blocked while one factory is edited, whose own machines can be taken out and put back', () => {
      const { draft: d } = draft([rubber, plastic]);

      d.edit('f-1');

      expect(d.defining()).toBe(true);
      expect(d.editing()).toBe('f-1');
      expect(d.name()).toBe('Rubber');
      expect([...d.chosen()]).toEqual(['m-1', 'm-2']);
      expect([...d.blocked()]).toEqual(['m-3']);
      d.choose('m-1');
      d.choose('m-3');
      expect([...d.chosen()]).toEqual(['m-2']);
      d.choose('m-1');
      expect([...d.chosen()]).toEqual(['m-2', 'm-1']);
    });

    it('leave nothing to edit until the factories are known', () => {
      TestBed.configureTestingModule({
        providers: [FactoryDraft, { provide: Api, useValue: { invoke: vi.fn() } }],
      });
      const d = TestBed.inject(FactoryDraft);
      d.within({ snapshotId: signal('s'), world: signal('w'), factories: signal(undefined) });

      d.edit('f-1');

      expect(d.defining()).toBe(false);
    });
  });
});

describe("a 3D view's draft", () => {
  @Component({ selector: 'app-view', template: '', providers: [ownDraftOrThePages] })
  class View {
    readonly draft = inject(FactoryDraft);
  }
  @Component({
    selector: 'app-page',
    imports: [View],
    template: '<app-view />',
    providers: [FactoryDraft],
  })
  class Page {
    readonly draft = inject(FactoryDraft);
  }

  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [{ provide: Api, useValue: { invoke: vi.fn() } }],
    }),
  );

  it("is the page's around it, so the two share it", () => {
    const fixture = TestBed.createComponent(Page);
    const view = fixture.debugElement.query((node) => node.componentInstance instanceof View);

    const shown: View = view.componentInstance;

    expect(shown.draft).toBe(fixture.componentInstance.draft);
  });

  it('is its own on a page of its own, and goes with it', () => {
    const first = TestBed.createComponent(View);
    first.componentInstance.draft.start();
    first.destroy();

    expect(TestBed.createComponent(View).componentInstance.draft.defining()).toBe(false);
  });
});
