import { defineConfig, devices } from '@playwright/test';

/**
 * Layout tests in real browsers (step 95, ADR-0008): the web app on a phone, a tablet and a desktop, with the API's
 * answers recorded (e2e/fixtures/api.json), so they run with Node only. `npm run e2e`.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  // Four browsers at a time: with more, a laptop starves the pages of idle time, and the 3D views of frames. CI's
  // runner has fewer cores and picks fewer by itself.
  workers: process.env['CI'] ? undefined : 4,
  // Room for a slow first page and the test itself.
  timeout: 60_000,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  reporter: process.env['CI'] ? [['github'], ['html', { open: 'never' }]] : 'list',
  // Pages load slowly while the dev server serves four browsers at once: give an expectation time to come true. One
  // that holds passes at once, so this only costs time when it fails.
  expect: { timeout: 15_000 },
  use: {
    baseURL: 'http://localhost:4300',
    trace: 'retain-on-failure',
  },
  projects: [
    // The recorded answers against the API's description (step 96): once, without a browser.
    { name: 'recording', testMatch: /recording\.spec\.ts/ },
    // The pages, per device. Not the journeys in e2e/stack, which need the real stack (playwright.stack.config.ts).
    ...[
      { name: 'phone-android', use: { ...devices['Pixel 7'] } },
      { name: 'phone-iphone', use: { ...devices['iPhone 15'] } },
      { name: 'tablet', use: { ...devices['iPad (gen 7)'] } },
      {
        name: 'desktop',
        use: { ...devices['Desktop Chrome'], viewport: { width: 1280, height: 800 } },
      },
    ].map((project) => ({ ...project, testIgnore: [/recording\.spec\.ts/, /[/]stack[/]/] })),
  ],
  // Its own port, beside the app the AppHost serves on 4200.
  webServer: {
    command: 'npx ng serve --port 4300',
    url: 'http://localhost:4300',
    reuseExistingServer: !process.env['CI'],
    timeout: 180_000,
  },
});
