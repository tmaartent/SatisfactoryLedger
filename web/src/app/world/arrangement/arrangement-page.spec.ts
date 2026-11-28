import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Api } from '../../api/api';
import { getMapArt } from '../../api/fn/catalog/get-map-art';
import { getWorldMap } from '../../api/fn/world/get-world-map';
import { previewFactory } from '../../api/fn/world/preview-factory';
import { requestArrangement } from '../../api/fn/world/request-arrangement';
import { traceFactories } from '../../api/fn/world/trace-factories';
import { CatalogNames } from '../../shared/catalog-names';
import { Sidebar } from '../../sidebar/sidebar';
import { ArrangementPage } from './arrangement-page';

// jsdom draws no canvas, and Leaflet draws on one: a context where everything does nothing.
beforeEach(() => {
  const nothing: unknown = new Proxy(() => nothing, { get: () => nothing });
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(
    // eslint-disable-next-line @typescript-eslint/consistent-type-assertions -- a stand-in, not a context
    nothing as CanvasRenderingContext2D,
  );
  // Nor does it lay anything out, so nothing is ever resized: an observer that never calls back.
  vi.stubGlobal(
    'ResizeObserver',
    class {
      observe() {
        /* never calls back */
      }
      disconnect() {
        /* nothing to stop */
      }
    },
  );
});
// Leaflet redraws its canvas on the next animation frame: let it, before jsdom goes.
afterEach(() => new Promise((drawn) => requestAnimationFrame(drawn)));

/** The game's map picture in two tiles, as the Catalog serves it. */
const art = {
  gameVersion: '1.2',
  x0: -1000,
  y0: -500,
  x1: 1000,
  y1: 500,
  tiles: [
    {
      column: 0,
      row: 0,
      x0: -1000,
      y0: -500,
      x1: 0,
      y1: 500,
      url: '/api/v1/catalog/map-art/tiles/0/0',
    },
    {
      column: 1,
      row: 0,
      x0: 0,
      y0: -500,
      x1: 1000,
      y1: 500,
      url: '/api/v1/catalog/map-art/tiles/1/0',
    },
  ],
};

/** A world with one factory's machine in it, Rubber's. */
const world = {
  machines: [
    {
      id: 'm-1',
      className: 'Build_ConstructorMk1_C',
      recipe: null,
      position: { x: 0, y: 0, z: 0 },
      yaw: 0,
    },
  ],
  providers: [],
  logistics: [],
  consumers: [],
  conveyors: [],
  footprints: [],
};

const rubber = {
  factoryId: 'f-1',
  name: 'Rubber',
  traceable: true,
  machines: [{ id: 'm-1', className: 'Build_ConstructorMk1_C', recipe: null, owner: 'f-1' }],
  inputs: [],
  outputs: [],
  missing: [],
  logistics: 0,
};

/** An area around Rubber's machine, as the world map hands one over in the address. */
const aroundRubber = { x0: '-1000', y0: '-1000', x1: '1000', y1: '1000' };

