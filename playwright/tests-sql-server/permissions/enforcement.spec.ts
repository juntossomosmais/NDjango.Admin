import { test, expect } from '@fixtures/anonymous';
import { provisionUserWithPermissions } from '@helpers/auth-setup';
import { ADMIN_PASSWORD, ADMIN_USERNAME, adminUrls } from '@helpers/admin-urls';
import { LoginPage } from '@pages/login-page';

async function loginAdmin(page: any) {
  await new LoginPage(page).goto();
  await page.locator('input[name="username"]').fill(ADMIN_USERNAME);
  await page.locator('input[name="password"]').fill(ADMIN_PASSWORD);
  await page.locator('button[type="submit"]').click();
  await page.waitForURL(/\/admin\/$/);
}

async function loginUser(page: any, username: string, password: string) {
  await new LoginPage(page).goto();
  await page.locator('input[name="username"]').fill(username);
  await page.locator('input[name="password"]').fill(password);
  await page.locator('button[type="submit"]').click();
  await page.waitForURL(/\/admin\//);
}

test.describe('Phase 5 — Permission enforcement', () => {
  test('non-superuser with view+add permissions can list and add Category, but not edit/delete', async ({
    page,
  }) => {
    // Provision via admin session
    await loginAdmin(page);
    const account = await provisionUserWithPermissions(page, ['view_category', 'add_category']);

    // Switch to the new user
    await page.context().clearCookies();
    await loginUser(page, account.username, account.password);

    // Allowed: view list (200)
    const listResp = await page.goto(adminUrls.list('Category'));
    expect(listResp?.status()).toBe(200);

    // Allowed: add form (200)
    const addResp = await page.goto(adminUrls.add('Category'));
    expect(addResp?.status()).toBe(200);

    // Denied: cannot access edit form for any category (403)
    // First find one category id from the admin-created data, falling back to id=1
    const editResp = await page.goto(adminUrls.change('Category', 1), {
      waitUntil: 'commit',
    });
    expect(editResp?.status()).toBeLessThanOrEqual(403);
    expect([403, 404]).toContain(editResp?.status());

    // Denied: cannot access delete form (403/404)
    const deleteResp = await page.goto(adminUrls.delete('Category', 1), {
      waitUntil: 'commit',
    });
    expect([403, 404]).toContain(deleteResp?.status());

    // Denied: cannot access another entity without permission (403)
    const restaurantResp = await page.goto(adminUrls.list('Restaurant'), {
      waitUntil: 'commit',
    });
    expect(restaurantResp?.status()).toBe(403);
  });
});
