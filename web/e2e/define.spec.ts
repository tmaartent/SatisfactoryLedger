import { Page } from '@playwright/test';

import { covered, dragOver, expect, inView, pages, test, touch, whole } from './test';

/**
 * Defining a factory in 3D (step 93, ADR-0033): on a touch screen, a box drawn with the select button, then Add, Take
 * out or Cancel; with a mouse, clicks and keys, and the steps in the Info tab.
 */

/** What the chosen machines would make: answered here, as the preview is asked for with a POST, which isn't recorded. */
async function answerPreviews(page: Page): Promise<void> {
  await page.route('**/api/v1/world/snapshots/*/factory-preview', (route) =>
    route.fulfill({
      json: {
        logistics: [],
        outputs: [],
        inputs: [],
        leaving: [{ item: 'Desc_IronIngot_C', perMinute: 30 }],
        arriving: [{ item: 'Desc_OreIron_C', perMinute: 30 }],
        connected: [],
        supplies: [],
      },
    }),
  );
}

test.describe('defining a factory in 3D', () => {
  test.beforeEach(async ({ page }) => {
    await answerPreviews(page);
    await page.goto(pages.area);
    await expect(page.locator('.stage canvas')).toBeVisible();
    // The canvas stands before the world arrives: wait for its machines, or a drag lands on a view still loading.
    await expect(page.locator('.page-header')).toContainText('5 machines');
    await whole(page);
    await page.getByRole('button', { name: 'Define a factory' }).click();
  });

  test('opens its steps beside the view, but not over it on a phone or a tablet', async ({
    page,
  }) => {
    // Where the Info tab would lie over the view, it stays closed until a machine is added (asked 2026-11-24). On a
    // tablet it lay over the select button: found by this test.
    await expect(page.locator('app-sidebar section.tab')).toHaveCount(covered(page) ? 0 : 1);
  });

  test('explains with a mouse how to choose, and leaves that to the hint on a touch screen', async ({
    page,
  }) => {
    if (covered(page)) await page.locator('app-sidebar nav.rail button').first().click();
    const how = page.locator('section.factory ul.how');

    if (await touch(page)) await expect(how).toBeHidden();
    else await expect(how).toBeVisible();
  });

  test('adds the machines in a box drawn with the select button on a touch screen', async ({
    page,
  }) => {
    test.skip(!(await touch(page)), 'With a mouse, shift-drag draws the box.');
    await page.getByRole('button', { name: 'Select a box of machines' }).tap();
    await dragOver(page, '.stage canvas', [0.1, 0.3], [0.9, 0.9]);

    await expect(page.locator('.stage .area-choices button')).toHaveText([
      /Add/,
      /Take out/,
      /Cancel/,
    ]);
    await inView(page, '.stage .area-choices');
    await page.locator('.stage .area-choices').getByRole('button', { name: 'Add' }).tap();

    // The Info tab opens on what they make and take.
    await expect(page.locator('section.factory')).toContainText(/[1-9]\d* machines? chosen/);
    await expect(page.locator('section.factory .preview')).toContainText('Iron Ingot');
  });

  test('keeps the choices in view for a box drawn down to the bottom of the view', async ({
    page,
  }) => {
    test.skip(
      !(await touch(page)),
      'With a mouse, a box is drawn with keys held and needs no choices.',
    );
    await page.getByRole('button', { name: 'Select a box of machines' }).tap();
    // Step 93: below the box they fell off the screen; now above it when there's no room below.
    await dragOver(page, '.stage canvas', [0.2, 0.6], [0.8, 0.99]);

    await expect(page.locator('.stage .area-choices')).toBeVisible();
    await inView(page, '.stage .area-choices');
  });

  test('lets go of a box with Cancel, choosing nothing', async ({ page }) => {
    test.skip(!(await touch(page)), 'With a mouse there are no choices to cancel.');
    await page.getByRole('button', { name: 'Select a box of machines' }).tap();
    await dragOver(page, '.stage canvas', [0.1, 0.3], [0.9, 0.9]);
    await page.locator('.stage .area-choices').getByRole('button', { name: 'Cancel' }).tap();

    await expect(page.locator('.stage .area-choices')).toBeHidden();
    if (covered(page)) await page.locator('app-sidebar nav.rail button').first().click();
    await expect(page.locator('section.factory')).toContainText('0 machines chosen');
  });

  test('adds the machines in a box drawn with shift held, with a mouse', async ({ page }) => {
    test.skip(await touch(page), 'A touch screen has no keys to hold.');
    await dragOver(page, '.stage canvas', [0.1, 0.3], [0.9, 0.9], ['Shift']);

    await expect(page.locator('section.factory')).toContainText(/[1-9]\d* machines? chosen/);
  });
});
