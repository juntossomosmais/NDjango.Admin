import { test, expect } from '@fixtures/admin';
import { uniqueName } from '@helpers/random';

async function findPermissionId(page: any, codename: string): Promise<number> {
  // AuthPermission has no SearchFields — iterate paginated list with exact match.
  const exact = new RegExp(`^${codename}$`);
  for (let pageNum = 1; pageNum <= 10; pageNum++) {
    await page.goto(`/admin/AuthPermission/?page=${pageNum}`);
    const row = page
      .locator('table#result_list tbody tr')
      .filter({ has: page.locator('td').filter({ hasText: exact }) })
      .first();
    if ((await row.count()) === 0) break;
    const href = await row.locator('a').first().getAttribute('href');
    const m = href?.match(/AuthPermission\/(\d+)\//);
    if (m) return parseInt(m[1], 10);
  }
  throw new Error(`Could not locate permission "${codename}"`);
}

async function createGroupAndReturnId(
  page: any,
  groupName: string,
): Promise<number> {
  await page.goto('/admin/AuthGroup/add/');
  await page.locator('input[name="Name"]').fill(groupName);
  await page.locator('button[type="submit"][name="_save_action"][value="continue"]').click();
  await page.waitForURL(/\/admin\/AuthGroup\/\d+\/change\/$/);
  const m = page.url().match(/AuthGroup\/(\d+)\//);
  if (!m) throw new Error('Could not extract group id');
  return parseInt(m[1], 10);
}

test.describe('Phase 4 — AuthGroupPermission', () => {
  test('assign a permission to a group', async ({ page, formFor, listFor }) => {
    const groupId = await createGroupAndReturnId(page, uniqueName('group-AGP'));
    const permId = await findPermissionId(page, 'view_category');

    const form = formFor('AuthGroupPermission');
    await form.gotoAdd();

    await form.expectFkInput('GroupId', 'AuthGroup');
    await form.expectFkInput('PermissionId', 'AuthPermission');

    await form.submit({ GroupId: groupId, PermissionId: permId }, 'save');

    const list = listFor('AuthGroupPermission');
    await expect(page).toHaveURL(/\/admin\/AuthGroupPermission\/(\?|$)/);

    await list.gotoLatest();
    const row = page.locator('tbody tr').filter({
      has: page.locator(`td`).filter({ hasText: new RegExp(`^${groupId}$`) }),
    }).filter({
      has: page.locator(`td`).filter({ hasText: new RegExp(`^${permId}$`) }),
    });
    await expect(row).toBeVisible();
  });
});
