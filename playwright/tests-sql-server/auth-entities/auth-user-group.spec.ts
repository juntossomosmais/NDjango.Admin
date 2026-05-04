import { test, expect } from '@fixtures/admin';
import { uniqueName } from '@helpers/random';

async function createGroupAndReturnId(page: any, groupName: string): Promise<number> {
  await page.goto('/admin/AuthGroup/add/');
  await page.locator('input[name="Name"]').fill(groupName);
  await page.locator('button[type="submit"][name="_save_action"][value="continue"]').click();
  await page.waitForURL(/\/admin\/AuthGroup\/\d+\/change\/$/);
  return parseInt(page.url().match(/AuthGroup\/(\d+)\//)![1], 10);
}

async function createUserAndReturnId(page: any, username: string): Promise<number> {
  await page.goto('/admin/AuthUser/add/');
  await page.locator('input[name="Username"]').fill(username);
  await page.locator('input[name="Password"]').fill('plain-password');
  await page.locator('button[type="submit"][name="_save_action"][value="continue"]').click();
  await page.waitForURL(/\/admin\/AuthUser\/\d+\/change\/$/);
  return parseInt(page.url().match(/AuthUser\/(\d+)\//)![1], 10);
}

test.describe('Phase 4 — AuthUserGroup', () => {
  test('assign a user to a group via the junction admin', async ({
    page,
    formFor,
    listFor,
  }) => {
    const userId = await createUserAndReturnId(page, uniqueName('user-AUG'));
    const groupId = await createGroupAndReturnId(page, uniqueName('group-AUG'));

    const form = formFor('AuthUserGroup');
    await form.gotoAdd();

    await form.expectFkInput('UserId', 'AuthUser');
    await form.expectFkInput('GroupId', 'AuthGroup');

    await form.submit({ UserId: userId, GroupId: groupId }, 'save');

    const list = listFor('AuthUserGroup');
    await expect(page).toHaveURL(/\/admin\/AuthUserGroup\/(\?|$)/);

    await list.gotoLatest();
    const row = page.locator('tbody tr').filter({
      has: page.locator(`td`).filter({ hasText: new RegExp(`^${userId}$`) }),
    }).filter({
      has: page.locator(`td`).filter({ hasText: new RegExp(`^${groupId}$`) }),
    });
    await expect(row).toBeVisible();
  });
});
