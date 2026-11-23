import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Api } from '../../api/api';
import { getMapArt } from '../../api/fn/catalog/get-map-art';
import { getResourceNodes } from '../../api/fn/catalog/get-resource-nodes';
import { getSnapshot } from '../../api/fn/world/get-snapshot';
import { getSnapshots } from '../../api/fn/world/get-snapshots';
import { getWorldMap } from '../../api/fn/world/get-world-map';
import { traceFactories } from '../../api/fn/world/trace-factories';
import { FactoryTrace } from '../../api/models/factory-trace';
import { CatalogNames } from '../../shared/catalog-names';
import { Sidebar } from '../../sidebar/sidebar';
import { MapPage } from './map-page';

const factory = (factoryId: string, name: string, traceable = true): FactoryTrace => ({
  factoryId,
  name,
  traceable,
  machines: [],
  inputs: [],
  outputs: [],
  missing: [],
  logistics: 0,
});

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

// Leaflet redraws its canvas on the next animation frame. Removed before that frame, the map left it to draw on a
// canvas that was gone, an error after the test that failed the run: let it draw first.
afterEach(() => new Promise((drawn) => requestAnimationFrame(drawn)));

async function render(id?: string, mapArt: unknown = null, focus?: string) {
  const answers = new Map<unknown, unknown>([
    [
      getResourceNodes,
      [
        {
          name: 'BP_ResourceNode1',
          resource: 'Desc_OreIron_C',
          purity: 'pure',
          position: { x: 1000, y: 2000, z: 0 },
        },
      ],
    ],
    [getSnapshot, { id, sessionName: 'Desert Start' }],
    [
      getWorldMap,
      {
        machines: [
          {
            id: 'smelter-1',
            className: 'Build_SmelterMk1_C',
            recipe: 'Recipe_IngotIron_C',
            position: { x: 0, y: 0, z: 0 },
            yaw: 0,
          },
        ],
        conveyors: [],
        // A miner on the one resource node.
        providers: [
          {
            id: 'miner-1',
            className: 'Build_MinerMk3_C',
            node: 'BP_ResourceNode1',
            resource: 'Desc_OreIron_C',
            purity: 'pure',
            position: { x: 1000, y: 2000, z: 0 },
            yaw: 0,
          },
        ],
        logistics: [],
        consumers: [],
        footprints: [],
      },
    ],
    [
      traceFactories,
      [factory('f-1', 'Rubber'), factory('f-2', 'Plastic'), factory('f-3', 'Gone', false)],
    ],
  ]);
  const api = {
    invoke: vi.fn((fn: unknown) =>
      fn === getMapArt
        ? mapArt
          ? Promise.resolve(mapArt)
          : // Before the map art is imported, the API has none.
            Promise.reject(new HttpErrorResponse({ status: 404 }))
        : Promise.resolve(answers.get(fn) ?? []),
    ),
    // The analysis, ready: a smelter of no factory, starved.
    invoke$Response: vi.fn(() =>
      Promise.resolve({
        status: 200,
        body: {
          machines: [
            {
              id: 'smelter-1',
              className: 'Build_SmelterMk1_C',
              recipe: 'Recipe_IngotIron_C',
              limit: 'starved',
              utilisation: 0.5,
              starvedOf: {},
              blockedOn: {},
            },
          ],
          generators: [],
          bottlenecks: [],
          summary: { machines: 1, running: 0, starved: 1, outputBlocked: 0, noRecipe: 0 },
          generatorSummary: {
            generators: 0,
            running: 0,
            starved: 0,
            outputBlocked: 0,
            fuelUnknown: 0,
          },
          settled: true,
        },
      }),
    ),
  };
  // Which kinds of node are hidden is kept in the browser: every test starts with none hidden.
  localStorage.clear();
  TestBed.configureTestingModule({
    imports: [MapPage],
    providers: [
      provideRouter([]),
      { provide: Api, useValue: api },
      {
        provide: CatalogNames,
        // The game's icons, imported: Iron Ore has one.
        useValue: {
          icon: (className: string) =>
            className === 'Desc_OreIron_C' ? '/api/v1/catalog/icons/Desc_OreIron_C' : null,
          iconOfItemNamed: () => null,
          item: (item: string) => item,
          building: (name: string) => name,
          recipe: (r: string) => r,
        },
      },
    ],
  });
  const fixture = TestBed.createComponent(MapPage);
  if (id) fixture.componentRef.setInput('id', id);
  if (focus) fixture.componentRef.setInput('focus', focus);
  // The sidebar the shell puts beside the page: the map's factories and snapshots are tabs of it.
  const sidebar = TestBed.createComponent(Sidebar);
  await fixture.whenStable();
  await sidebar.whenStable();
  const tabs = sidebar.nativeElement as HTMLElement;
  /** Opens a tab of the sidebar by its icon on the rail. */
  const open = async (label: string) => {
    (tabs.querySelector(`.rail button[aria-label="${label}"]`) as HTMLButtonElement).click();
    await fixture.whenStable();
    await sidebar.whenStable();
  };
  return { fixture, sidebar, api, element: fixture.nativeElement as HTMLElement, tabs, open };
}

