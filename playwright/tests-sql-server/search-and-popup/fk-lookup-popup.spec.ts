import { test, expect } from '@fixtures/admin';
import { createMenuItem, createRestaurant } from '@helpers/data';
import { uniqueName } from '@helpers/random';

test.describe('Phase 3a — FK lookup popup', () => {
  test('MenuItem add form renders Restaurant as text input + lookup icon (no <select>)', async ({
    page,
    formFor,
  }) => {
    const form = formFor('MenuItem');
    await form.gotoAdd();

    await form.expectFkInput('RestaurantId', 'Restaurant');
    await expect(page.locator('select[name="RestaurantId"]')).toHaveCount(0);
  });

  test('RestaurantProfile add form renders Restaurant as text input + lookup icon', async ({
    formFor,
  }) => {
    const form = formFor('RestaurantProfile');
    await form.gotoAdd();
    await form.expectFkInput('RestaurantId', 'Restaurant');
  });

  test('Popup mode renders simplified layout (popup body, no header, no sidebar)', async ({
    popupFor,
  }) => {
    const popup = popupFor('Restaurant');
    await popup.goto();
    await popup.expectIsPopup();
    await expect(popup.popupSelectLinks.first()).toBeVisible();
  });

  test('Popup respects conditional search (Restaurant has search, Ingredient does not)', async ({
    popupFor,
  }) => {
    const restaurantPopup = popupFor('Restaurant');
    await restaurantPopup.goto();
    await expect(restaurantPopup.searchInput).toBeVisible();

    const ingredientPopup = popupFor('Ingredient');
    await ingredientPopup.goto();
    await expect(ingredientPopup.searchInput).toHaveCount(0);
  });

  test('Popup search preserves _popup and _to_field hidden inputs', async ({ popupFor }) => {
    const popup = popupFor('Restaurant');
    await popup.goto({ _to_field: 'id' });

    await expect(popup.hiddenInput('_popup')).toHaveValue('1');
    await expect(popup.hiddenInput('_to_field')).toHaveValue('id');
  });

  test('Popup search filters results by query string', async ({ page, popupFor }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Bella') });

    const popup = popupFor('Restaurant');
    await popup.goto({ _to_field: 'id', q: restaurant.name });

    // The first column shown is the Id, so popup-select text is the id, not the name.
    // Filter the row (which contains all columns including Name) instead.
    const row = page.locator('table#result_list tbody tr').filter({ hasText: restaurant.name });
    await expect(row).toHaveCount(1);
    await expect(row.locator('a.popup-select')).toHaveAttribute('data-pk', String(restaurant.id));
  });

  test('FK value entered as raw id saves correctly', async ({ page, formFor, listFor }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Rest-FK') });
    const itemName = uniqueName('Item');

    const form = formFor('MenuItem');
    await form.gotoAdd();
    await form.submit(
      {
        Name: itemName,
        Description: 'desc',
        Price: 9.99,
        IsAvailable: true,
        RestaurantId: restaurant.id,
      },
      'save'
    );

    const list = listFor('MenuItem');
    await list.gotoLatest();
    await expect(list.rowByText(itemName)).toContainText(String(restaurant.id));
  });

  test('FK value pre-fills correctly on edit', async ({ page, formFor }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Rest-FK2') });
    const item = await createMenuItem(page, restaurant.id, { Name: uniqueName('Item') });

    const form = formFor('MenuItem');
    await form.gotoEdit(item.id);
    await expect(form.fkInput('RestaurantId')).toHaveValue(String(restaurant.id));
  });

  test('Popup-select links carry data-pk attribute', async ({ page, popupFor }) => {
    await createRestaurant(page, { Name: uniqueName('Rest-DP') });
    const popup = popupFor('Restaurant');
    await popup.goto({ _to_field: 'id' });

    const firstLink = popup.popupSelectLinks.first();
    await expect(firstLink).toBeVisible();
    await expect(firstLink).toHaveAttribute('data-pk', /\d+/);
  });

  test('Lookup link points to popup URL with _to_field=id&_popup=1', async ({ formFor }) => {
    const form = formFor('MenuItem');
    await form.gotoAdd();
    const lookup = form.fkLookupLink('RestaurantId');
    await expect(lookup).toHaveAttribute('href', /\/admin\/Restaurant\/\?_to_field=id&_popup=1/);
  });
});
