import { test, expect } from '@fixtures/anonymous';
import { ADMIN_PASSWORD, ADMIN_USERNAME, adminUrls } from '@helpers/admin-urls';

test.describe('Phase 1 — Authentication: login', () => {
  test('redirects unauthenticated /admin/ to /admin/login/?next=/admin/', async ({ page }) => {
    await page.goto(adminUrls.home());
    await expect(page).toHaveURL(/\/admin\/login\/\?next=%2Fadmin%2F$|\/admin\/login\/\?next=\/admin\//);
  });

  test('login page shows username, password and "Log in" button', async ({ loginPage }) => {
    await loginPage.goto();
    await loginPage.expectVisible();
  });

  test('invalid credentials stay on login page with error', async ({ page, loginPage }) => {
    await loginPage.goto();
    await loginPage.login('admin', 'wrong-password');

    await expect(page).toHaveURL(/\/admin\/login\//);
    await expect(page.locator('p.errornote')).toBeVisible();
  });

  test('valid admin/admin redirects to /admin/ and shows welcome', async ({ page, loginPage }) => {
    await loginPage.goto();
    await loginPage.login(ADMIN_USERNAME, ADMIN_PASSWORD);

    await expect(page).toHaveURL(new RegExp(`${adminUrls.home()}$`));
    await expect(page.locator('#user-tools')).toContainText(/welcome,\s*admin/i);
    await expect(page.locator('#user-tools').getByRole('link', { name: /log\s?out/i })).toBeVisible();
  });

  test('?next= param redirects after successful login', async ({ page, loginPage }) => {
    await loginPage.goto('/admin/Category/');
    await loginPage.login(ADMIN_USERNAME, ADMIN_PASSWORD);

    await expect(page).toHaveURL(/\/admin\/Category\/$/);
  });

  test('inactive users are treated as invalid credentials', async ({ page, loginPage }) => {
    test.skip(
      true,
      'Requires creating an inactive user via API/DB; covered by integration tests.'
    );
    await loginPage.goto();
    await loginPage.login('inactive_user', 'whatever');
    await expect(page).toHaveURL(/\/admin\/login\//);
    await expect(page.locator('p.errornote')).toBeVisible();
  });
});
