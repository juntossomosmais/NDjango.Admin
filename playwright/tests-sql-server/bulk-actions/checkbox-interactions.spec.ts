import { test, expect } from '@fixtures/admin';
import { createRestaurant } from '@helpers/data';
import { uniqueName } from '@helpers/random';

test.describe('Phase 7 — Checkbox interactions (client-side JS)', () => {
  test('select-all toggle checks/unchecks all rows and updates counter', async ({
    page,
    listFor,
  }) => {
    // Ensure at least 2 rows.
    await createRestaurant(page, { Name: uniqueName('Rest-CB1') });
    await createRestaurant(page, { Name: uniqueName('Rest-CB2') });

    const list = listFor('Restaurant');
    await list.gotoLatest();

    const total = await list.rows.count();
    expect(total).toBeGreaterThan(0);

    await list.selectAllCheckbox.check();
    await expect(list.actionCounter).toContainText(`${total} of ${total} selected`);
    expect(await list.rowCheckboxes.evaluateAll((els) => els.every((e: any) => e.checked))).toBe(true);

    await list.selectAllCheckbox.uncheck();
    await expect(list.actionCounter).toContainText(`0 of ${total} selected`);
    expect(await list.rowCheckboxes.evaluateAll((els) => els.every((e: any) => !e.checked))).toBe(true);
  });

  test('individual checkbox toggle updates the counter', async ({ page, listFor }) => {
    await createRestaurant(page, { Name: uniqueName('Rest-CB3') });
    await createRestaurant(page, { Name: uniqueName('Rest-CB4') });

    const list = listFor('Restaurant');
    await list.gotoLatest();

    await list.rowCheckboxes.first().check();
    await expect(list.actionCounter).toContainText(/1 of \d+ selected/);

    await list.rowCheckboxes.nth(1).check();
    await expect(list.actionCounter).toContainText(/2 of \d+ selected/);
  });

  test('checking all rows individually causes header checkbox to become checked', async ({
    page,
    listFor,
  }) => {
    await createRestaurant(page, { Name: uniqueName('Rest-CB5') });

    const list = listFor('Restaurant');
    await list.gotoLatest();

    const count = await list.rowCheckboxes.count();
    for (let i = 0; i < count; i++) {
      await list.rowCheckboxes.nth(i).check();
    }
    await expect(list.selectAllCheckbox).toBeChecked();
  });
});
