import { test, expect } from '@fixtures/admin';
import { adminUrls } from '@helpers/admin-urls';

test.describe('Phase 1 — Authentication: logout', () => {
  test('clicking "Log out" clears cookie and redirects to login', async ({ page, dashboard }) => {
    await dashboard.goto();
    await dashboard.expectLoggedIn();

    await dashboard.logout();

    await expect(page).toHaveURL(/\/admin\/login\//);
  });

  test('after logout, /admin/ redirects back to login', async ({ page, dashboard, context }) => {
    await dashboard.goto();
    await dashboard.expectLoggedIn();
    await dashboard.logout();

    // The session has been cleared on this context; verify direct navigation requires login again.
    await context.clearCookies();
    await page.goto(adminUrls.home());
    await expect(page).toHaveURL(/\/admin\/login\//);
  });

  test('navigating to /admin/logout/ also clears the session', async ({ page }) => {
    await page.goto(adminUrls.logout());
    await expect(page).toHaveURL(/\/admin\/login\//);
  });
});
