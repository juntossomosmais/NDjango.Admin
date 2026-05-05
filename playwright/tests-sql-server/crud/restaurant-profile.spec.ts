import { test, expect } from '@fixtures/admin';
import { createRestaurant } from '@helpers/data';
import { uniqueName } from '@helpers/random';

test.describe('Phase 3 — RestaurantProfile (1:1 FK to Restaurant)', () => {
  test('add form renders Restaurant FK as raw_id text input + lookup popup', async ({
    formFor,
  }) => {
    const form = formFor('RestaurantProfile');
    await form.gotoAdd();
    await form.expectFkInput('RestaurantId', 'Restaurant');
  });

  test('create, edit pre-fills FK id, delete', async ({
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
        Capacity: 50,
        OpeningHours: '09:00-23:00',
        Website: 'https://example.com',
        RestaurantId: restaurant.id,
      },
      'save'
    );

    await expect(page).toHaveURL(/\/admin\/RestaurantProfile\/(\?|$)/);
    await list.gotoLatest();
    await expect(list.rowByText(String(restaurant.id))).toBeVisible();

    await list.clickRowLink(String(restaurant.id));
    await expect(form.fkInput('RestaurantId')).toHaveValue(String(restaurant.id));
    await expect(form.input('Capacity')).toHaveValue('50');

    await form.deleteLink.click();
    await deleteConfirmation.confirm();
    await expect(page).toHaveURL(/\/admin\/RestaurantProfile\/(\?|$)/);
  });
});
