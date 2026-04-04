import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  retries: 0,
  use: {
    baseURL: 'http://localhost:4200',
    headless: true,
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { browserName: 'chromium' },
    },
  ],
  webServer: [
    {
      command: 'dotnet run --project "../backend"',
      url: 'http://localhost:5260/api/products',
      reuseExistingServer: true,
      timeout: 30_000,
    },
    {
      command: 'npx ng serve --configuration development',
      url: 'http://localhost:4200',
      reuseExistingServer: true,
      timeout: 60_000,
    },
  ],
});
