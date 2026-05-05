import { test, expect } from '@fixtures/admin-mongo';
import { adminUrls } from '@helpers/admin-urls';

test.describe('Mongo Phase 1 — Authentication: logout', () => {
  test('clicking "Log out" returns to the login page', async ({ page, dashboard }) => {
    await dashboard.goto();
    await dashboard.expectLoggedIn();
    await dashboard.logout();

    await expect(page).toHaveURL(/\/admin\/login\//);
  });

  test('GET /admin/logout/ redirects to login', async ({ page }) => {
    await page.goto(adminUrls.logout());
    await expect(page).toHaveURL(/\/admin\/login\//);
  });
});
