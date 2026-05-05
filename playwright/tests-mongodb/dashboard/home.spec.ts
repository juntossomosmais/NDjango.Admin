import { test, expect } from '@fixtures/admin-mongo';

const RESTAURANT_GROUP = [
  'Categories',
  'Restaurants',
  'Restaurant Profiles',
  'Menu Items',
  'Ingredients',
  'Menu Item Ingredients',
];

const SHOP_GROUP = ['Gifts'];

const AUTH_GROUP = [
  'Mongo Auth Users',
  'Mongo Auth Groups',
  'Mongo Auth Permissions',
  'Mongo Auth Group Permissions',
  'Mongo Auth User Groups',
];

test.describe('Mongo Phase 2 — Dashboard home & sidebar', () => {
  test('home page lists three entity-group sections', async ({ page, dashboard }) => {
    await dashboard.goto();

    await expect(page).toHaveTitle(/Sample Admin \(MongoDB\)/);
    await expect(page.locator('div.app-module')).toHaveCount(3);
  });

  test('Restaurant group lists all six entities', async ({ page, dashboard }) => {
    await dashboard.goto();

    const module = dashboard.appModuleByCaption('Restaurant');
    await expect(module).toBeVisible();

    for (const model of RESTAURANT_GROUP) {
      const row = module
        .locator('tbody tr')
        .filter({ has: page.getByRole('link', { name: model, exact: true }) });
      await expect(row, `Restaurant section should list "${model}"`).toBeVisible();
    }
  });

  test('Shop group lists Gifts', async ({ page, dashboard }) => {
    await dashboard.goto();

    const module = dashboard.appModuleByCaption('Shop');
    await expect(module).toBeVisible();
    for (const model of SHOP_GROUP) {
      const row = module
        .locator('tbody tr')
        .filter({ has: page.getByRole('link', { name: model, exact: true }) });
      await expect(row).toBeVisible();
    }
  });

  test('Authentication and Authorization section lists all five Mongo auth entities', async ({
    page,
    dashboard,
  }) => {
    await dashboard.goto();

    const module = dashboard.appModuleByCaption('Authentication and Authorization');
    await expect(module).toBeVisible();

    for (const model of AUTH_GROUP) {
      const row = module
        .locator('tbody tr')
        .filter({ has: page.getByRole('link', { name: model, exact: true }) });
      await expect(row, `Auth section should list "${model}"`).toBeVisible();
    }
  });

  test('sidebar on entity list pages shows all groups and entities', async ({ page }) => {
    await page.goto('/admin/Category/');
    const sidebar = page.locator('#sidebar');
    await expect(sidebar).toBeVisible();

    await expect(sidebar.locator('input#sidebar-search')).toBeVisible();
    await expect(sidebar.getByRole('heading', { name: /^Restaurant$/ })).toBeVisible();
    await expect(sidebar.getByRole('heading', { name: /^Shop$/ })).toBeVisible();
    await expect(
      sidebar.getByRole('heading', { name: /Authentication and Authorization/i })
    ).toBeVisible();

    for (const model of [...RESTAURANT_GROUP, ...SHOP_GROUP, ...AUTH_GROUP]) {
      await expect(
        sidebar.locator('ul.sidebar-models a').filter({ hasText: model }).first()
      ).toBeVisible();
    }
  });

  test('sidebar filter narrows the model list as the user types', async ({ page }) => {
    await page.goto('/admin/Category/');
    const sidebar = page.locator('#sidebar');
    await sidebar.locator('input#sidebar-search').fill('cat');

    // Categories should remain; unrelated entries should be hidden by JS.
    await expect(
      sidebar.locator('ul.sidebar-models a').filter({ hasText: /^Categories$/ })
    ).toBeVisible();
    await expect(
      sidebar.locator('ul.sidebar-models a:visible').filter({ hasText: /Restaurants/i })
    ).toHaveCount(0);
  });
});
