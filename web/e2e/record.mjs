// Records what the API answers for the pages the layout tests open, into e2e/fixtures/api.json (step 95).
// Run it against the running app (the AppHost) after importing the test world, the save in
// tests/SatisfactoryLedger.World.SaveFile.Tests/Fixtures/Development.sav:
//
//   node e2e/record.mjs <snapshot id> [http://localhost:4200]
//
// Map art and icons are the game's art (Coffee Stain's): never recorded, and the tests answer them with a 404.
import { chromium } from '@playwright/test';
import { writeFileSync } from 'node:fs';

const [snapshot, base = 'http://localhost:4200'] = process.argv.slice(2);
if (!snapshot) {
  console.error('Usage: node e2e/record.mjs <snapshot id> [base url]');
  process.exit(1);
}

// A factory in the test world, for the pages that show one (step 96): its two smelters and the constructor making
// plates, as the .NET tests' plates factory. Made once; recording again finds it.
const factoryName = 'Iron Plates';
const machines = [
  'Build_SmelterMk1_C_2147301500',
  'Build_SmelterMk1_C_2147301226',
  'Build_ConstructorMk1_C_2147291547',
].map((name) => `Persistent_Level:PersistentLevel.${name}`);
const known = await fetch(`${base}/api/v1/world/factories?world=Development`).then((r) => r.json());
let factory = known.find((candidate) => candidate.name === factoryName)?.id;
if (!factory) {
  const made = await fetch(`${base}/api/v1/world/factories`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ world: 'Development', name: factoryName, machines }),
  });
  if (!made.ok)
    throw new Error(`The factory couldn't be made: ${made.status} ${await made.text()}`);
  factory = (await made.json()).id;
}

// The test world's machines, with some room: the 3D view of it.
const area = 'x0=243000&y0=-199500&x1=255000&y1=-193000';
const pages = [
  '/snapshots',
  '/catalog',
  `/snapshots/${snapshot}/map`,
  `/snapshots/${snapshot}/area?${area}`,
  `/snapshots/${snapshot}/arrangement?${area}`,
  `/snapshots/${snapshot}/factories/${factory}`,
];
const art = /\/api\/v1\/catalog\/(icons|map-art)/;

const answers = {};
const browser = await chromium.launch();
const page = await browser.newPage();
page.on('response', async (response) => {
  const url = new URL(response.url());
  if (!url.pathname.startsWith('/api/v1/') || art.test(url.pathname)) return;
  if (response.request().method() !== 'GET') return;
  const body = await response.text().catch(() => null);
  if (body === null) return;
  answers[`GET ${url.pathname}${url.search}`] = {
    status: response.status(),
    body: body ? JSON.parse(body) : null,
  };
});
for (const path of pages) {
  await page.goto(base + path);
  // The map waits for the analysis, the 3D view for the world: give them time to ask everything.
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(3000);
}
await browser.close();

// Only what's the test world's: the snapshot list names the player's other worlds too, and the node table comes from
// the player's own resource-nodes.json, which stays out of the repo. Of it, the nodes the test world's providers use.
const list = answers['GET /api/v1/world/snapshots'];
if (list) list.body = list.body.filter((entry) => entry.id === snapshot);
const map = answers[`GET /api/v1/world/snapshots/${snapshot}/map`]?.body;
const nodes = answers['GET /api/v1/catalog/resource-nodes'];
if (nodes) {
  const used = new Set((map?.providers ?? []).map((provider) => provider.node).filter(Boolean));
  nodes.body = nodes.body.filter((node) => used.has(node.name));
}

const sorted = Object.fromEntries(Object.entries(answers).sort(([a], [b]) => a.localeCompare(b)));
writeFileSync(
  new URL('./fixtures/api.json', import.meta.url),
  JSON.stringify({ snapshot, factory, answers: sorted }, null, 1) + '\n',
);
console.log(`Recorded ${Object.keys(sorted).length} answers:`);
for (const key of Object.keys(sorted)) console.log(' ', key, sorted[key].status);
