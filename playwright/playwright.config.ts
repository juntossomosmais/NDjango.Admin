import { defineConfig, devices } from '@playwright/test';

const BASE_URL_EF = process.env.PLAYWRIGHT_BASE_URL ?? 'http://localhost:8000';
const BASE_URL_MONGO = process.env.PLAYWRIGHT_MONGO_BASE_URL ?? 'http://localhost:8001';
const ADMIN_PATH = '/admin/';

export default defineConfig({
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1,
  reporter: process.env.CI
    ? [['html', { open: 'never' }], ['github']]
    : [['html', { open: 'on-failure' }], ['list']],
  timeout: 60_000,
  expect: {
    timeout: 10_000,
  },
  use: {
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },
  projects: [
    // EF Core (sample-project on :8000)
    {
      name: 'setup',
      testDir: './tests-sql-server',
      testMatch: /.*\.setup\.ts/,
      use: { baseURL: BASE_URL_EF },
    },
    {
      name: 'chromium',
      testDir: './tests-sql-server',
      use: {
        ...devices['Desktop Chrome'],
        baseURL: BASE_URL_EF,
        storageState: '.auth/admin.json',
      },
      dependencies: ['setup'],
    },
    {
      name: 'firefox',
      testDir: './tests-sql-server',
      use: {
        ...devices['Desktop Firefox'],
        baseURL: BASE_URL_EF,
        storageState: '.auth/admin.json',
      },
      dependencies: ['setup'],
    },
    {
      name: 'webkit',
      testDir: './tests-sql-server',
      use: {
        ...devices['Desktop Safari'],
        baseURL: BASE_URL_EF,
        storageState: '.auth/admin.json',
      },
      dependencies: ['setup'],
    },

    // MongoDB (sample-project-mongodb on :8001)
    {
      name: 'setup-mongo',
      testDir: './tests-mongodb',
      testMatch: /.*\.setup\.ts/,
      use: { baseURL: BASE_URL_MONGO },
    },
    {
      name: 'chromium-mongo',
      testDir: './tests-mongodb',
      use: {
        ...devices['Desktop Chrome'],
        baseURL: BASE_URL_MONGO,
        storageState: '.auth/admin-mongo.json',
      },
      dependencies: ['setup-mongo'],
    },
    {
      name: 'firefox-mongo',
      testDir: './tests-mongodb',
      use: {
        ...devices['Desktop Firefox'],
        baseURL: BASE_URL_MONGO,
        storageState: '.auth/admin-mongo.json',
      },
      dependencies: ['setup-mongo'],
    },
    {
      name: 'webkit-mongo',
      testDir: './tests-mongodb',
      use: {
        ...devices['Desktop Safari'],
        baseURL: BASE_URL_MONGO,
        storageState: '.auth/admin-mongo.json',
      },
      dependencies: ['setup-mongo'],
    },
  ],
  webServer: process.env.PLAYWRIGHT_START_SERVER
    ? {
        command: 'cd ../sample-project/src && dotnet run -- api',
        url: `${BASE_URL_EF}${ADMIN_PATH}login/`,
        reuseExistingServer: !process.env.CI,
        timeout: 180_000,
        stdout: 'pipe',
        stderr: 'pipe',
      }
    : undefined,
});
