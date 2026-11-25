import { expect, pages, test, touch, whole } from './test';

/**
 * A factory's building on the map (step 92, ADR-0033): on a touch screen, where nothing hovers, the first tap shows what
 * it is; a second tap, or a tap on its label, opens the factory. With a mouse a click opens it at once.
 */
test.describe("a factory's building on the map", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto(pages.smelter);
    await expect(page.locator('h1')).toContainText('Development');
    await whole(page);
    // At first a smelter is some 10 px wide, two stacked: zoomed in, as a person would, a tap at the middle hits it.
    // When the map zoomed to the smelter (?focus=) first, fewer clicks take it as close as it goes, and the button is
    // then disabled: a click that waits for it gives up (step 98; it waited out the test).
    const zoomIn = page.getByRole('button', { name: 'Zoom in' });
    for (let times = 0; times < 3; times++)
      await zoomIn.click({ timeout: 3000 }).catch(() => undefined);
    await expect(page.locator('.leaflet-container')).not.toHaveClass(/leaflet-zoom-anim/);
  });

  /**
   * The label shown: the newest. Highlighting the factory draws its buildings anew, and for a moment the label of the
   * old shape fades out beside the new one.
   */
  const label = (page: import('@playwright/test').Page) => page.locator('.leaflet-tooltip').last();

  /** A tap or click on the building the map is centred on: one of the factory's smelters. */
  const middle = async (page: import('@playwright/test').Page) => {
    const map = (await page.locator('.leaflet-container').boundingBox())!;
    return { x: map.width / 2, y: map.height / 2 };
  };

  test('shows on the first tap what it is, and that a second tap opens it', async ({ page }) => {
    test.skip(!(await touch(page)), 'With a mouse, hovering shows it.');
    await page.locator('.leaflet-container').tap({ position: await middle(page) });

    await expect(label(page)).toContainText('Iron Plates');
    await expect(label(page)).toContainText('tap again to open');
    await expect(page).toHaveURL(/\/map/);
  });

  test('opens the factory on a second tap', async ({ page }) => {
    test.skip(!(await touch(page)), 'With a mouse, the first click opens it.');
    await page.locator('.leaflet-container').tap({ position: await middle(page) });
    await expect(label(page)).toContainText('tap again to open');
    await page.locator('.leaflet-container').tap({ position: await middle(page) });

    await expect(page).toHaveURL(/\/factories\//);
    await expect(page.locator('h1')).toContainText('Iron Plates');
  });

  test('opens the factory on a tap on its label', async ({ page }) => {
    test.skip(!(await touch(page)), 'With a mouse, the first click opens it.');
    await page.locator('.leaflet-container').tap({ position: await middle(page) });
    await label(page).tap();

    await expect(page).toHaveURL(/\/factories\//);
  });

  test('opens the factory on a click with a mouse', async ({ page }) => {
    test.skip(await touch(page), 'On a touch screen, the first tap shows it.');
    await page.locator('.leaflet-container').click({ position: await middle(page) });

    await expect(page).toHaveURL(/\/factories\//);
    await expect(page.locator('h1')).toContainText('Iron Plates');
  });
});
