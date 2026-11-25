import { expect, Page, test as base } from '@playwright/test';

import { answerFromRecording, factory, snapshot } from './api';

export { expect } from '@playwright/test';

/**
 * Playwright's test, with the recorded API answers for every test (step 95): it fails when the page asked for
 * something not recorded. A test that changes something answers that call itself, with page.route.
 *
 * It also fails when the page threw (step 98): an error nothing caught, or one Angular logged as it does every error
 * while drawing ("ERROR ..."). Such an error leaves a page half drawn, as the snapshot picker greyed out after its list
 * failed to load once; no test had asked for that. The browser's own lines about a failed request don't count: the
 * recording answers the game's art with a 404 on purpose.
 */
export const test = base.extend<{ api: string[]; thrown: string[] }>({
  api: [
    async ({ page }, use) => {
      const unanswered = await answerFromRecording(page);
      await use(unanswered);
      expect(unanswered, 'API calls without a recorded answer').toEqual([]);
    },
    { auto: true },
  ],
  thrown: [
    async ({ page }, use) => {
      const thrown: string[] = [];
      page.on('pageerror', (error) => thrown.push(error.message));
      page.on('console', (message) => {
        if (message.type() === 'error' && message.text().startsWith('ERROR'))
          thrown.push(message.text().split('\n')[0]);
      });
      await use(thrown);
      expect(thrown, 'Errors the page threw').toEqual([]);
    },
    { auto: true },
  ],
});

/** The test world's machines, with some room: the area of its 3D view and its arrangement. */
export const area = 'x0=243000&y0=-199500&x1=255000&y1=-193000';

export const pages = {
  start: '/snapshots',
  catalog: '/catalog',
  map: `/snapshots/${snapshot}/map`,
  area: `/snapshots/${snapshot}/area?${area}`,
  arrangement: `/snapshots/${snapshot}/arrangement?${area}`,
  factory: `/snapshots/${snapshot}/factories/${factory}`,
  /** The map, centred on one of the factory's smelters. */
  smelter: `/snapshots/${snapshot}/map?focus=Persistent_Level:PersistentLevel.Build_SmelterMk1_C_2147301500`,
};

/**
 * Waits until a page is whole: a World page's snapshot picker comes last, when the browser is idle or after 2 s (app.html,
 * @defer), and on a phone it takes a band above the bar of tabs. A drag planned before it came could end on it: the view
 * under it never hears the finger lift.
 */
export async function whole(page: Page): Promise<void> {
  // As app.ts's picksSnapshot: every World page but an arrangement's and a factory's has it.
  const [, world, snapshot, shown] = new URL(page.url()).pathname.split('/');
  if (world === 'snapshots' && !(snapshot && ['arrangement', 'factories'].includes(shown)))
    await expect(page.locator('app-snapshot-picker button.current')).toBeAttached();
}

/** Whether the device has no hover: a touch screen, as the stylesheets ask. */
export const touch = (page: Page) => page.evaluate(() => matchMedia('(hover: none)').matches);

/** Whether the device is a phone, as the stylesheets' (max-width: 599px) asks. */
export const phone = (page: Page) => (page.viewportSize()?.width ?? 0) < 600;

/** Whether the open tab lies over the page rather than beside it, as sidebar.scss's (max-width: 960px) asks. */
export const covered = (page: Page) => (page.viewportSize()?.width ?? 0) <= 960;

/** Whether the page is as wide as the screen: nothing sticks out sideways, so it doesn't scroll sideways. */
export async function noSidewaysScroll(page: Page): Promise<void> {
  const { scroll, screen } = await page.evaluate(() => ({
    scroll: document.documentElement.scrollWidth,
    screen: document.documentElement.clientWidth,
  }));
  expect(scroll).toBeLessThanOrEqual(screen);
}

/**
 * Whether something lies wholly in the window, and is what a finger touches at its middle: nothing lies over it, as the
 * bar of tabs along a phone's bottom could.
 */
export async function inView(page: Page, selector: string): Promise<void> {
  const box = await page.locator(selector).boundingBox();
  const size = page.viewportSize()!;
  expect(box).not.toBeNull();
  expect(box!.x).toBeGreaterThanOrEqual(0);
  expect(box!.y).toBeGreaterThanOrEqual(0);
  expect(box!.x + box!.width).toBeLessThanOrEqual(size.width);
  expect(box!.y + box!.height).toBeLessThanOrEqual(size.height);
  const touched = await page.evaluate(
    ([selector, x, y]) => {
      const hit = document.elementFromPoint(x as number, y as number);
      return !!hit && !!hit.closest(selector as string);
    },
    [selector, box!.x + box!.width / 2, box!.y + box!.height / 2] as const,
  );
  expect(touched, `${selector} is under something else`).toBe(true);
}

/**
 * Where on screen `selector`'s element is what a finger at (x, y) would touch: the point itself, or the nearest one above
 * it. Something may lie over the element there, as the bar of tabs along a phone's bottom lies over the 3D view.
 */
async function reachable(
  page: Page,
  selector: string,
  x: number,
  y: number,
): Promise<{ x: number; y: number }> {
  const top = await page.evaluate(
    ([selector, x, y]) => {
      const element = document.querySelector(selector as string)!;
      for (let at = y as number; at >= 0; at -= 4) {
        const hit = document.elementFromPoint(x as number, at);
        if (hit && (hit === element || element.contains(hit))) return at;
      }
      return null;
    },
    [selector, x, y] as const,
  );
  if (top === null)
    throw new Error(`Nothing of ${selector} can be touched at x ${x}, at or above y ${y}`);
  return { x, y: top };
}

/**
 * A drag over what `selector` shows, from one fraction of it to another: (0, 0) its top left, (1, 1) its bottom right,
 * of the part that's in the window (on a phone the 3D view runs on below it). First, as a person scrolls to it, the
 * element is scrolled to the window's middle if it isn't wholly in view. A point where something lies over it moves up
 * until the element is what it touches. With the pointer of a mouse, which the pages take as a finger: both are
 * pointer events.
 */
export async function dragOver(
  page: Page,
  selector: string,
  from: [number, number],
  to: [number, number],
  keys: string[] = [],
): Promise<void> {
  await page
    .locator(selector)
    .first()
    .evaluate((element) => {
      const rect = element.getBoundingClientRect();
      if (rect.top < 0 || rect.bottom > innerHeight) element.scrollIntoView({ block: 'center' });
    });
  const whole = (await page.locator(selector).first().boundingBox())!;
  const window = page.viewportSize()!;
  const top = Math.max(whole.y, 0);
  const height = Math.min(whole.y + whole.height, window.height) - top;
  const at = (fraction: [number, number]) =>
    reachable(page, selector, whole.x + whole.width * fraction[0], top + height * fraction[1]);
  const [start, end] = [await at(from), await at(to)];
  for (const key of keys) await page.keyboard.down(key);
  await page.mouse.move(start.x, start.y);
  await page.mouse.down();
  await page.mouse.move(end.x, end.y, { steps: 8 });
  await page.mouse.up();
  for (const key of keys) await page.keyboard.up(key);
}
