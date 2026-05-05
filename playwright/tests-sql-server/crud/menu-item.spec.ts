import { test, expect } from '@fixtures/admin';
import { createRestaurant } from '@helpers/data';
import { uniqueName } from '@helpers/random';

test.describe('Phase 3 — MenuItem (N:1 FK to Restaurant)', () => {
  test('add form renders Restaurant FK as raw_id text input + lookup popup', async ({
    formFor,
  }) => {
    const form = formFor('MenuItem');
    await form.gotoAdd();
    await form.expectFkInput('RestaurantId', 'Restaurant');
  });

  test('Price uses number input with step="any"', async ({ formFor }) => {
    const form = formFor('MenuItem');
    await form.gotoAdd();
    await expect(form.input('Price')).toHaveAttribute('type', 'number');
    await expect(form.input('Price')).toHaveAttribute('step', /any|0\.\d+/);
  });

  test('create, edit and delete a MenuItem with FK', async ({
    page,
    listFor,
    formFor,
    deleteConfirmation,
  }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Rest-MI') });
    const itemName = uniqueName('Item');

    const list = listFor('MenuItem');
    const form = formFor('MenuItem');

    await form.gotoAdd();
    await form.submit(
      {
        Name: itemName,
        Description: 'A delicious dish',
        Price: 14.99,
        IsAvailable: true,
        RestaurantId: restaurant.id,
      },
      'save'
    );

    await expect(page).toHaveURL(/\/admin\/MenuItem\/(\?|$)/);
    await list.gotoLatest();
    const row = list.rowByText(itemName);
    await expect(row).toBeVisible();
    await expect(row).toContainText(String(restaurant.id));

    await list.clickRowLink(itemName);
    await expect(form.input('Name')).toHaveValue(itemName);
    await expect(form.fkInput('RestaurantId')).toHaveValue(String(restaurant.id));
    await expect(form.checkbox('IsAvailable')).toBeChecked();

    await form.deleteLink.click();
    await deleteConfirmation.confirm();
    await list.gotoLatest();
    await expect(list.rowByText(itemName)).toHaveCount(0);
  });
});
