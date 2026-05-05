import { test, expect } from '@fixtures/admin-mongo';

test.describe('Mongo Phase 4 — Conditional search', () => {
  test('search box visible on Category', async ({ listFor }) => {
    const list = listFor('Category');
    await list.goto();
    await expect(list.searchBox).toBeVisible();
    await expect(list.searchInput).toBeVisible();
    await expect(list.searchSubmitButton).toBeVisible();
  });

  test('search box visible on Restaurant', async ({ listFor }) => {
    const list = listFor('Restaurant');
    await list.goto();
    await expect(list.searchBox).toBeVisible();
  });

  test('search box hidden on Ingredient', async ({ listFor }) => {
    const list = listFor('Ingredient');
    await list.goto();
    await expect(list.searchBox).toHaveCount(0);
  });

  test('search box hidden on MenuItem', async ({ listFor }) => {
    const list = listFor('MenuItem');
    await list.goto();
    await expect(list.searchBox).toHaveCount(0);
  });

  test('search box hidden on RestaurantProfile', async ({ listFor }) => {
    const list = listFor('RestaurantProfile');
    await list.goto();
    await expect(list.searchBox).toHaveCount(0);
  });

  test('search box hidden on Gift', async ({ listFor }) => {
    const list = listFor('Gift');
    await list.goto();
    await expect(list.searchBox).toHaveCount(0);
  });

  test('search filters Category by Name', async ({ listFor }) => {
    const list = listFor('Category');
    await list.goto({ q: 'Italian' });
    await expect(list.rowByText('Italian')).toBeVisible();
    await expect(list.rowByText('Japanese')).toHaveCount(0);
  });

  test('search filters Restaurant by Name', async ({ listFor }) => {
    const list = listFor('Restaurant');
    await list.goto({ q: 'Bella' });
    await expect(list.rowByText('Bella Napoli')).toBeVisible();
  });

  test('search with no match returns 0 rows', async ({ listFor, page }) => {
    const list = listFor('Category');
    await list.goto({ q: 'definitely-not-a-category-zzz' });
    await expect(list.rows).toHaveCount(0);
  });

  test('?q= ignored on a non-searchable entity', async ({ listFor }) => {
    const list = listFor('Ingredient');
    await list.goto({ q: 'something' });
    // All seeded ingredients still visible — the param is silently dropped.
    await expect(list.rows).not.toHaveCount(0);
  });
});
