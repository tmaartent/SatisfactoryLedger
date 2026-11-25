import { Page } from '@playwright/test';

import { factory, snapshot } from './api';
import { covered, dragOver, expect, inView, noSidewaysScroll, pages, test, touch } from './test';

/**
 * The optimal arrangement page (step 91, ADR-0033): on a touch screen the area and the parts to keep out are selected
 * with the select button, then named; with a mouse, with shift and ctrl held.
 */
test.describe('the optimal arrangement', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto(pages.arrangement);
    await expect(page.locator('h1')).toContainText('optimal arrangement');
    await expect(page.locator('.leaflet-container')).toBeVisible();
  });

  test('fits the screen, without sideways scroll', async ({ page }) => {
    await noSidewaysScroll(page);
  });

  test('says how to draw the area with the fingers or the keys it has, in its Info tab', async ({
    page,
  }) => {
    const [shown, hidden] = (await touch(page)) ? ['touch', 'keys'] : ['keys', 'touch'];
    await page.getByRole('button', { name: 'Info', exact: true }).click();

    await expect(page.locator(`.about .${shown}`).first()).toBeVisible();
    await expect(page.locator(`.about .${hidden}`).first()).toBeHidden();
  });

  test('opens on the area to arrange, its map in view without scrolling', async ({ page }) => {
    // Step 100: the map lay below the introduction, the settings and the runs, and showed the whole world.
    await inView(page, '.leaflet-container');
    const area = page.locator('.leaflet-overlay-pane path, .leaflet-overlay-pane canvas').first();
    await expect(area).toBeAttached();
    const map = (await page.locator('.leaflet-container').boundingBox())!;
    expect(map.height).toBeGreaterThan(200);
  });

  test('offers to make a selection the area or a part to keep out, in view, on a touch screen', async ({
    page,
  }) => {
    test.skip(
      !(await touch(page)),
      'With a mouse, shift draws the area and ctrl a part to keep out.',
    );
    await page.getByRole('button', { name: 'Select an area' }).tap();
    await dragOver(page, '.leaflet-container', [0.4, 0.4], [0.6, 0.6]);

    // The area came with the page, so a part of it can be kept out.
    await expect(page.locator('.area-choices button')).toHaveText([/Area/, /Keep out/, /Cancel/]);
    await inView(page, '.area-choices');
  });

  test('stays selecting after a part is kept out, for the next one', async ({ page }) => {
    test.skip(!(await touch(page)), 'With a mouse, ctrl draws each part to keep out.');
    await page.getByRole('button', { name: 'Select an area' }).tap();
    await dragOver(page, '.leaflet-container', [0.4, 0.4], [0.6, 0.6]);
    await page.locator('.area-choices').getByRole('button', { name: 'Keep out' }).tap();

    await expect(page.locator('.area-choices')).toBeHidden();
    await expect(page.locator('.drawing-hint')).toContainText('a part to keep out');
  });

  test('has no select button with a mouse, which has keys to hold', async ({ page }) => {
    test.skip(await touch(page), 'The select button is for a touch screen.');
    await expect(page.getByRole('button', { name: 'Select an area' })).toBeHidden();
  });
});

/**
 * Each Leaflet map a page makes, caught as it's made, so a test can read what it shows: the maps draw on a canvas, with
 * nothing in the page to measure. Leaflet sets window.L as it loads, and an init hook then hears of every map.
 */
async function catchMaps(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const seen = window as unknown as { maps: unknown[] };
    seen.maps = [];
    let leaflet: { Map: { addInitHook(hook: () => void): void } } | undefined;
    Object.defineProperty(window, 'L', {
      configurable: true,
      get: () => leaflet,
      set: (value: typeof leaflet) => {
        leaflet = value;
        value?.Map.addInitHook(function (this: unknown) {
          seen.maps.push(this);
        });
      },
    });
  });
}

interface WorldRectangle {
  x0: number;
  y0: number;
  x1: number;
  y1: number;
}

