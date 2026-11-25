import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import * as L from 'leaflet';

import { Api } from '../../api/api';
import { getMapArt } from '../../api/fn/catalog/get-map-art';
import { getArrangements } from '../../api/fn/world/get-arrangements';
import { getResourceNodes } from '../../api/fn/catalog/get-resource-nodes';
import { getSnapshot } from '../../api/fn/world/get-snapshot';
import { getWorldMap } from '../../api/fn/world/get-world-map';
import { traceFactories } from '../../api/fn/world/trace-factories';
import { ArrangementSummary } from '../../api/models/arrangement-summary';
import { FactoryTrace } from '../../api/models/factory-trace';
import { CatalogNames } from '../../shared/catalog-names';
import { Sidebar } from '../../sidebar/sidebar';
import { MachineSummary } from '../../api/models/machine-summary';
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

/** A run of the optimal arrangement for the world, ready. */
const run = (id: string, requestedAt: string, unroutedBelts = 0): ArrangementSummary => ({
  id,
  snapshotId: 'snapshot-1',
  world: 'Desert Start',
  requestedAt,
  computedAt: requestedAt,
  status: 'ready',
  quality: 'optimal',
  error: null,
  area: null,
  exclusions: [],
  plotWidthMetres: 48,
  plotDepthMetres: 48,
  roadXMetres: 32,
  roadYMetres: 32,
  ceilingMetres: 64,
  supplyLines: false,
  seconds: 60,
  spotWeight: 1,
  together: 0,
  duration: null,
  costNow: 1000,
  cost: 660,
  beltMetres: 1234,
  levelsUsed: 2,
  unroutedBelts,
  placements: 1,
  lastResortCrossings: 0,
});

async function render(
  id?: string,
  mapArt: unknown = null,
  focus?: string,
  traces: FactoryTrace[] = [
    factory('f-1', 'Rubber'),
    factory('f-2', 'Plastic'),
    factory('f-3', 'Gone', false),
  ],
  // The world's runs of the optimal arrangement, newest first, as the API lists them.
  runs: ArrangementSummary[] = [
    run('run-2', '2026-11-25T14:30:00Z', 1),
    run('run-1', '2026-11-24T09:05:00Z'),
  ],
) {
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
    [
      getSnapshot,
      {
        id,
        sessionName: 'Desert Start',
        counts: {
          machines: 2425,
          logistics: 38699,
          providers: 416,
          consumers: 693,
          connections: 39228,
        },
        // What the import couldn't take in: shown on the Info tab.
        unknownRecipes: ['Recipe_Alternate_Mystery_C'],
        unsupported: [{ className: 'Build_SpaceElevator_C', count: 1 }],
      },
    ],
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
    [traceFactories, traces],
    [getArrangements, runs],
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
          // A miner that could give more, and a freight platform whose rate the save doesn't tell.
          unusedSupply: [
            {
              id: 'miner-1',
              className: 'Build_MinerMk3_C',
              item: 'Desc_OreIron_C',
              delivered: 120,
              offered: 480,
              position: { x: 1000, y: 2000, z: 0 },
              approximate: false,
            },
          ],
          unknownSupply: [
            {
              id: 'platform-1',
              className: 'Build_TrainDockingStation_C',
              confirmed: true,
              delivered: { Desc_OreIron_C: 60 },
              stockMinutes: 12,
              position: null,
              approximate: false,
            },
          ],
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
  /** Opens a tab of the sidebar by its icon on the rail; an open one stays open (a click would collapse it). */
  const open = async (label: string) => {
    const icon = tabs.querySelector(`.rail button[aria-label="${label}"]`) as HTMLButtonElement;
    if (icon.getAttribute('aria-pressed') !== 'true') icon.click();
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

  it('asks for the resource nodes, nothing of a snapshot', async () => {
    const { api } = await render();

    expect(api.invoke.mock.calls.map(([fn]) => fn)).toContain(getResourceNodes);
    expect(api.invoke.mock.calls.every(([fn]) => fn === getResourceNodes || fn === getMapArt)).toBe(
      true,
    );
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
    expect(text).not.toContain('Hold shift');
    expect(element.querySelector('.factories')).toBeNull();
  });

  it('has the resource nodes in the sidebar, and no factories there', async () => {
    const { tabs } = await render();

    expect(
      [...tabs.querySelectorAll('.rail button')].map((b) => b.getAttribute('aria-label')),
    ).toEqual(['Resource nodes']);
  });
});

