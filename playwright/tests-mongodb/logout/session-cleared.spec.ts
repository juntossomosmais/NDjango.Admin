import { test, expect } from '@fixtures/admin-mongo';
import { adminUrls } from '@helpers/admin-urls';

test.describe('Mongo Phase 11 — Logout', () => {
  test('after logout, /admin/ redirects back to login (cookie cleared)', async ({
    page,
    dashboard,
  }) => {
    await dashboard.goto();
    await dashboard.expectLoggedIn();
    await dashboard.logout();
    await expect(page).toHaveURL(/\/admin\/login\//);

    await page.context().clearCookies();
    await page.goto(adminUrls.home());
    await expect(page).toHaveURL(/\/admin\/login\//);
  });
});
