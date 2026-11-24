import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Api } from '../../api/api';
import { getMapArt } from '../../api/fn/catalog/get-map-art';
import { getWorldMap } from '../../api/fn/world/get-world-map';
import { previewFactory } from '../../api/fn/world/preview-factory';
import { traceFactories } from '../../api/fn/world/trace-factories';
import { CatalogNames } from '../../shared/catalog-names';
import { Sidebar } from '../../sidebar/sidebar';
import { ArrangementPage } from './arrangement-page';

// jsdom draws no canvas, and Leaflet draws on one: a context where everything does nothing.
beforeEach(() => {
  const nothing: unknown = new Proxy(() => nothing, { get: () => nothing });
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(
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
  machines: [{ id: 'm-1', className: 'Build_ConstructorMk1_C', recipe: null }],
  inputs: [],
  outputs: [],
  missing: [],
  logistics: 0,
};

describe('the optimal arrangement', () => {
  async function render() {
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
    // The sidebar the shell puts beside the page: the settings are a tab of it.
    const sidebar = TestBed.createComponent(Sidebar);
    await fixture.whenStable();
    await sidebar.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    /** A drag over the map with a key held, shift or ctrl, from one point to another down its diagonal. */
    const drag = async (key: 'shiftKey' | 'ctrlKey', from: number, to: number) => {
      const map = element.querySelector('.leaflet-container') as HTMLElement;
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
      const map = element.querySelector('.leaflet-container') as HTMLElement;
      const pointer = (type: string, x: number) =>
        new PointerEvent(type, {
          bubbles: true,
          isPrimary: true,
          button: 0,
          clientX: x,
          clientY: x,
        });
      const button = element.querySelector('button.area-select') as HTMLButtonElement;
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
    return {
      fixture,
      sidebar,
      element,
      tabs: sidebar.nativeElement as HTMLElement,
      drag,
      select,
      api: api!,
    };
  }

  const arrange = (within: HTMLElement) =>
    [...within.querySelectorAll('button')].find((button) =>
      button.textContent?.includes('Arrange'),
    ) as HTMLButtonElement | undefined;

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

  it('has its settings and Arrange on the page, not in the sidebar', async () => {
    const { element, tabs } = await render();

    expect(element.querySelector('input[name="plotWidth"]')).not.toBeNull();
    expect(arrange(element)).toBeDefined();
    expect(tabs.querySelector('section.tab[aria-label="Settings"]')).toBeNull();
  });

  it('has no Factories tab: a factory is picked by clicking it on the map', async () => {
    const { tabs } = await render();

    expect(tabs.querySelector('section.tab[aria-label="Factories"]')).toBeNull();
    expect(tabs.querySelector('.rail button[aria-label="Factories"]')).toBeNull();
  });

  it('has no switches for where the factories stand now or for the belts: belts always show', async () => {
    const { element, tabs } = await render();

    const text = (element.textContent ?? '') + (tabs.textContent ?? '');
    expect(text).not.toContain('Where they stand now');
    expect(text).not.toContain('on the floor below');
  });
});
