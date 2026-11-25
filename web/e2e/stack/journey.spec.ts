import { APIRequestContext, expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import { area, dragOver, whole } from '../test';

/**
 * A journey against the real stack (step 97, ADR-0008): the web app, the API, the analysis service, PostgreSQL and
 * RabbitMQ, nothing recorded. It checks that they fit together; the layout tests (e2e/*.spec.ts) check each page on each
 * device against recorded answers. Desktop only (asked 2026-11-25): defining a factory on a touch screen is covered there.
 * `npm run e2e:stack`, against the running AppHost.
 *
 * It leaves the database as it found it (asked 2026-11-25): the factory is deleted as its last step, and the snapshot
 * after it. What a failed run left is deleted after it, and before the next.
 */
const fixtures = join(__dirname, '../../../tests');
const docsFile = join(fixtures, 'SatisfactoryLedger.Catalog.Tests/Fixtures/en-US.json');
const saveFile = join(fixtures, 'SatisfactoryLedger.World.SaveFile.Tests/Fixtures/Development.sav');

const world = 'Development';
const name = 'Journey Plates';

/** The ones this run made and hasn't deleted yet. */
let snapshot: string | undefined;
let factory: string | undefined;

/** The journey's factory a failed run left behind: its machines would be taken (BR-W5). */
async function leftBehind(request: APIRequestContext): Promise<string[]> {
  const answer = await request.get(`/api/v1/world/factories?world=${world}`);
  expect(answer.ok()).toBe(true);
  const factories = (await answer.json()) as { id: string; name: string }[];
  return factories.filter((candidate) => candidate.name === name).map(({ id }) => id);
}

test.beforeAll(async ({ request }) => {
  for (const id of await leftBehind(request)) await request.delete(`/api/v1/world/factories/${id}`);

  // A new database knows no buildings or recipes yet: the import needs them (BR-W10). One that has a Catalog keeps it.
  const items = await request.get('/api/v1/catalog/items');
  expect(items.ok()).toBe(true);
  if (((await items.json()) as unknown[]).length > 0) return;
  const imported = await request.post('/api/v1/catalog/imports', {
    multipart: {
      gameVersion: 'test',
      file: { name: 'en-US.json', mimeType: 'application/json', buffer: readFileSync(docsFile) },
    },
  });
  expect(imported.status(), await imported.text()).toBe(201);
});

test.afterEach(async ({ request }) => {
  if (factory) await request.delete(`/api/v1/world/factories/${factory}`);
  if (snapshot) await request.delete(`/api/v1/world/snapshots/${snapshot}`);
  [factory, snapshot] = [undefined, undefined];
});

test('a save imported, a factory of it defined in 3D shows what it makes on its page, and is deleted', async ({
  page,
  request,
}) => {
  await test.step('import the test world: its map opens', async () => {
    await page.goto('/snapshots');
    await whole(page);
    const chooser = page.waitForEvent('filechooser');
    await page.getByRole('button', { name: 'Import a save' }).click();
    const map = page.waitForResponse(
      (response) => /\/api\/v1\/world\/snapshots\/[^/]+\/map$/.test(response.url()),
      { timeout: 60_000 },
    );
    await (await chooser).setFiles(saveFile);

    await expect(page).toHaveURL(/\/snapshots\/[^/]+\/map/, { timeout: 60_000 });
    snapshot = new URL(page.url()).pathname.split('/')[2];
    expect((await map).status()).toBe(200);
    await expect(page.locator('h1')).toContainText(world);
  });

  await test.step('define a factory of its machines in 3D, and save it', async () => {
    await page.goto(`/snapshots/${snapshot}/area?${area}`);
    await expect(page.locator('.page-header')).toContainText('5 machines', { timeout: 30_000 });
    await whole(page);
    await page.getByRole('button', { name: 'Define a factory' }).click();
    await dragOver(page, '.stage canvas', [0.1, 0.3], [0.9, 0.9], ['Shift']);
    await expect(page.locator('section.factory')).toContainText(/[1-9]\d* machines? chosen/);
    await page.getByRole('textbox', { name: 'Name' }).fill(name);

    const saved = page.waitForResponse(
      (response) =>
        response.request().method() === 'POST' &&
        new URL(response.url()).pathname === '/api/v1/world/factories',
    );
    await page.getByRole('button', { name: 'Save', exact: true }).click();
    const answer = await saved;
    expect(answer.status(), await answer.text()).toBe(201);
    factory = ((await answer.json()) as { id: string }).id;
  });

  await test.step('its page shows what it makes', async () => {
    // The Factories tab lists it, as the area's other factories.
    await page.getByRole('link', { name }).click();

    await expect(page).toHaveURL(new RegExp(`/factories/${factory}`));
    // The analysis is made in the background after the import (ADR-0018): the page waits for it.
    await expect(page.locator('h1')).toHaveText(name, { timeout: 60_000 });
    await expect(page.getByRole('region', { name: 'Output' })).toContainText('Iron Plate');
  });

  await test.step('delete it: the map opens, and the factory is gone', async () => {
    await page.getByRole('button', { name: 'Delete', exact: true }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();

    await expect(page).toHaveURL(new RegExp(`/snapshots/${snapshot}/map`));
    expect((await request.get(`/api/v1/world/factories/${factory}`)).status()).toBe(404);
    factory = undefined;
  });
});
