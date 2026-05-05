import { test, expect } from '@fixtures/admin-mongo';

test.describe('Mongo Phase 6 — Action bar visibility', () => {
  test('Category list shows action dropdown with built-in delete + Go button + counter', async ({
    listFor,
  }) => {
    const list = listFor('Category');
    await list.goto();

    await expect(list.actionDropdown).toBeVisible();

    const options = await list.actionDropdown.locator('option').allTextContents();
    expect(options[0]).toMatch(/^-+$/);
    expect(options.join(' | ')).toMatch(/Delete selected categories/i);

    await expect(list.actionGoButton).toBeVisible();
    await expect(list.actionCounter).toContainText(/0 of \d+ selected/);
    await expect(list.rowCheckboxes.first()).toBeVisible();
    await expect(list.selectAllCheckbox).toBeVisible();
  });

  test('No custom actions exist on the Mongo sample', async ({ listFor }) => {
    const list = listFor('Category');
    await list.goto();
    const options = await list.actionDropdown.locator('option').allTextContents();
    const nonPlaceholder = options.filter((o) => !/^-+$/.test(o));
    expect(nonPlaceholder).toEqual([
      expect.stringMatching(/Delete selected categories/i),
    ]);
  });
});
