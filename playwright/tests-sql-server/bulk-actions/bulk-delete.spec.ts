import { test, expect } from '@fixtures/admin';
import { createCategory, createRestaurant } from '@helpers/data';
import { uniqueName } from '@helpers/random';

test.describe('Phase 7 — Bulk delete (built-in)', () => {
  test('Delete selected redirects to confirmation page with hidden ids', async ({
    page,
    listFor,
    deleteConfirmation,
  }) => {
    const r1 = await createRestaurant(page, { Name: uniqueName('R-BD-1') });
    const r2 = await createRestaurant(page, { Name: uniqueName('R-BD-2') });

    const list = listFor('Restaurant');
    await list.gotoLatest();

    await list.checkRows(r1.id, r2.id);
    await list.runAction('delete_selected');

    await expect(page).toHaveURL(/\/admin\/Restaurant\/action\/delete\//);
    await deleteConfirmation.expectVisible();
    await expect(page.getByText(/2\s+restaurants/i)).toBeVisible();
    await expect(deleteConfirmation.hiddenSelectedIds).toHaveCount(2);
    // Sidebar visible (not popup mode).
    await expect(page.locator('#sidebar')).toBeVisible();
  });

  test('confirm bulk delete removes records and shows success banner', async ({
    page,
    listFor,
    deleteConfirmation,
  }) => {
    const r1 = await createRestaurant(page, { Name: uniqueName('R-BD-3') });
    const r2 = await createRestaurant(page, { Name: uniqueName('R-BD-4') });

    const list = listFor('Restaurant');
    await list.gotoLatest();

    await list.checkRows(r1.id, r2.id);
    await list.runAction('delete_selected');
    await deleteConfirmation.confirm();

    await expect(page).toHaveURL(/\/admin\/Restaurant\/(\?|$)/);
    await expect(list.successMessage).toContainText(/successfully deleted 2 restaurants/i);
    await expect(list.rowByText(r1.name)).toHaveCount(0);
    await expect(list.rowByText(r2.name)).toHaveCount(0);
  });

  test('cancel bulk delete keeps records intact', async ({
    page,
    listFor,
    deleteConfirmation,
  }) => {
    const r1 = await createRestaurant(page, { Name: uniqueName('R-BD-5') });

    const list = listFor('Restaurant');
    await list.gotoLatest();

    await list.checkRows(r1.id);
    await list.runAction('delete_selected');
    await deleteConfirmation.cancel();

    await expect(page).toHaveURL(/\/admin\/Restaurant\/(\?|$)/);
    await list.gotoLatest();
    await expect(list.rowByText(r1.name)).toBeVisible();
  });

  test('singular vs plural formatting: 1 vs N entities', async ({
    page,
    listFor,
    deleteConfirmation,
  }) => {
    const c1 = await createCategory(page, { Name: uniqueName('C-BD-S') });

    const list = listFor('Category');
    await list.gotoLatest();

    await list.checkRows(c1.id);
    await list.runAction('delete_selected');

    await expect(page.getByText(/1\s+category/i)).toBeVisible();
    await deleteConfirmation.confirm();

    await expect(list.successMessage).toContainText(/successfully deleted 1 category/i);
  });

  test('bulk delete on Category', async ({ page, listFor, deleteConfirmation }) => {
    const c1 = await createCategory(page, { Name: uniqueName('C-BD-1') });
    const c2 = await createCategory(page, { Name: uniqueName('C-BD-2') });

    const list = listFor('Category');
    await list.gotoLatest();
    await list.checkRows(c1.id, c2.id);
    await list.runAction('delete_selected');

    await expect(page.getByText(/2\s+categories/i)).toBeVisible();
    await deleteConfirmation.confirm();
    await expect(list.successMessage).toContainText(/successfully deleted 2 categories/i);
  });
});
