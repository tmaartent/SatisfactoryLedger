import { defineConfig, devices } from '@playwright/test';

/**
 * Journeys against the real stack (step 97, ADR-0008): the web app with the API, the analysis service, PostgreSQL and
 * RabbitMQ running, nothing recorded. `npm run e2e:stack`, against the running AppHost; CI runs them after a merge to
 * main. On the desktop only: the layout tests cover the devices.
 */
export default defineConfig({
  testDir: './e2e/stack',
  // One journey after another: they share the database.
  workers: 1,
  // An import and its analysis take their time.
  timeout: 180_000,
  forbidOnly: !!process.env['CI'],
  reporter: process.env['CI'] ? [['github'], ['html', { open: 'never' }]] : 'list',
  expect: { timeout: 15_000 },
  use: {
    baseURL: 'http://localhost:4301',
    trace: 'retain-on-failure',
    ...devices['Desktop Chrome'],
    viewport: { width: 1280, height: 800 },
  },
  // Its own port, beside the AppHost's 4200 and the layout tests' 4300. Its proxy finds the API as under the AppHost
  // (proxy.conf.js): CI gives its address as services__api__http__0.
  webServer: {
    command: 'npx ng serve --port 4301',
    url: 'http://localhost:4301',
    reuseExistingServer: !process.env['CI'],
    timeout: 180_000,
  },
});
