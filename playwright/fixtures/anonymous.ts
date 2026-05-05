import { test as base, expect } from '@playwright/test';
import { LoginPage } from '@pages/login-page';

type Fixtures = {
  loginPage: LoginPage;
};

export const test = base.extend<Fixtures>({
  storageState: { cookies: [], origins: [] },
  loginPage: async ({ page }, use) => {
    await use(new LoginPage(page));
  },
});

export { expect };
