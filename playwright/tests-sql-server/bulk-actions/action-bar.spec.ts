import { test, expect } from '@fixtures/admin';
import { createRestaurant } from '@helpers/data';
import { uniqueName } from '@helpers/random';

test.describe('Phase 7 — Action bar visibility', () => {
  test('Restaurant list shows action dropdown with built-in delete + custom action + Go button + counter', async ({
    page,
    listFor,
  }) => {
    await createRestaurant(page, { Name: uniqueName('Rest-AB') });
    const list = listFor('Restaurant');
    await list.goto();

    await expect(list.actionDropdown).toBeVisible();

    const options = await list.actionDropdown.locator('option').allTextContents();
    expect(options[0]).toMatch(/^-+$/);
    expect(options.join(' | ')).toMatch(/Delete selected restaurants/i);
    expect(options.join(' | ')).toMatch(/Mark selected restaurants as featured/i);

    await expect(list.actionGoButton).toBeVisible();
    await expect(list.actionCounter).toContainText(/0 of \d+ selected/);

    await expect(list.rowCheckboxes.first()).toBeVisible();
    await expect(list.selectAllCheckbox).toBeVisible();
  });

  test('Category list contains only built-in delete (no custom actions)', async ({
    listFor,
  }) => {
    const list = listFor('Category');
    await list.goto();

    const options = await list.actionDropdown.locator('option').allTextContents();
    const nonPlaceholder = options.filter((o) => !/^-+$/.test(o));
    expect(nonPlaceholder).toHaveLength(1);
    expect(nonPlaceholder[0]).toMatch(/Delete selected categories/i);
  });

  test('Action bar is hidden in popup mode', async ({ popupFor }) => {
    const popup = popupFor('Restaurant');
    await popup.goto();
    await expect(popup.page.locator('select[name="action"]')).toHaveCount(0);
    await expect(popup.page.locator('button.action-btn')).toHaveCount(0);
    await expect(popup.page.locator('input[name="_selected_ids"]')).toHaveCount(0);
    await expect(popup.page.locator('#action-toggle')).toHaveCount(0);
  });
});
