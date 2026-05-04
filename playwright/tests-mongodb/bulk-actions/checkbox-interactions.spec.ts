import { test, expect } from '@fixtures/admin-mongo';

test.describe('Mongo Phase 6 — Checkbox interactions', () => {
  test('select-all toggles all rows on and off', async ({ listFor, page }) => {
    const list = listFor('Category');
    await list.goto();

    const totalRows = await list.rowCheckboxes.count();
    expect(totalRows).toBeGreaterThan(0);

    await list.selectAllCheckbox.check();
    for (let i = 0; i < totalRows; i++) {
      await expect(list.rowCheckboxes.nth(i)).toBeChecked();
    }
    await expect(list.actionCounter).toContainText(`${totalRows} of ${totalRows} selected`);

    await list.selectAllCheckbox.uncheck();
    for (let i = 0; i < totalRows; i++) {
      await expect(list.rowCheckboxes.nth(i)).not.toBeChecked();
    }
    await expect(list.actionCounter).toContainText(`0 of ${totalRows} selected`);
  });

  test('individual row checkboxes update the counter', async ({ listFor }) => {
    const list = listFor('Category');
    await list.goto();
    const totalRows = await list.rowCheckboxes.count();
    test.skip(totalRows < 2, 'Needs at least 2 rows to exercise partial selection.');

    await list.rowCheckboxes.nth(0).check();
    await list.rowCheckboxes.nth(1).check();
    await expect(list.actionCounter).toContainText(`2 of ${totalRows} selected`);
    await expect(list.selectAllCheckbox).not.toBeChecked();
  });

  test('unchecking one row after select-all unchecks the header checkbox', async ({
    listFor,
  }) => {
    const list = listFor('Category');
    await list.goto();

    await list.selectAllCheckbox.check();
    await list.rowCheckboxes.first().uncheck();
    await expect(list.selectAllCheckbox).not.toBeChecked();
  });
});
