import { test, expect } from '@fixtures/admin-mongo';

test.describe('Mongo Phase 9 — Pagination', () => {
  test('Ingredient list (5 seeded items) renders no pagination controls', async ({
    listFor,
  }) => {
    const list = listFor('Ingredient');
    await list.goto();
    // Pagination text shows the count even on a single page; the page-link bar
    // only appears once total > pageSize. Default page size is 25, seeded = 5.
    await expect(list.pagination.locator('span.this-page')).toHaveCount(0);
  });

  test('MongoAuthPermission list paginates across two pages (48 / 25 = 2)', async ({
    listFor,
    page,
  }) => {
    const list = listFor('MongoAuthPermission');
    await list.goto();
    await expect(list.paginatorText).toContainText(/permission/i);

    // Sliding-window pagination must include "1" (current) and a link to "2".
    await expect(list.currentPage).toContainText('1');
    const linkToPage2 = list.pagination.getByRole('link', { name: /^2$/ });
    await expect(linkToPage2).toBeVisible();

    // Navigate to page 2 and verify page indicator updates.
    await linkToPage2.click();
    await expect(page).toHaveURL(/page=2/);
    await expect(list.currentPage).toContainText('2');
  });

  test('seeded counts match the documented values', async ({ listFor }) => {
    const expectations: Array<[entity: string, count: number]> = [
      ['Category', 3],
      ['Restaurant', 2],
      ['RestaurantProfile', 2],
      ['Ingredient', 5],
      ['MenuItem', 4],
      ['Gift', 2],
    ];

    for (const [entity, count] of expectations) {
      const list = listFor(entity);
      await list.goto();
      // Tests in this suite create their own records; assertions tolerate ≥ seeded count.
      await expect(list.rows).not.toHaveCount(0);
      const visible = await list.rows.count();
      expect(visible).toBeGreaterThanOrEqual(count);
    }
  });
});
