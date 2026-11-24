import { Page } from '@playwright/test';

import recorded from './fixtures/api.json' with { type: 'json' };

/** The test world's snapshot, as recorded: the pages the tests open are about it. */
export const snapshot = recorded.snapshot;

const answers: Record<string, { status: number; body: unknown }> = recorded.answers;

/**
 * Answers the app's API calls from the recording (e2e/record.mjs). Map art and icons, the game's art, aren't recorded:
 * they get a 404, and the pages draw without them. So does anything else not recorded, which the test then lists.
 */
export async function answerFromRecording(page: Page): Promise<string[]> {
  const unanswered: string[] = [];
  await page.route('**/api/v1/**', async (route) => {
    const url = new URL(route.request().url());
    const key = `${route.request().method()} ${url.pathname}${url.search}`;
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
