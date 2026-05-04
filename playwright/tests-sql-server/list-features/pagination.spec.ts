import { test, expect } from '@fixtures/admin';

test.describe('Phase 6 — List pagination', () => {
  test('AuthPermission has multiple pages and navigation works', async ({ page, listFor }) => {
    const list = listFor('AuthPermission');
    await list.goto();

    const total = await list.rows.count();
    test.skip(total < 25, 'Less than a full page; pagination not exercised');

    await expect(list.pagination).toBeVisible();
    await expect(list.currentPage).toContainText('1');

    // Navigate to page 2
    await list.goto({ page: '2' });
    await expect(list.currentPage).toContainText('2');
    await expect(list.rows.first()).toBeVisible();
  });
});
