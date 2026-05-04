import { test, expect } from '@fixtures/admin-mongo';
import { createCategory } from '@helpers/data-mongo';
import { uniqueName } from '@helpers/random';

test.describe('Mongo Phase 6 — Empty-selection guards', () => {
  test('Go with no selection and Delete action triggers JS guard or no-op', async ({
    page,
    listFor,
  }) => {
    const c = await createCategory(page, { Name: uniqueName('Cat-ES-1') });

    const list = listFor('Category');
    await list.gotoLatest();

    page.once('dialog', (dialog) => dialog.dismiss());

    await list.actionDropdown.selectOption('delete_selected');
    await list.actionGoButton.click();

    // Either JS prevents submission, or the server bounces back — in both cases
    // we must still be on the Category list.
    await expect(page).toHaveURL(/\/admin\/Category\/(\?|$)/);
    expect(page.url()).not.toMatch(/\/action\/delete\//);

    // Cleanup.
    await page.goto(`/admin/Category/${c.id}/change/`);
    await page.locator('a.deletelink').click();
    await page.locator('button.delete-btn').click();
  });

  test('No action selected → Go does nothing', async ({ page, listFor }) => {
    const c = await createCategory(page, { Name: uniqueName('Cat-ES-2') });

    const list = listFor('Category');
    await list.gotoLatest();

    await list.rowCheckboxes.first().check();
    await list.actionGoButton.click();

    await expect(page).toHaveURL(/\/admin\/Category\/(\?|$)/);

    // Cleanup.
    await page.goto(`/admin/Category/${c.id}/change/`);
    await page.locator('a.deletelink').click();
    await page.locator('button.delete-btn').click();
  });
});
