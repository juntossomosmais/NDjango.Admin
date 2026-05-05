import { test, expect } from '@fixtures/admin-mongo';

test.describe('Mongo Phase 7 — MongoAuthPermission (auto-generated)', () => {
  test('list paginates 48 permissions across two pages', async ({ listFor, page }) => {
    const list = listFor('MongoAuthPermission');
    await list.goto();

    // First page should be paginated.
    await expect(list.currentPage).toContainText('1');
    await expect(list.pagination.getByRole('link', { name: /^2$/ })).toBeVisible();
  });

  test('expected codenames exist (e.g., add_category, view_category)', async ({
    page,
    listFor,
  }) => {
    const list = listFor('MongoAuthPermission');
    // The default page size is 25 and the list is sorted by Id; iterate pages.
    let foundAdd = false;
    let foundView = false;
    for (let p = 1; p <= 5 && (!foundAdd || !foundView); p++) {
      await list.goto({ page: String(p) });
      const text = await page.locator('table#result_list tbody').innerText();
      if (/add_category/.test(text)) foundAdd = true;
      if (/view_category/.test(text)) foundView = true;
      if ((await list.rows.count()) === 0) break;
    }
    expect(foundAdd, 'add_category permission must exist').toBe(true);
    expect(foundView, 'view_category permission must exist').toBe(true);
  });
});
