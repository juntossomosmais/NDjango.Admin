import { test, expect } from '@fixtures/admin';
import { adminUrls } from '@helpers/admin-urls';

test.describe('Phase 10 — Final logout (session cleared)', () => {
  test('after logout, accessing /admin/ redirects back to /admin/login/', async ({
    page,
    dashboard,
  }) => {
    await dashboard.goto();
    await dashboard.expectLoggedIn();

    await dashboard.logout();
    await expect(page).toHaveURL(/\/admin\/login\//);

    // Cookie cleared on logout — confirm direct nav redirects.
    await page.goto(adminUrls.home());
    await expect(page).toHaveURL(/\/admin\/login\//);
  });
});
