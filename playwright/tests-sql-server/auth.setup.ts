import { test as setup, expect } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { dirname } from 'node:path';
import { LoginPage } from '@pages/login-page';
import { ADMIN_PASSWORD, ADMIN_USERNAME, adminUrls } from '@helpers/admin-urls';

const ADMIN_AUTH_FILE = '.auth/admin.json';

setup('authenticate as admin', async ({ page }) => {
  mkdirSync(dirname(ADMIN_AUTH_FILE), { recursive: true });

  const loginPage = new LoginPage(page);
  await loginPage.goto();
  await loginPage.expectVisible();
  await loginPage.login(ADMIN_USERNAME, ADMIN_PASSWORD);

  await expect(page).toHaveURL(new RegExp(`${adminUrls.home()}$`));
  await expect(page.getByText(/welcome,\s*admin/i)).toBeVisible();

  await page.context().storageState({ path: ADMIN_AUTH_FILE });
});
