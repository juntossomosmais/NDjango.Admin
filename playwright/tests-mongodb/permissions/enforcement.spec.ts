import { test, expect } from '@fixtures/admin-mongo';
import { provisionMongoUserWithPermissions } from '@helpers/auth-setup-mongo';
import { adminUrls } from '@helpers/admin-urls';

test.describe('Mongo Phase 8 — Permission enforcement', () => {
  test('non-superuser with view_category can list Categories but cannot add', async ({
    browser,
    page,
  }) => {
    // Step 1: as the seeded admin, create a user with only view_category.
    const account = await provisionMongoUserWithPermissions(page, ['view_category']);

    // Step 2: open a clean (no cookies) context for the new user.
    const context = await browser.newContext();
    const userPage = await context.newPage();
    await userPage.goto(adminUrls.login());
    await userPage.locator('input[name="username"]').fill(account.username);
    await userPage.locator('input[name="password"]').fill(account.password);
    await userPage.getByRole('button', { name: /log in/i }).click();
    await expect(userPage).toHaveURL(new RegExp(`${adminUrls.home()}$`));

    // Allowed: list Category.
    const listResp = await userPage.goto(adminUrls.list('Category'), {
      waitUntil: 'commit',
    });
    expect(listResp?.status()).toBe(200);

    // Denied: add Category.
    const addResp = await userPage.goto(adminUrls.add('Category'), {
      waitUntil: 'commit',
    });
    expect(addResp?.status()).toBe(403);

    // Denied: list Restaurant.
    const restResp = await userPage.goto(adminUrls.list('Restaurant'), {
      waitUntil: 'commit',
    });
    expect(restResp?.status()).toBe(403);

    await context.close();
  });
});