/** What the page's last map shows, in the world's centimetres, as an area is given; nothing before it's made. */
function shown(page: Page): Promise<WorldRectangle | null> {
  return page.evaluate(() => {
    const maps = (
      window as unknown as {
        maps: {
          getBounds(): Record<'getWest' | 'getEast' | 'getNorth' | 'getSouth', () => number>;
        }[];
      }
    ).maps;
    if (maps.length === 0) return null;
    const view = maps[maps.length - 1].getBounds();
    // A map point is [-y, x] in metres (toMapPoint).
    return {
      x0: view.getWest() * 100,
      x1: view.getEast() * 100,
      y0: -view.getNorth() * 100,
      y1: -view.getSouth() * 100,
    };
  });
}

/** How wide the page's last map shows the world, in centimetres; 0 before it's made. */
const width = async (page: Page) => {
  const view = await shown(page);
  return view ? view.x1 - view.x0 : 0;
};

const within = (inner: WorldRectangle, outer: WorldRectangle | null) =>
  !!outer &&
  inner.x0 >= outer.x0 &&
  inner.x1 <= outer.x1 &&
  inner.y0 >= outer.y0 &&
  inner.y1 <= outer.y1;

/** Two runs of the test world's arrangement, a short way apart: the recording has none. */
const runs = {
  near: { id: 'run-near', area: { x0: 243000, y0: -199500, x1: 255000, y1: -193000 } },
  west: { id: 'run-west', area: { x0: 238000, y0: -192000, x1: 244000, y1: -187000 } },
};

function summary(run: { id: string; area: WorldRectangle }, requestedAt: string) {
  return {
    id: run.id,
    snapshotId: snapshot,
    world: 'Development',
    area: run.area,
    exclusions: [],
    plotWidthMetres: 48,
    plotDepthMetres: 48,
    roadXMetres: 32,
    roadYMetres: 32,
    together: 0,
    spotWeight: 1,
    ceilingMetres: 64,
    supplyLines: false,
    seconds: 60,
    status: 'ready',
    requestedAt,
    computedAt: requestedAt,
    duration: '00:01:00',
    error: null,
    costNow: 1000,
    cost: 660,
    quality: 'optimal',
    beltMetres: null,
    levelsUsed: null,
    unroutedBelts: 0,
    placements: 1,
    lastResortCrossings: 0,
  };
}

/** Answers the world's runs, newest first (the near one, then the one to the west), and each run. */
async function answerRuns(page: Page): Promise<void> {
  const summaries = [
    summary(runs.near, '2026-11-25T14:00:00Z'),
    summary(runs.west, '2026-11-24T10:00:00Z'),
  ];
  await page.route('**/api/v1/world/arrangements?world=Development', (route) =>
    route.fulfill({ json: summaries }),
  );
  for (const run of summaries) {
    // Its one factory on a plot in the middle of its area.
    const { x0, y0, x1, y1 } = run.area;
    const plot = {
      x0: (x0 * 3 + x1) / 4,
      y0: (y0 * 3 + y1) / 4,
      x1: (x0 + x1 * 3) / 4,
      y1: (y0 + y1 * 3) / 4,
    };
    await page.route(`**/api/v1/world/arrangements/${run.id}`, (route) =>
      route.fulfill({
        json: {
          summary: run,
          factories: [{ factoryId: factory, name: 'Iron Plates', now: plot, proposed: plot }],
          fixed: [],
          links: [],
          belts: [],
          unrouted: [],
          ways: [],
        },
      }),
    );
    await page.route(`**/api/v1/world/arrangements/${run.id}/machines`, (route) =>
      route.fulfill({ json: { footprints: [], machines: [] } }),
    );
  }
}

/** Presses with shift held on the map and drags a little, still pressed: with a mouse, how an area is drawn. */
async function startShiftDrag(page: Page): Promise<() => Promise<void>> {
  const box = (await page.locator('.leaflet-container').boundingBox())!;
  await page.keyboard.down('Shift');
  await page.mouse.move(box.x + box.width * 0.4, box.y + box.height * 0.4);
  await page.mouse.down();
  await page.mouse.move(box.x + box.width * 0.6, box.y + box.height * 0.6, { steps: 4 });
  return async () => {
    await page.mouse.up();
    await page.keyboard.up('Shift');
  };
}

