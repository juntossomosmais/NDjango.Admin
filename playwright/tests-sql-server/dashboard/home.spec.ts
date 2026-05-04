import { test, expect } from '@fixtures/admin';

const USER_MODELS = [
  'Categories',
  'Restaurants',
  'Restaurant Profiles',
  'Ingredients',
  'Menu Items',
  'Menu Item Ingredients',
  'Gifts',
];

const AUTH_MODELS = [
  'Auth Users',
  'Auth Groups',
  'Auth Permissions',
  'Auth Group Permissions',
  'Auth User Groups',
];

test.describe('Phase 2 — Dashboard home & sidebar', () => {
  test('home page lists user models and auth section', async ({ page, dashboard }) => {
    await dashboard.goto();

    await expect(page.getByRole('heading', { name: /site administration/i })).toBeVisible();
    await expect(page.locator('div.app-module')).toHaveCount(2);
  });

  test('home page shows user models with Add and Change links', async ({ page, dashboard }) => {
    await dashboard.goto();

    for (const model of USER_MODELS) {
      const link = page
        .locator('div.app-module tbody tr')
        .filter({ has: page.getByRole('link', { name: model, exact: true }) });
      await expect(link, `home should list "${model}"`).toBeVisible();
      await expect(link.locator('a.addlink')).toBeVisible();
      await expect(link.locator('a.changelink')).toBeVisible();
    }
  });

  test('home page shows Authentication and Authorization section', async ({ page, dashboard }) => {
    await dashboard.goto();

    const authModule = dashboard.appModuleByCaption('Authentication and Authorization');
    await expect(authModule).toBeVisible();

    for (const model of AUTH_MODELS) {
      const row = authModule
        .locator('tbody tr')
        .filter({ has: page.getByRole('link', { name: model, exact: true }) });
      await expect(row, `auth section should list "${model}"`).toBeVisible();
    }
  });

  test('sidebar is hidden on dashboard home', async ({ page, dashboard }) => {
    await dashboard.goto();
    // The dashboard home does not pass sidebarGroups, so the sidebar element must not render.
    await expect(page.locator('#sidebar')).toHaveCount(0);
  });

  test('sidebar is visible on entity list pages with all entities', async ({ page }) => {
    await page.goto('/admin/Category/');
    const sidebar = page.locator('#sidebar');
    await expect(sidebar).toBeVisible();

    await expect(sidebar.locator('input#sidebar-search')).toBeVisible();
    await expect(sidebar.getByRole('heading', { name: /Models/i })).toBeVisible();
    await expect(sidebar.getByRole('heading', { name: /Authentication and Authorization/i })).toBeVisible();

    for (const model of [...USER_MODELS, ...AUTH_MODELS]) {
      await expect(
        sidebar.locator('ul.sidebar-models a').filter({ hasText: model }).first()
      ).toBeVisible();
    }
  });
});
