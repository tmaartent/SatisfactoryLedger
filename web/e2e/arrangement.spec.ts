import { dragOver, expect, inView, noSidewaysScroll, pages, test, touch } from './test';

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
