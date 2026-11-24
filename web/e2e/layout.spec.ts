import {
  dragOver,
  expect,
  inView,
  noSidewaysScroll,
  pages,
  phone,
  test,
  touch,
  whole,
} from './test';

/**
 * The world map and the 3D view on a phone, a tablet and a desktop (step 95, ADR-0033): what steps 90 to 94 fixed by
 * hand, checked in real browsers. Each test runs once per device in playwright.config.ts.
 */
test.describe('the world map', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto(pages.map);
    await expect(page.locator('h1')).toContainText('Development');
    await whole(page);
  });

  test('fits the screen, without sideways scroll', async ({ page }) => {
    await noSidewaysScroll(page);
  });

  test('starts with the sidebar collapsed on a touch screen, and open with a mouse', async ({
    page,
  }) => {
    await expect(page.locator('app-sidebar section.tab')).toHaveCount((await touch(page)) ? 0 : 1);
  });

  test('keeps the snapshot picker in view, also with the sidebar collapsed', async ({ page }) => {
    // Step 90: it hid with the tabs when they collapsed.
    if (!(await touch(page)))
      await page.getByRole('button', { name: 'Collapse the sidebar' }).click();
    await expect(page.locator('app-sidebar section.tab')).toHaveCount(0);

    await expect(page.locator('app-snapshot-picker button.current')).toBeVisible();
    await inView(page, 'app-snapshot-picker button.current');
  });

  test('folds the legend on a phone, where it would cover the map', async ({ page }) => {
    const entries = page.locator('.legend .entries');
    if (phone(page)) await expect(entries).toBeHidden();
    else await expect(entries).toBeVisible();
  });

  test('selects an area with the select button on a touch screen, and offers 3D and Arrange in view', async ({
    page,
  }) => {
    test.skip(!(await touch(page)), 'With a mouse, keys select an area: shift or ctrl and drag.');
    await page.locator('button.area-select').tap();
    await dragOver(page, '.leaflet-container', [0.3, 0.3], [0.6, 0.6]);

    await expect(page.locator('.area-choices')).toBeVisible();
    await inView(page, '.area-choices');
    await expect(page.locator('.area-choices button')).toHaveText([/3D/, /Arrange/, /Cancel/]);
  });

  test('draws the area to arrange with ctrl held, and opens its arrangement', async ({ page }) => {
    test.skip(await touch(page), 'A touch screen has no keys to hold.');
    await dragOver(page, '.leaflet-container', [0.3, 0.3], [0.6, 0.6], ['Control']);

    await expect(page).toHaveURL(/\/arrangement\?/);
  });

  test("shows the snapshot picker's arrow at its right edge", async ({ page }) => {
    test.skip(
      await touch(page),
      'On a touch screen the sidebar starts collapsed, with the picker as an icon.',
    );
    const button = (await page.locator('app-snapshot-picker button.current').boundingBox())!;
    const arrow = (await page
      .locator('app-snapshot-picker button.current mat-icon')
      .last()
      .boundingBox())!;

    expect(button.x + button.width - (arrow.x + arrow.width)).toBeLessThan(24);
  });
});

test.describe('the 3D view', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto(pages.area);
    await expect(page.locator('.stage canvas')).toBeVisible();
    // The canvas stands before the world arrives: wait for its machines, or a drag lands on a view still loading.
    await expect(page.locator('.page-header')).toContainText('5 machines');
    await whole(page);
  });

  test('fits the screen, without sideways scroll', async ({ page }) => {
    await noSidewaysScroll(page);
  });

  test('draws on a canvas as big as its stage, at any pixel ratio', async ({ page }) => {
    // Step 90: on a phone with a pixel ratio of 2.6, the canvas came out twice the stage and the scene off to the side.
    const stage = (await page.locator('.stage').boundingBox())!;
    const canvas = (await page.locator('.stage canvas').boundingBox())!;

    expect(Math.abs(canvas.width - stage.width)).toBeLessThanOrEqual(1);
    expect(Math.abs(canvas.height - stage.height)).toBeLessThanOrEqual(1);
  });

  test('says how to move around with the fingers or the mouse it has', async ({ page }) => {
    const [shown, hidden] = (await touch(page)) ? ['touch', 'mouse'] : ['mouse', 'touch'];

    await expect(page.locator(`.stage .hint .${shown}`)).toBeVisible();
    await expect(page.locator(`.stage .hint .${hidden}`)).toBeHidden();
  });

  test('starts with the sidebar collapsed on a touch screen, so the view is free', async ({
    page,
  }) => {
    await expect(page.locator('app-sidebar section.tab')).toHaveCount((await touch(page)) ? 0 : 1);
  });
});
