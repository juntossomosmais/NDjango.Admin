import { test, expect } from '@fixtures/admin-mongo';
import { createRestaurant } from '@helpers/data-mongo';
import { OBJECT_ID_PATTERN } from '@helpers/object-id';
import { uniqueName } from '@helpers/random';

test.describe('Mongo Phase 3 — RestaurantProfile (ObjectId reference)', () => {
  test('RestaurantId renders as a plain text input (no FK lookup popup)', async ({
    formFor,
  }) => {
    const form = formFor('RestaurantProfile');
    await form.gotoAdd();
    await form.expectObjectIdInput('RestaurantId');
  });

  test('create, edit and delete with an existing Restaurant ObjectId', async ({
    page,
    listFor,
    formFor,
    deleteConfirmation,
  }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Rest-RP') });

    const list = listFor('RestaurantProfile');
    const form = formFor('RestaurantProfile');

    await form.gotoAdd();
    await form.submit(
      {
        RestaurantId: restaurant.id,
        Website: 'https://test.example.com',
        OpeningHours: '9-5',
        Capacity: 50,
      },
      'continue'
    );

    await expect(page).toHaveURL(
      new RegExp(`/admin/RestaurantProfile/${OBJECT_ID_PATTERN.source}/change/`)
    );
    await form.expectObjectIdValue('RestaurantId', restaurant.id);
    await expect(form.input('Capacity')).toHaveValue('50');
    await expect(form.input('Website')).toHaveValue('https://test.example.com');

    await form.fillField('Capacity', 75);
    await form.clickSave('continue');
    await expect(form.input('Capacity')).toHaveValue('75');

    await form.deleteLink.click();
    await deleteConfirmation.confirm();
    await expect(page).toHaveURL(/\/admin\/RestaurantProfile\/(\?|$)/);
  });

  test('list view does NOT render a search box (no IAdminSettings)', async ({ listFor }) => {
    const list = listFor('RestaurantProfile');
    await list.goto();
    await expect(list.searchBox).toHaveCount(0);
  });
});
