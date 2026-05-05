import { test, expect } from '@fixtures/admin';
import { uniqueName } from '@helpers/random';

test.describe('Phase 4 — AuthGroup CRUD', () => {
  test('create a group and verify it appears in list', async ({ page, listFor, formFor }) => {
    const groupName = uniqueName('group');
    const list = listFor('AuthGroup');
    const form = formFor('AuthGroup');

    await form.gotoAdd();
    await form.submit({ Name: groupName }, 'save');

    await expect(page).toHaveURL(/\/admin\/AuthGroup\/(\?|$)/);
    await list.gotoLatest();
    await expect(list.rowByText(groupName)).toBeVisible();
  });
});
