import AxeBuilder from '@axe-core/playwright';
import { Page } from '@playwright/test';

import { expect, pages, phone, test, touch, whole } from './test';

/**
 * Every page on every device, against rules that hold everywhere (step 95, asked 2026-11-24): written once, so a new page
 * needs only its address in the list below. The rules are what most of the touch-screen fixes of steps 90 to 95 were
 * about, without a test of their own.
 */
const sweep: Record<
  string,
  { path: string; ready: (page: Page) => Promise<void>; fills?: boolean }
> = {
  'the start map': {
    path: pages.start,
    ready: (page) => expect(page.locator('.leaflet-container')).toBeVisible(),
    fills: true,
  },
  'the catalog': { path: pages.catalog, ready: (page) => expect(page.locator('h1')).toBeVisible() },
  'the world map': {
    path: pages.map,
    ready: (page) => expect(page.locator('h1')).toContainText('Development'),
    fills: true,
  },
  'the 3D view': {
    path: pages.area,
    ready: (page) => expect(page.locator('.page-header')).toContainText('5 machines'),
  },
  'a factory': {
    path: pages.factory,
    ready: (page) => expect(page.locator('h1')).toContainText('Iron Plates'),
  },
  'the optimal arrangement': {
    path: pages.arrangement,
    ready: (page) => expect(page.locator('.leaflet-container')).toBeVisible(),
  },
};

/** What a finger or the mouse acts on. */
const controls =
  'button, a[href], input, select, textarea, [role="button"], [role="tab"], [role="checkbox"]';

/**
 * The controls that something else covers: at their middle, what a finger touches isn't them. Under something fixed to
 * the window, as the bar of tabs along a phone's bottom, the page is scrolled to bring the control to the middle first,
 * as a person would; only the page, not what scrolls inside it, which could carry the control away from what covers it.
 * A form field's own label doesn't count: a tap on it reaches the field. Hidden controls don't count.
 */
function coveredControls(selector: string): string[] {
  const nameOf = (element: Element) =>
    element.getAttribute('aria-label') ??
    element.getAttribute('title') ??
    element.textContent?.trim().replace(/\s+/g, ' ').slice(0, 30) ??
    '';
  const describe = (element: Element) => `${element.tagName.toLowerCase()} "${nameOf(element)}"`;
  const hitAt = (element: HTMLElement) => {
    const box = element.getBoundingClientRect();
    return document.elementFromPoint(box.left + box.width / 2, box.top + box.height / 2);
  };
  const free = (element: HTMLElement, hit: Element | null) =>
    !hit ||
    element.contains(hit) ||
    hit.contains(element) ||
    (!!element.closest('mat-form-field') &&
      hit.closest('mat-form-field') === element.closest('mat-form-field'));

  const fixed = (element: Element) => {
    for (let at: Element | null = element; at; at = at.parentElement)
      if (getComputedStyle(at).position === 'fixed') return true;
    return false;
  };

  const covered: string[] = [];
  const scrolled = [scrollX, scrollY];
  for (const element of document.querySelectorAll<HTMLElement>(selector)) {
    const style = getComputedStyle(element);
    const box = element.getBoundingClientRect();
    if (
      style.visibility !== 'visible' ||
      Number(style.opacity) === 0 ||
      box.width < 4 ||
      box.height < 4
    )
      continue;
    // A control drawn hidden for a screen reader, as Material's slide toggle keeps its own input, isn't touched.
    if (element.closest('.cdk-visually-hidden, [aria-hidden="true"]')) continue;
    let hit = hitAt(element);
    if (free(element, hit)) continue;
    if (hit && fixed(hit)) {
      const middle = element.getBoundingClientRect();
      scrollTo(scrollX, scrollY + middle.top + middle.height / 2 - innerHeight / 2);
      hit = hitAt(element);
    }
    if (!free(element, hit)) {
      const owner = hit!.closest(selector) ?? hit!;
      covered.push(
        `${describe(element)} under ${describe(owner)} (${hit!.tagName.toLowerCase()}.${String(hit!.className).split(' ')[0]})`,
      );
    }
    scrollTo(scrolled[0], scrolled[1]);
  }
  return covered;
}

/**
 * The controls in the window smaller than 24 × 24 px, the least WCAG 2.2 (AA, target size) asks for a finger. A
 * Material button counts with its touch target, an invisible larger area it reacts to. Links in running text are left
 * out, as WCAG does.
 */
function smallTargets(selector: string): string[] {
  const small: string[] = [];
  for (const element of document.querySelectorAll<HTMLElement>(selector)) {
    const style = getComputedStyle(element);
    const own = element.getBoundingClientRect();
    if (style.visibility !== 'visible' || style.display === 'inline' || own.width === 0) continue;
    if (own.bottom < 0 || own.top > innerHeight || own.right < 0 || own.left > innerWidth) continue;
    if (element.closest('.cdk-visually-hidden, [aria-hidden="true"]')) continue;
    const target =
      element.querySelector('.mat-mdc-button-touch-target')?.getBoundingClientRect() ?? own;
    const [width, height] = [
      Math.max(own.width, target.width),
      Math.max(own.height, target.height),
    ];
    if (width < 24 || height < 24) {
      const name =
        element.getAttribute('aria-label') ?? element.textContent?.trim().slice(0, 30) ?? '';
      small.push(
        `${element.tagName.toLowerCase()} "${name}" ${Math.round(width)} × ${Math.round(height)} px`,
      );
    }
  }
  return small;
}

for (const [name, { path, ready, fills }] of Object.entries(sweep)) {
  test.describe(name, () => {
    test.beforeEach(async ({ page }) => {
      await page.goto(path);
      await ready(page);
      await whole(page);
    });

    test('fits the screen, without sideways scroll', async ({ page }) => {
      const { scroll, screen } = await page.evaluate(() => ({
        scroll: document.documentElement.scrollWidth,
        screen: document.documentElement.clientWidth,
      }));
      expect(scroll).toBeLessThanOrEqual(screen);
    });

    test('fits a phone without scrolling up and down, when it is made to fill the screen', async ({
      page,
    }) => {
      // A map fills what the screen has above the snapshot picker and the bar of tabs (step 90): if the page scrolls,
      // the map runs on under them, with what's in its corners. Found on the start map: its legend lay under the picker.
      test.skip(!fills || !phone(page), 'Only a map page on a phone is made to fit the screen.');
      const { scroll, screen } = await page.evaluate(() => ({
        scroll: document.documentElement.scrollHeight,
        screen: document.documentElement.clientHeight,
      }));
      expect(scroll).toBeLessThanOrEqual(screen);
    });

    test('covers none of its controls', async ({ page }) => {
      expect(await page.evaluate(coveredControls, controls)).toEqual([]);
    });

    test('has controls big enough for a finger on a touch screen', async ({ page }) => {
      test.skip(!(await touch(page)), 'A mouse points more finely than a finger.');
      expect(await page.evaluate(smallTargets, controls)).toEqual([]);
    });

    test('has nothing axe finds against WCAG A and AA', async ({ page }) => {
      const results = await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
        .analyze();
      const found = results.violations.map(
        (violation) =>
          `${violation.id} (${violation.impact}): ${violation.help}, ${violation.nodes.length}× e.g. ${violation.nodes[0]?.target.join(' ')}`,
      );
      expect(found).toEqual([]);
    });
  });
}
