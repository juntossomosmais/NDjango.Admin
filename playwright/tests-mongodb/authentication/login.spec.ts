import { test, expect } from '@fixtures/anonymous-mongo';
import { ADMIN_PASSWORD, ADMIN_USERNAME, adminUrls } from '@helpers/admin-urls';

test.describe('Mongo Phase 1 — Authentication: login', () => {
  test('redirects unauthenticated /admin/ to /admin/login/?next=/admin/', async ({ page }) => {
    await page.goto(adminUrls.home());
    await expect(page).toHaveURL(
      /\/admin\/login\/\?next=%2Fadmin%2F$|\/admin\/login\/\?next=\/admin\//
    );
  });

  test('login page shows username, password and "Log in" button', async ({ loginPage }) => {
    await loginPage.goto();
    await loginPage.expectVisible();
  });

  test('invalid credentials stay on the login page with an error', async ({ page, loginPage }) => {
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
    await expect(
      page.locator('#user-tools').getByRole('link', { name: /log\s?out/i })
    ).toBeVisible();
  });

  test('?next= param redirects after successful login', async ({ page, loginPage }) => {
    await loginPage.goto('/admin/Category/');
    await loginPage.login(ADMIN_USERNAME, ADMIN_PASSWORD);

    await expect(page).toHaveURL(/\/admin\/Category\/$/);
  });
});