describe('the world map with no snapshot loaded', () => {
  const cells = (tabs: HTMLElement) =>
    Array.from(tabs.querySelectorAll<HTMLButtonElement>('.node-kinds button.kind'));

  it('has a tab of the resource nodes: a row per resource, a cell per purity from impure to pure', async () => {
    const { tabs, open } = await render();

    await open('Resource nodes');

    expect(tabs.querySelectorAll('.node-kinds .row')).toHaveLength(1);
    // Iron Ore has a pure node only: no cell to click for impure or normal.
    expect(cells(tabs).map((cell) => cell.getAttribute('aria-label'))).toEqual([
      'Pure Desc_OreIron_C · 1 node',
    ]);
    const badge = cells(tabs)[0].querySelector('.badge') as HTMLElement;
    expect(badge.querySelector('img')?.getAttribute('src')).toBe(
      '/api/v1/catalog/icons/Desc_OreIron_C',
    );
    expect(badge.style.getPropertyValue('--purity')).toBe('#43a047');
  });

  it('hides that kind of node from the map when its cell is clicked, and shows it again on a second click', async () => {
    const { fixture, element, tabs, open } = await render();
    await open('Resource nodes');
    const cell = cells(tabs)[0];
    expect(cell.getAttribute('aria-pressed')).toBe('true');

    cell.click();
    await fixture.whenStable();
    expect(element.querySelector('.node-pin')).toBeNull();
    expect(cell.getAttribute('aria-pressed')).toBe('false');

    cell.click();
    await fixture.whenStable();
    expect(element.querySelector('.node-pin')).not.toBeNull();
  });

  it('hides every node at once, and shows them all again', async () => {
    const { fixture, element, tabs, open } = await render();
    await open('Resource nodes');
    const button = (label: string) =>
      Array.from(tabs.querySelectorAll<HTMLButtonElement>('.node-kinds .all button')).find(
        (candidate) => candidate.textContent?.trim() === label,
      ) as HTMLButtonElement;

    button('Hide all').click();
    await fixture.whenStable();
    expect(element.querySelector('.node-pin')).toBeNull();

    button('Show all').click();
    await fixture.whenStable();
    expect(element.querySelector('.node-pin')).not.toBeNull();
  });

  it('asks for the resource nodes and the snapshots beside it, nothing of a snapshot', async () => {
    const { api } = await render();

    expect(api.invoke.mock.calls.map(([fn]) => fn)).toEqual(
      expect.arrayContaining([getResourceNodes, getSnapshots]),
    );
    expect(
      api.invoke.mock.calls.every(
        ([fn]) => fn === getResourceNodes || fn === getSnapshots || fn === getMapArt,
      ),
    ).toBe(true);
    expect(api.invoke$Response).not.toHaveBeenCalled();
  });

  it("draws a resource node as a pin: its resource's icon on its purity's colour, on a stick to where it lies", async () => {
    const { element } = await render();

    const pin = element.querySelector('.node-pin') as HTMLElement | null;
    expect(pin?.querySelector('.node-marker img')?.getAttribute('src')).toBe(
      '/api/v1/catalog/icons/Desc_OreIron_C',
    );
    // Pure: green, behind the icon.
    expect(pin?.style.getPropertyValue('--purity')).toBe('#43a047');
  });

  it('sizes the resource nodes for how far the map is zoomed in', async () => {
    const { element } = await render();

    const map = element.querySelector('.leaflet-container') as HTMLElement;
    expect(map.style.getPropertyValue('--node-size')).toMatch(/^\d+px$/);
  });

  it('offers nothing that needs a snapshot', async () => {
    const { element } = await render();

    const text = element.querySelector('.page-header')?.textContent ?? '';
    expect(text).not.toContain('Findings');
    expect(text).not.toContain('Open an area in 3D');
    expect(element.querySelector('.factories')).toBeNull();
  });

  it('has the snapshots in the sidebar, and no factories there', async () => {
    const { tabs } = await render();

    expect(
      [...tabs.querySelectorAll('.rail button')].map((b) => b.getAttribute('aria-label')),
    ).toEqual(['Snapshots', 'Resource nodes']);
    expect(tabs.querySelector('app-snapshots-sidebar')).not.toBeNull();
  });
});

