import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  retries: 1,
  use: {
    baseURL: 'http://localhost:4201',
    headless: true,
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { browserName: 'chromium' },
    },
    {
      name: 'firefox',
      use: { browserName: 'firefox' },
    },
    {
      name: 'webkit',
      use: { browserName: 'webkit' },
    },
  ],
  webServer: [
    {
      command: 'dotnet run --project "../backend"',
      url: 'http://localhost:5260/healthz',
      reuseExistingServer: true,
      timeout: 60_000,
      env: { ASPNETCORE_ENVIRONMENT: 'Development' },
    },
    {
      command: 'npx ng serve --configuration development --port 4201',
      url: 'http://localhost:4201',
      reuseExistingServer: true,
      timeout: 120_000,
    },
  ],
});
