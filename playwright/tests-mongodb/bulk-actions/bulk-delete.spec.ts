import { test, expect } from '@fixtures/admin-mongo';
import { createCategory } from '@helpers/data-mongo';
import { uniqueName } from '@helpers/random';

test.describe('Mongo Phase 6 — Bulk delete + flash message', () => {
  test('select two test categories and bulk-delete them with a green success banner', async ({
    page,
    listFor,
    deleteConfirmation,
  }) => {
    const c1 = await createCategory(page, { Name: uniqueName('BulkDel1') });
    const c2 = await createCategory(page, { Name: uniqueName('BulkDel2') });

    const list = listFor('Category');
    await list.gotoLatest();

    await list.checkRows(c1.id, c2.id);
    await list.runAction('delete_selected');

    await expect(page).toHaveURL(/\/admin\/Category\/action\/delete\//);
    await expect(deleteConfirmation.summary.first()).toContainText('2');

    // The form must carry hidden _selected_ids inputs for both records.
    await expect(deleteConfirmation.hiddenSelectedIds).toHaveCount(2);

    await deleteConfirmation.confirm();

    await expect(page).toHaveURL(/\/admin\/Category\/(\?|$)/);
    await expect(list.successMessage).toContainText(/successfully deleted 2/i);

    // Records gone (verify by direct change URL).
    for (const id of [c1.id, c2.id]) {
      const resp = await page.goto(`/admin/Category/${id}/change/`, { waitUntil: 'commit' });
      expect([404, 400]).toContain(resp?.status());
    }
  });

  test('cancel bulk delete leaves records intact', async ({
    page,
    listFor,
    deleteConfirmation,
  }) => {
    const c = await createCategory(page, { Name: uniqueName('BulkCancel') });

    const list = listFor('Category');
    await list.gotoLatest();
    await list.checkRows(c.id);
    await list.runAction('delete_selected');

    await expect(page).toHaveURL(/\/admin\/Category\/action\/delete\//);
    await deleteConfirmation.cancel();

    await expect(page).toHaveURL(/\/admin\/Category\/(\?|$)/);

    // Record is still there: navigating to its edit URL succeeds (cleans up too).
    const resp = await page.goto(`/admin/Category/${c.id}/change/`);
    expect(resp?.status()).toBe(200);
    await page.locator('a.deletelink').click();
    await deleteConfirmation.confirm();
  });

  test('flash message clears on subsequent navigation', async ({
    page,
    listFor,
    deleteConfirmation,
  }) => {
    const c = await createCategory(page, { Name: uniqueName('FlashCat') });
    const list = listFor('Category');
    await list.gotoLatest();
    await list.checkRows(c.id);
    await list.runAction('delete_selected');
    await deleteConfirmation.confirm();

    await expect(list.successMessage).toBeVisible();

    // Navigate away and back — the success banner is gone.
    await page.goto('/admin/Restaurant/');
    await page.goto('/admin/Category/');
    await expect(list.successMessage).toHaveCount(0);
  });
});
