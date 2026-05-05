import { test, expect } from '@fixtures/admin';

const ENTITY_NAMES = [
  'category',
  'restaurant',
  'restaurantprofile',
  'ingredient',
  'menuitem',
  'menuitemingredient',
  'gift',
  'authuser',
  'authgroup',
  'authpermission',
  'authgrouppermission',
  'authusergroup',
];

test.describe('Phase 4 — AuthPermission auto-generated', () => {
  test('list contains add/change/delete/view permissions for each entity', async ({
    page,
    listFor,
  }) => {
    const list = listFor('AuthPermission');
    await list.goto();

    // 4 perms × 12 entities = 48 (allow some flexibility)
    await expect(list.paginatorText).toContainText(/\d{2,}\s+auth\s*permission/i);

    // AuthPermission has no SearchFields, so we iterate pages and look for an
    // exact codename match in the codename column.
    const codenamesByPage = new Set<string>();
    for (let pageNum = 1; pageNum <= 10; pageNum++) {
      await list.goto({ page: String(pageNum) });
      const cells = await page.locator('table#result_list tbody tr td').allTextContents();
      if (cells.length === 0) break;
      for (const text of cells) {
        if (/^(add|change|delete|view)_[a-z]+$/.test(text)) {
          codenamesByPage.add(text);
        }
      }
    }

    for (const entityName of ENTITY_NAMES) {
      for (const action of ['add', 'change', 'delete', 'view']) {
        const codename = `${action}_${entityName}`;
        expect(codenamesByPage.has(codename), `permission "${codename}" should exist`).toBe(true);
      }
    }
  });

  test('AuthPermission list is paginated when results exceed page size', async ({
    listFor,
  }) => {
    const list = listFor('AuthPermission');
    await list.goto();

    const total = await list.rows.count();
    if (total >= 25) {
      await expect(list.pagination).toBeVisible();
    }
  });
});