describe('the world map of a snapshot', () => {
  it('lists the unused supply in a tab: each source, its item, what it gives of what it could, linked to it on the map', async () => {
    const { tabs, open } = await render('s1');

    await open('Unused supply');

    const entries = [...tabs.querySelectorAll('.supply li')];
    expect(entries).toHaveLength(1);
    expect(entries[0].textContent).toContain('Build_MinerMk3_C');
    expect(entries[0].textContent).toContain('Desc_OreIron_C');
    expect(entries[0].textContent).toMatch(/120.*of.*480/);
    expect(entries[0].querySelector('a')?.getAttribute('href')).toContain('focus=miner-1');
  });

  it('lists the unknown supply in a tab: each source and what it is assumed to give', async () => {
    const { tabs, open } = await render('s1');

    await open('Unknown supply');

    const entries = [...tabs.querySelectorAll('.supply li')];
    expect(entries).toHaveLength(1);
    expect(entries[0].textContent).toContain('Build_TrainDockingStation_C');
    expect(entries[0].textContent).toMatch(/Desc_OreIron_C.*60/);
    expect(entries[0].querySelector('a')?.getAttribute('href')).toContain('focus=platform-1');
  });

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

  it('folds the legend behind a button, which opens and closes it: on a phone it covers the map', async () => {
    const { fixture, element } = await render('s1');
    const legend = () => element.querySelector('.legend') as HTMLElement;
    const button = element.querySelector('.legend-toggle') as HTMLButtonElement;
    expect([legend().classList.contains('open'), button.getAttribute('aria-expanded')]).toEqual([
      false,
      'false',
    ]);

    button.click();
    await fixture.whenStable();
    expect([legend().classList.contains('open'), button.getAttribute('aria-expanded')]).toEqual([
      true,
      'true',
    ]);

    button.click();
    await fixture.whenStable();
    expect(legend().classList.contains('open')).toBe(false);
  });

  const names = (element: HTMLElement, selector: string) =>
    [...element.querySelectorAll(selector)].map((entry) => entry.textContent?.trim());

  it('opens the sidebar on the overview of its findings, with its factories and supply in the tabs after it', async () => {
    const { tabs } = await render('snapshot-1');

    expect(
      [...tabs.querySelectorAll('.rail button')].map((b) => b.getAttribute('aria-label')),
    ).toEqual([
      'Info',
      'Factories',
      'Arrangements',
      'Unused supply',
      'Unknown supply',
      'Resource nodes',
    ]);
    expect(tabs.querySelector('.info')?.textContent).toMatch(/1\s*starved/);
  });

  it('says how to see an area in 3D, with no button for it in the header, nor a link to the findings', async () => {
    const { element } = await render('snapshot-1');

    const header = element.querySelector('.page-header') as HTMLElement;
    expect(header.textContent).toContain('Hold shift and drag to see an area in 3D');
    expect(header.textContent).toContain('ctrl to arrange its factories');
    expect(header.textContent).not.toContain('Open an area in 3D');
    expect(header.textContent).not.toContain('Findings');
  });

  describe('on a touch screen, with no keys to hold', () => {
    const pointer = (type: string, x: number, y: number) =>
      new PointerEvent(type, { bubbles: true, isPrimary: true, button: 0, clientX: x, clientY: y });

    async function selectArea() {
      const rendered = await render('snapshot-1');
      const map = rendered.element.querySelector('.leaflet-container') as HTMLElement;
      (rendered.element.querySelector('button.area-select') as HTMLButtonElement).click();
      rendered.fixture.detectChanges();
      map.dispatchEvent(pointer('pointerdown', 10, 10));
      map.dispatchEvent(pointer('pointermove', 110, 110));
      map.dispatchEvent(pointer('pointerup', 110, 110));
      rendered.fixture.detectChanges();
      await rendered.fixture.whenStable();
      return rendered;
    }
    const choice = (element: HTMLElement, label: string) =>
      [...element.querySelectorAll<HTMLButtonElement>('.area-choices button')].find((button) =>
        button.textContent?.includes(label),
      );

    it('has a button to select an area, which a drag then draws instead of moving the map', async () => {
      const { fixture, element } = await render('snapshot-1');
      const select = element.querySelector('button.area-select') as HTMLButtonElement;

      select.click();
      fixture.detectChanges();

      expect(select.getAttribute('aria-pressed')).toBe('true');
      expect(element.querySelector('.drawing-hint')?.textContent).toContain('Drag over an area');
    });

    it('offers to open the area drawn in 3D or to arrange it, once let go', async () => {
      const { element } = await selectArea();

      expect(choice(element, '3D')).toBeTruthy();
      expect(choice(element, 'Arrange')).toBeTruthy();
    });

    it('opens the area drawn in 3D', async () => {
      const { fixture, element } = await selectArea();
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

      choice(element, '3D')!.click();
      await fixture.whenStable();

      expect(navigate).toHaveBeenCalledWith(
        ['/snapshots', 'snapshot-1', 'area'],
        expect.objectContaining({
          queryParams: expect.objectContaining({ x0: expect.any(Number) }),
        }),
      );
    });

    it('opens the optimal arrangement of the area drawn', async () => {
      const { fixture, element } = await selectArea();
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

      choice(element, 'Arrange')!.click();
      await fixture.whenStable();

      expect(navigate).toHaveBeenCalledWith(
        ['/snapshots', 'snapshot-1', 'arrangement'],
        expect.anything(),
      );
    });

    it('lets go of the area drawn on Cancel, and of selecting', async () => {
      const { fixture, element } = await selectArea();

      choice(element, 'Cancel')!.click();
      fixture.detectChanges();

      expect(element.querySelector('.area-choices')).toBeNull();
      expect(element.querySelector('button.area-select')?.getAttribute('aria-pressed')).toBe(
        'false',
      );
    });

    it('offers nothing for a tap without a drag', async () => {
      const { fixture, element } = await render('snapshot-1');
      const map = element.querySelector('.leaflet-container') as HTMLElement;
      (element.querySelector('button.area-select') as HTMLButtonElement).click();

      map.dispatchEvent(pointer('pointerdown', 10, 10));
      map.dispatchEvent(pointer('pointerup', 10, 10));
      fixture.detectChanges();

      expect(element.querySelector('.area-choices')).toBeNull();
    });
  });

  it('opens the area dragged over with shift held in 3D', async () => {
    const { fixture, element } = await render('snapshot-1');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const map = element.querySelector('.leaflet-container') as HTMLElement;
    const at = (type: string, x: number, y: number) =>
      new MouseEvent(type, { bubbles: true, shiftKey: true, button: 0, clientX: x, clientY: y });

    map.dispatchEvent(
      new PointerEvent('pointerdown', { bubbles: true, shiftKey: true, button: 0 }),
    );
    map.dispatchEvent(at('mousedown', 10, 10));
    map.dispatchEvent(at('mousemove', 110, 110));
    map.dispatchEvent(at('mouseup', 110, 110));
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith(['/snapshots', 'snapshot-1', 'area'], expect.anything());
  });

  it('opens the optimal arrangement of the area dragged over with ctrl held', async () => {
    const { fixture, element } = await render('snapshot-1');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const map = element.querySelector('.leaflet-container') as HTMLElement;
    const at = (type: string, x: number, y: number) =>
      new MouseEvent(type, { bubbles: true, ctrlKey: true, button: 0, clientX: x, clientY: y });

    map.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, ctrlKey: true, button: 0 }));
    map.dispatchEvent(at('mousedown', 10, 10));
    map.dispatchEvent(at('mousemove', 110, 110));
    map.dispatchEvent(at('mouseup', 110, 110));
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith(
      ['/snapshots', 'snapshot-1', 'arrangement'],
      expect.objectContaining({
        queryParams: expect.objectContaining({ x0: expect.any(Number), y1: expect.any(Number) }),
      }),
    );
  });

  it('warns on the Info tab of recipes the catalog does not know and buildings the analysis leaves out', async () => {
    const { tabs } = await render('snapshot-1');

    const info = tabs.querySelector('.info')?.textContent ?? '';
    expect(info).toContain("Recipes the catalog doesn't know");
    expect(info).toContain('Recipe_Alternate_Mystery_C');
    expect(info).toContain('Buildings the analysis leaves out');
    expect(info).toMatch(/Build_SpaceElevator_C\s*×\s*1/);
  });

  it('says under the overview what the snapshot holds', async () => {
    const { tabs } = await render('snapshot-1');

    expect(tabs.querySelector('.info app-what-it-holds')?.textContent).toMatch(/2425\s*machines/);
  });

  describe('the Arrangements tab (asked 2026-11-25: the arrangement was reached only by drawing an area)', () => {
    it("lists the world's runs of the optimal arrangement, newest first, each summed up in a line", async () => {
      const { api, tabs, open } = await render('snapshot-1');

      await open('Arrangements');

      expect(api.invoke).toHaveBeenCalledWith(getArrangements, { world: 'Desert Start' });
      expect(names(tabs, '.arrangements .run .what')).toEqual([
        '6 × 6 · roads 4 · 4 · −34% · 1 not routed · Optimal',
        '6 × 6 · roads 4 · 4 · −34% · Optimal',
      ]);
      expect(tabs.querySelectorAll('.arrangements .run .when')).toHaveLength(2);
    });

    it('opens a run on the arrangement page', async () => {
      const { tabs, open } = await render('snapshot-1');

      await open('Arrangements');

      const first = tabs.querySelector('.arrangements a.run') as HTMLAnchorElement;
      expect(first.getAttribute('href')).toBe('/snapshots/snapshot-1/arrangement?run=run-2');
    });

    it('says how to arrange an area while the world has no runs', async () => {
      const { tabs, open } = await render('snapshot-1', null, undefined, undefined, []);

      await open('Arrangements');

      expect(tabs.querySelector('.arrangements')?.textContent).toMatch(
        /None yet\. Hold ctrl and drag over an area/,
      );
    });
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

  /** A click on the map itself, where nothing is drawn. */
  const clickTheMap = async (
    element: HTMLElement,
    fixture: { whenStable: () => Promise<unknown> },
  ) => {
    (element.querySelector('.leaflet-container') as HTMLElement).dispatchEvent(
      new MouseEvent('click', { bubbles: true, clientX: 5, clientY: 5 }),
    );
    await fixture.whenStable();
  };

  it('lets go of the factory picked when the map is clicked anywhere else, so nothing is faded', async () => {
    const { fixture, sidebar, element, tabs, open } = await render('snapshot-1');
    await open('Factories');
    (tabs.querySelector('.factories button.factory-name') as HTMLButtonElement).click();
    await fixture.whenStable();

    await clickTheMap(element, fixture);
    await sidebar.whenStable();

    expect(names(tabs, '.factories .highlighted .factory-name')).toEqual([]);
  });

  it('lets go of the count shown when the map is clicked anywhere else', async () => {
    const { fixture, sidebar, element, tabs } = await render('snapshot-1');
    const starved = () =>
      [...tabs.querySelectorAll('.info mat-card')].find((card) =>
        card.textContent?.includes('starved'),
      ) as HTMLElement;
    starved().click();
    await fixture.whenStable();

    await clickTheMap(element, fixture);
    await sidebar.whenStable();

    expect(starved().getAttribute('aria-pressed')).toBe('false');
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

  describe("a factory's building tapped on a touch screen", () => {
    // The Rubber factory owns the smelter.
    const owning = [
      { ...factory('f-1', 'Rubber'), machines: [{ id: 'smelter-1' } as MachineSummary] },
      factory('f-2', 'Plastic'),
    ];
    // A media query list as the browser gives it: Material's breakpoints listen to it too.
    const screen = (touch: boolean) =>
      vi.stubGlobal(
        'matchMedia',
        vi.fn((query: string) => ({
          matches: touch && query === '(hover: none)',
          media: query,
          addListener: () => undefined,
          removeListener: () => undefined,
        })),
      );
    afterEach(() => vi.unstubAllGlobals());

    /** A tap on the smelter, as the map's canvas passes it on to its shape once it finds it under the finger. */
    async function tap(rendered: Awaited<ReturnType<typeof render>>) {
      const shapes = (
        rendered.fixture.componentInstance as unknown as { shapes: Map<string, L.Path> }
      ).shapes;
      shapes.get('smelter-1')!.fire('click', { originalEvent: new MouseEvent('click') });
      rendered.fixture.detectChanges();
      await rendered.fixture.whenStable();
      return shapes.get('smelter-1')!;
    }

    it('says what it is and highlights its factory at the first tap, staying on the map', async () => {
      screen(true);
      const rendered = await render('snapshot-1', null, undefined, owning);
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

      const smelter = await tap(rendered);

      expect(navigate).not.toHaveBeenCalled();
      expect(
        (
          rendered.fixture.componentInstance as unknown as { factoryId: () => string | null }
        ).factoryId(),
      ).toBe('f-1');
      expect(smelter.isTooltipOpen()).toBe(true);
      expect(smelter.getTooltip()?.getContent()).toContain(
        'Factory <b>Rubber</b> · tap again to open',
      );
    });

    it("opens the factory's page at a second tap on the same building", async () => {
      screen(true);
      const rendered = await render('snapshot-1', null, undefined, owning);
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

      await tap(rendered);
      await tap(rendered);

      expect(navigate).toHaveBeenCalledWith(['/snapshots', 'snapshot-1', 'factories', 'f-1']);
    });

    it("opens the factory's page at a tap on the label the first tap showed", async () => {
      screen(true);
      const rendered = await render('snapshot-1', null, undefined, owning);
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

      const smelter = await tap(rendered);
      smelter.getTooltip()!.getElement()!.click();

      expect(navigate).toHaveBeenCalledWith(['/snapshots', 'snapshot-1', 'factories', 'f-1']);
    });

    it("opens the factory's page at the first click with a mouse, whose pointer shows what it is on the way", async () => {
      screen(false);
      const rendered = await render('snapshot-1', null, undefined, owning);
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

      await tap(rendered);

      expect(navigate).toHaveBeenCalledWith(['/snapshots', 'snapshot-1', 'factories', 'f-1']);
    });
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