/**
 * Step 105 (asked 2026-11-25, on an Android phone): the map in view on arriving, a run's area shown when it's picked, a
 * factory picked zoomed to, and selecting an area only for a new run.
 */
test.describe('the optimal arrangement and its runs', () => {
  test.beforeEach(async ({ page }) => {
    await catchMaps(page);
    await answerRuns(page);
  });

  test('opens on a run from the world map with its map in view, though a tab was open there', async ({
    page,
  }) => {
    await page.goto(pages.map);
    await page.getByRole('button', { name: 'Arrangements', exact: true }).click();
    await page.locator('.arrangements a.run').first().click();

    await expect(page.locator('h1')).toContainText('optimal arrangement');
    await expect(page.locator('.leaflet-container')).toBeVisible();
    await expect(async () => inView(page, '.leaflet-container')).toPass();
  });

  test('shows the area of the run picked from the list, in the middle of the map', async ({
    page,
  }) => {
    await page.goto(`/snapshots/${snapshot}/arrangement?run=${runs.near.id}`);
    await expect.poll(async () => within(runs.near.area, await shown(page))).toBe(true);

    await page.getByRole('button', { name: /^Run: / }).click();
    await page.locator('.run-list .run').nth(1).click();

    await expect.poll(async () => within(runs.west.area, await shown(page))).toBe(true);
    const view = (await shown(page))!;
    const { area } = runs.west;
    // Within 50 m of its middle.
    expect(Math.abs((view.x0 + view.x1) / 2 - (area.x0 + area.x1) / 2)).toBeLessThan(5000);
    expect(Math.abs((view.y0 + view.y1) / 2 - (area.y0 + area.y1) / 2)).toBeLessThan(5000);
  });

  test('centres a factory picked in the Factories tab, the tab closed where it lies over the map', async ({
    page,
  }) => {
    await page.goto(pages.arrangement);
    await expect.poll(() => width(page)).toBeGreaterThan(0);
    // Away from it first: the test world's factory stands in the area the page opens on.
    await page.evaluate(() => {
      const maps = (window as unknown as { maps: { panBy(by: [number, number]): void }[] }).maps;
      maps[maps.length - 1].panBy([300, 200]);
    });
    await page.getByRole('button', { name: 'Factories', exact: true }).click();
    await page.locator('.factories .factory', { hasText: 'Iron Plates' }).click();

    if (covered(page)) await expect(page.locator('app-sidebar .tab')).toBeHidden();
    else await expect(page.locator('app-sidebar .tab')).toBeVisible();
    // Its name, at its middle, in the middle of the map: within a tenth of the map's size.
    await expect(async () => {
      await inView(page, '.factory-label.picked');
      const map = (await page.locator('.leaflet-container').boundingBox())!;
      const name = (await page.locator('.factory-label.picked').boundingBox())!;
      expect(Math.abs(name.x + name.width / 2 - (map.x + map.width / 2))).toBeLessThan(
        map.width / 10,
      );
      expect(Math.abs(name.y + name.height / 2 - (map.y + map.height / 2))).toBeLessThan(
        map.height / 10,
      );
    }).toPass();
  });

  test('offers no selecting while a run is shown, until Reset', async ({ page }) => {
    await page.goto(`/snapshots/${snapshot}/arrangement?run=${runs.near.id}`);
    await expect.poll(async () => within(runs.near.area, await shown(page))).toBe(true);
    const select = page.getByRole('button', { name: 'Select an area' });
    const hint = page.locator('.drawing-hint');

    if (await touch(page)) await expect(select).toBeHidden();
    else {
      // With a mouse, shift and a drag only pans the map while a run is shown.
      const letGo = await startShiftDrag(page);
      await expect(hint).toBeHidden();
      await letGo();
    }

    await page.getByRole('button', { name: 'Reset' }).click();

    if (await touch(page)) await expect(select).toBeVisible();
    else {
      const letGo = await startShiftDrag(page);
      await expect(hint).toBeVisible();
      await letGo();
    }
  });
});