describe('the world map of a snapshot', () => {
  it('draws a pin for a resource node a miner stands on too, saying what stands on it', async () => {
    const { element } = await render('s1');

    const pin = element.querySelector('.node-pin') as HTMLElement | null;
    expect(pin?.querySelector('.node-marker img')?.getAttribute('src')).toBe(
      '/api/v1/catalog/icons/Desc_OreIron_C',
    );
  });

  it('has no legend entry for the resource nodes: the pins speak for themselves', async () => {
    const { element } = await render('s1');

    const legend = element.querySelector('.legend')?.textContent ?? '';
    expect([legend.includes('Free node'), legend.includes('Resource node')]).toEqual([
      false,
      false,
    ]);
  });

  const names = (element: HTMLElement, selector: string) =>
    [...element.querySelectorAll(selector)].map((entry) => entry.textContent?.trim());

  it('opens the sidebar on the overview of its findings, with its factories and the snapshots in the tabs after it', async () => {
    const { tabs } = await render('snapshot-1');

    expect(
      [...tabs.querySelectorAll('.rail button')].map((b) => b.getAttribute('aria-label')),
    ).toEqual(['Info', 'Factories', 'Snapshots', 'Resource nodes']);
    expect(tabs.querySelector('.info')?.textContent).toMatch(/1\s*starved/);
  });

  it('lists its factories in the sidebar, not beside the map', async () => {
    const { element, tabs, open } = await render('snapshot-1');

    await open('Factories');

    expect(names(tabs, '.factories .factory-name')).toEqual(['Rubber', 'Plastic', 'Gone']);
    expect(element.querySelector('.factories')).toBeNull();
  });

  it('marks the count clicked in the overview as shown, and lets go of it when a factory is picked', async () => {
    const { fixture, sidebar, tabs, open } = await render('snapshot-1');
    const starved = () =>
      [...tabs.querySelectorAll('.info mat-card')].find((card) =>
        card.textContent?.includes('starved'),
      ) as HTMLElement;

    starved().click();
    await fixture.whenStable();
    await sidebar.whenStable();
    expect(starved().getAttribute('aria-pressed')).toBe('true');

    await open('Factories');
    (tabs.querySelector('.factories button.factory-name') as HTMLButtonElement).click();
    await open('Info');
    expect(starved().getAttribute('aria-pressed')).toBe('false');
  });

  it('highlights a factory picked in the list, and none once it is picked again', async () => {
    const { fixture, sidebar, tabs, open } = await render('snapshot-1');
    await open('Factories');
    const rubber = () => tabs.querySelector('.factories button.factory-name') as HTMLButtonElement;

    rubber().click();
    await fixture.whenStable();
    await sidebar.whenStable();
    expect(names(tabs, '.factories .highlighted .factory-name')).toEqual(['Rubber']);

    rubber().click();
    await fixture.whenStable();
    await sidebar.whenStable();
    expect(names(tabs, '.factories .highlighted .factory-name')).toEqual([]);
  });

  it('offers to explain a held-back machine of no factory, in its tooltip', async () => {
    const { element } = await render('snapshot-1', null, 'smelter-1');

    expect(element.querySelector('.leaflet-tooltip')?.textContent).toContain(
      'Click to explain why',
    );
  });

  it('offers no factory to highlight that cannot be traced', async () => {
    const { tabs, open } = await render('snapshot-1');
    await open('Factories');

    const gone = [...tabs.querySelectorAll('.factories button.factory-name')].find(
      (button) => button.textContent?.trim() === 'Gone',
    ) as HTMLButtonElement;
    expect(gone.disabled).toBe(true);
  });

  it('has no switches for colouring belts or hiding layers, and no factory drop-down', async () => {
    const { element } = await render('snapshot-1');

    expect(element.textContent).not.toContain('Colour belts by what they carry');
    expect(element.querySelector('.leaflet-control-layers')).toBeNull();
    expect(element.querySelector('mat-select')).toBeNull();
  });
});

describe("the game's map picture under the world map", () => {
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
  const images = (element: HTMLElement) =>
    [...element.querySelectorAll('img.leaflet-image-layer')].map((image) =>
      image.getAttribute('src'),
    );

  it('lays each tile of the map art under the map, also with no snapshot loaded', async () => {
    const { element } = await render(undefined, art);

    expect(images(element)).toEqual([
      '/api/v1/catalog/map-art/tiles/0/0',
      '/api/v1/catalog/map-art/tiles/1/0',
    ]);
  });

  it("lays it under a snapshot's map too", async () => {
    const { element } = await render('snapshot-1', art);

    expect(images(element)).toHaveLength(2);
  });

  it('shows the map without a picture before the map art is imported', async () => {
    const { element } = await render();

    expect(images(element)).toEqual([]);
    expect(element.textContent).not.toContain("couldn't be loaded");
  });
});
