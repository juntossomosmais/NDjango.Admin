import { test, expect } from '@fixtures/admin-mongo';
import { createRestaurant } from '@helpers/data-mongo';
import { OBJECT_ID_PATTERN } from '@helpers/object-id';
import { uniqueName } from '@helpers/random';

test.describe('Mongo Phase 3 — MenuItem (ObjectId reference + decimal + boolean)', () => {
  test('add form renders RestaurantId as plain text, Price as number, IsAvailable as checkbox', async ({
    formFor,
  }) => {
    const form = formFor('MenuItem');
    await form.gotoAdd();

    await form.expectObjectIdInput('RestaurantId');
    await expect(form.input('Price')).toHaveAttribute('type', 'number');
    await expect(form.checkbox('IsAvailable')).toBeVisible();

    // No legacy IngredientIds field — M2M is via the junction collection.
    await expect(form.input('IngredientIds')).toHaveCount(0);
  });

  test('create with all fields, edit and delete', async ({
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
      'continue'
    );

    await expect(page).toHaveURL(
      new RegExp(`/admin/MenuItem/${OBJECT_ID_PATTERN.source}/change/`)
    );
    await form.expectObjectIdValue('RestaurantId', restaurant.id);
    await expect(form.input('Name')).toHaveValue(itemName);
    await expect(form.input('Price')).toHaveValue('14.99');
    await expect(form.checkbox('IsAvailable')).toBeChecked();

    // Update Price and toggle IsAvailable off.
    await form.fillField('Price', 19.99);
    await form.fillField('IsAvailable', false);
    await form.clickSave('continue');
    await expect(form.input('Price')).toHaveValue('19.99');
    await expect(form.checkbox('IsAvailable')).not.toBeChecked();

    await form.deleteLink.click();
    await deleteConfirmation.confirm();
    await list.goto();
  });

  test('list view does NOT render a search box (no IAdminSettings)', async ({ listFor }) => {
    const list = listFor('MenuItem');
    await list.goto();
    await expect(list.searchBox).toHaveCount(0);
  });
});