/** A miner outside the area, whose existing belt comes into the area at its west edge. */
const miner = 'Persistent_Level:PersistentLevel.Build_MinerMk2_C_1';

/**
 * A run with one supply line of iron ore: from the miner outside the area to Smelting, on the floor, and running on from
 * a splitter under Smelting to Iron Plates. Iron Plates is the second factory on the line.
 */
async function answerLine(page: Page): Promise<void> {
  const run = { id: 'run-line', area: runs.near.area };
  const at = (x: number) => ({ x, y: -196000, z: 0 });
  const plot = (x: number) => ({ x0: x - 1000, y0: -197000, x1: x + 1000, y1: -195000 });
  const belt = (from: string, to: string, corners: number[], more: object) => ({
    from,
    to,
    corners: corners.map(at),
    pipe: false,
    heights: [0],
    lifts: [],
    levels: [0],
    item: 'Desc_OreIron_C',
    perMinute: 240,
    injection: false,
    overflow: false,
    startsOnBelt: false,
    endsOnBelt: false,
    leavesFloor: false,
    lastResortCrossings: [],
    ...more,
  });
  await page.route('**/api/v1/world/arrangements?world=Development', (route) =>
    route.fulfill({ json: [summary(run, '2026-11-25T15:00:00Z')] }),
  );
  await page.route(`**/api/v1/world/arrangements/${run.id}`, (route) =>
    route.fulfill({
      json: {
        summary: summary(run, '2026-11-25T15:00:00Z'),
        factories: [
          { factoryId: 'smelting', name: 'Smelting', now: plot(247000), proposed: plot(247000) },
          { factoryId: factory, name: 'Iron Plates', now: plot(251000), proposed: plot(251000) },
        ],
        fixed: [],
        links: [],
        belts: [
          belt(miner, 'smelting', [244000, 247000], { leavesFloor: true }),
          belt('smelting', factory, [247000, 251000], { startsOnBelt: true }),
        ],
        unrouted: [],
        ways: [{ spotId: miner, points: [at(200000), at(243000)] }],
      },
    }),
  );
  await page.route(`**/api/v1/world/arrangements/${run.id}/machines`, (route) =>
    route.fulfill({ json: { footprints: [], machines: [] } }),
  );
}

/** How strongly the map draws the existing belt outside the area, dotted; null while there's none. */
function wayOpacity(page: Page): Promise<number | null> {
  return page.evaluate(() => {
    const maps = (
      window as unknown as {
        maps: { eachLayer(each: (layer: { options?: Record<string, unknown> }) => void): void }[];
      }
    ).maps;
    if (maps.length === 0) return null;
    let opacity: number | null = null;
    maps[maps.length - 1].eachLayer((layer) => {
      if (layer.options?.['dashArray'] === '2 4') opacity = layer.options['opacity'] as number;
    });
    return opacity;
  });
}

/**
 * Step 106 (asked 2026-11-25): a factory picked lights up its sources outside the area too, along the existing belts
 * there, not only its belts on the floor up to the area's edge.
 */
test.describe('the optimal arrangement, its sources outside the area', () => {
  test.beforeEach(async ({ page }) => {
    await catchMaps(page);
    await answerLine(page);
  });

  test('lights the way outside the area to the source of a factory picked further along its line', async ({
    page,
  }) => {
    await page.goto(`/snapshots/${snapshot}/arrangement?run=run-line`);
    await expect.poll(() => wayOpacity(page)).toBe(0.8);

    await page.getByRole('button', { name: 'Factories', exact: true }).click();
    await page.locator('.factories .factory', { hasText: 'Iron Plates' }).click();

    // Picked: its name shows on the map.
    await expect(page.locator('.factory-label.picked')).toContainText('Iron Plates');
    await expect.poll(() => wayOpacity(page)).toBe(0.8);
  });
});