describe('the optimal arrangement', () => {
  async function render(area?: typeof aroundRubber) {
    let api: { invoke: ReturnType<typeof vi.fn> } | undefined;
    TestBed.configureTestingModule({
      imports: [ArrangementPage],
      providers: [
        provideRouter([]),
        {
          provide: Api,
          useValue: (api = {
            invoke: vi.fn((fn: unknown) =>
              Promise.resolve(
                fn === getMapArt
                  ? art
                  : fn === getWorldMap
                    ? world
                    : fn === traceFactories
                      ? [rubber]
                      : fn === previewFactory
                        ? {
                            connected: ['m-1', 'belt-1'],
                            supplies: ['m-1', 'belt-1'],
                            logistics: [],
                          }
                        : [],
              ),
            ),
          }),
        },
        {
          provide: CatalogNames,
          useValue: { icon: () => null, item: (id: string) => id, building: (id: string) => id },
        },
      ],
    });
    const fixture = TestBed.createComponent(ArrangementPage);
    fixture.componentRef.setInput('id', 'snapshot-1');
    if (area)
      for (const [corner, value] of Object.entries(area))
        fixture.componentRef.setInput(corner, value);
    // The sidebar the shell puts beside the page: the settings are a tab of it.
    const sidebar = TestBed.createComponent(Sidebar);
    await fixture.whenStable();
    await sidebar.whenStable();
    const element: HTMLElement = fixture.nativeElement;
    /** A drag over the map with a key held, shift or ctrl, from one point to another down its diagonal. */
    const drag = async (key: 'shiftKey' | 'ctrlKey', from: number, to: number) => {
      const map = element.querySelector<HTMLElement>('.leaflet-container')!;
      const at = (type: string, x: number) =>
        new MouseEvent(type, { bubbles: true, [key]: true, button: 0, clientX: x, clientY: x });
      map.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, [key]: true, button: 0 }));
      map.dispatchEvent(at('mousedown', from));
      map.dispatchEvent(at('mousemove', to));
      map.dispatchEvent(at('mouseup', to));
      map.dispatchEvent(new PointerEvent('pointerup', { bubbles: true }));
      await fixture.whenStable();
    };
    /** On a touch screen: the select button on, a finger's drag, and one of the choices offered once let go. */
    const select = async (from: number, to: number, choice: string) => {
      const map = element.querySelector<HTMLElement>('.leaflet-container')!;
      const pointer = (type: string, x: number) =>
        new PointerEvent(type, {
          bubbles: true,
          isPrimary: true,
          button: 0,
          clientX: x,
          clientY: x,
        });
      const button = element.querySelector<HTMLButtonElement>('button.area-select')!;
      if (button.getAttribute('aria-pressed') !== 'true') button.click();
      fixture.detectChanges();
      map.dispatchEvent(pointer('pointerdown', from));
      map.dispatchEvent(pointer('pointermove', to));
      map.dispatchEvent(pointer('pointerup', to));
      fixture.detectChanges();
      const offered = [...element.querySelectorAll<HTMLButtonElement>('.area-choices button')];
      offered.find((b) => b.textContent?.includes(choice))?.click();
      await fixture.whenStable();
      return offered.map((b) => b.textContent?.trim());
    };
    const tabs: HTMLElement = sidebar.nativeElement;
    return {
      fixture,
      sidebar,
      element,
      tabs,
      drag,
      select,
      api: api!,
    };
  }

  const arrange = (within: HTMLElement) =>
    [...within.querySelectorAll<HTMLButtonElement>('button')].find((button) =>
      button.textContent?.includes('Arrange'),
    );

  it("lays the game's map picture under its map, as the world map does", async () => {
    const { element } = await render();

    const images = [...element.querySelectorAll('img.leaflet-image-layer')].map((image) =>
      image.getAttribute('src'),
    );
    expect(images).toEqual([
      '/api/v1/catalog/map-art/tiles/0/0',
      '/api/v1/catalog/map-art/tiles/1/0',
    ]);
  });

  it('draws the area to arrange with shift held, with no button for it', async () => {
    const { fixture, element, drag } = await render();
    expect(arrange(element)?.disabled).toBe(true);
    expect(element.textContent).not.toContain('Draw the area');

    await drag('shiftKey', 10, 200);
    await fixture.whenStable();

    expect(arrange(element)?.disabled).toBe(false);
  });

  it('keeps a part of the area out with ctrl held, once there is an area', async () => {
    const { element, drag } = await render();

    await drag('ctrlKey', 20, 100);
    expect(element.textContent).not.toContain('kept out');

    await drag('shiftKey', 10, 300);
    await drag('ctrlKey', 20, 100);
    expect(element.textContent).toContain('1 kept out');
  });

  describe('on a touch screen, with no keys to hold', () => {
    it('draws the area to arrange with the select button and a drag', async () => {
      const { element, select } = await render();

      await select(10, 200, 'Area');

      expect(arrange(element)?.disabled).toBe(false);
    });

    it('stops selecting when a run is asked for, so the map pans again, and offers no selecting over it', async () => {
      // Asked 2026-11-25: the run came back with the map still in select mode. Step 105: over a run shown, an area is
      // only selected after Reset.
      const { element, fixture, select } = await render();
      await select(10, 200, 'Area');
      const button = element.querySelector<HTMLButtonElement>('button.area-select')!;
      expect(button.getAttribute('aria-pressed')).toBe('true');

      arrange(element)!.click();
      await fixture.whenStable();

      expect(element.querySelector('.map.drawing')).toBeNull();
      expect(element.querySelector('button.area-select')).toBeNull();
    });

    it('offers to keep a part out only once there is an area', async () => {
      const { element, select } = await render();

      expect(await select(20, 100, 'Cancel')).not.toContainEqual(
        expect.stringContaining('Keep out'),
      );

      await select(10, 300, 'Area');
      await select(20, 100, 'Keep out');
      expect(element.textContent).toContain('1 kept out');
    });

    it('stays selecting after a choice, so the next part can be drawn at once', async () => {
      const { element, select } = await render();

      await select(10, 300, 'Area');

      expect(element.querySelector('button.area-select')?.getAttribute('aria-pressed')).toBe(
        'true',
      );
      expect(element.querySelector('.area-choices')).toBeNull();
    });
  });

  it('has its settings in a Settings tab, the first, and Arrange above the map', async () => {
    // Step 85 kept them on the page, beside a table of runs that read unclear with a tab; the runs are a list of their
    // own now, so the map can come first (asked 2026-11-25).
    const { element, tabs } = await render();

    expect(
      tabs.querySelector('section.tab[aria-label="Settings"] input[name="plotWidth"]'),
    ).not.toBeNull();
    expect(element.querySelector('input[name="plotWidth"]')).toBeNull();
    expect(arrange(element)).toBeDefined();
  });

  it('says how it works in an Info tab, not above the map', async () => {
    const { element, tabs } = await render();

    expect(tabs.querySelector('.rail button[aria-label="Info"]')).not.toBeNull();
    expect(element.querySelector('.intro')).toBeNull();
  });

  it('lists its runs above the map, as the snapshots are listed, not in a table', async () => {
    const { element } = await render();

    expect(element.querySelector('app-run-picker')).not.toBeNull();
    expect(element.querySelector('table')).toBeNull();
  });

  it('arranges with the settings of their tab, also while the tab is closed', async () => {
    const { element, tabs, fixture, drag, api } = await render();
    await drag('shiftKey', 10, 200);
    tabs.querySelector<HTMLButtonElement>('button[aria-label="Collapse the sidebar"]')!.click();
    await fixture.whenStable();

    arrange(element)!.click();
    await fixture.whenStable();

    const call = api.invoke.mock.calls.find(([fn]) => fn === requestArrangement);
    expect(call?.[1].body.plotWidthMetres).toBe(48);
  });

  it('has no switches for where the factories stand now or for the belts: belts always show', async () => {
    const { element, tabs } = await render();

    const text = (element.textContent ?? '') + (tabs.textContent ?? '');
    expect(text).not.toContain('Where they stand now');
    expect(text).not.toContain('on the floor below');
  });

  describe("the area's factories (asked 2026-11-25: what lights up couldn't be told)", () => {
    async function openFactories(rendered: Awaited<ReturnType<typeof render>>) {
      rendered.tabs
        .querySelector<HTMLButtonElement>('.rail button[aria-label="Factories"]')!
        .click();
      await rendered.sidebar.whenStable();
      return rendered.tabs.querySelector<HTMLElement>('section.tab[aria-label="Factories"]')!;
    }

    it('lists them in a Factories tab', async () => {
      const tab = await openFactories(await render(aroundRubber));

      expect(tab.textContent).toContain('Rubber');
    });

    it('says to draw an area first, before there is one', async () => {
      const tab = await openFactories(await render());

      expect(tab.textContent).toContain('Draw an area');
    });

    it('picks one from the list, as a click on it on the map does, and lets go on a second', async () => {
      const rendered = await render(aroundRubber);
      const tab = await openFactories(rendered);
      const rubber = () => tab.querySelector<HTMLButtonElement>('button.factory')!;

      rubber().click();
      await rendered.fixture.whenStable();
      await rendered.sidebar.whenStable();
      expect(rubber().getAttribute('aria-pressed')).toBe('true');

      rubber().click();
      await rendered.fixture.whenStable();
      await rendered.sidebar.whenStable();
      expect(rubber().getAttribute('aria-pressed')).toBe('false');
    });

    it('names on the map only the factory picked and those lit up with it', async () => {
      // Asked 2026-11-25: every name at once piled up on a phone.
      const rendered = await render(aroundRubber);
      const named = () =>
        [...rendered.element.querySelectorAll('.leaflet-tooltip.factory-label')].map((name) =>
          name.textContent?.trim(),
        );
      expect(named()).toEqual([]);

      const tab = await openFactories(rendered);
      tab.querySelector<HTMLButtonElement>('button.factory')!.click();
      await rendered.fixture.whenStable();

      expect(named()).toEqual(['Rubber']);
    });
  });
});
