import { Page } from '@playwright/test';

import recorded from './fixtures/api.json' with { type: 'json' };

/** The test world's snapshot, as recorded: the pages the tests open are about it. */
export const snapshot = recorded.snapshot;

/** The factory the recorder made in the test world: its two smelters and the constructor making plates. */
export const factory = recorded.factory;

const answers: Record<string, { status: number; body: unknown }> = recorded.answers;

/**
 * What chosen machines would make, for every preview: it's asked for with a POST, which isn't recorded, by defining a
 * factory and by a factory's page, whose 3D view shows its lines. Any answer of the right shape does for layout.
 */
const preview = {
  logistics: [],
  outputs: [],
  inputs: [],
  leaving: [{ item: 'Desc_IronIngot_C', perMinute: 30 }],
  arriving: [{ item: 'Desc_OreIron_C', perMinute: 30 }],
  connected: [],
  supplies: [],
};

/**
 * Answers the app's API calls from the recording (e2e/record.mjs), and every factory preview with the one above. Map art and icons, the game's art, aren't recorded:
 * they get a 404, and the pages draw without them. So does anything else not recorded, which the test then lists.
 */
export async function answerFromRecording(page: Page): Promise<string[]> {
  const unanswered: string[] = [];
  await page.route('**/api/v1/**', async (route) => {
    const url = new URL(route.request().url());
    const key = `${route.request().method()} ${url.pathname}${url.search}`;
    if (route.request().method() === 'POST' && url.pathname.endsWith('/factory-preview')) {
      await route.fulfill({ json: preview });
      return;
    }
    const answer = answers[key];
    if (answer) {
      await route.fulfill({ status: answer.status, json: answer.body });
      return;
    }
    if (!/\/api\/v1\/catalog\/(icons|map-art)/.test(url.pathname)) unanswered.push(key);
    await route.fulfill({ status: 404, json: { title: 'Not recorded' } });
  });
  return unanswered;
}
