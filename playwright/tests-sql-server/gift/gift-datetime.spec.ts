import { test, expect } from '@fixtures/admin';
import { uniqueName } from '@helpers/random';

const GIFT_DEFAULTS = {
  Barcode: '1234567890',
  Price: '29.99',
  Weight: '1.5',
  Rating: '4.5',
  QuantityInStock: '100',
  MinAge: '12',
  TrackingCode: '00000000-0000-0000-0000-000000000000',
  Description: 'A nice gift',
  Notes: 'Some notes',
};

async function fillGift(form: any, fields: Record<string, string | number | boolean>) {
  await form.fill({
    Name: fields.Name,
    IsWrapped: false,
    Barcode: GIFT_DEFAULTS.Barcode,
    Price: GIFT_DEFAULTS.Price,
    Weight: GIFT_DEFAULTS.Weight,
    Rating: GIFT_DEFAULTS.Rating,
    QuantityInStock: GIFT_DEFAULTS.QuantityInStock,
    MinAge: GIFT_DEFAULTS.MinAge,
    TrackingCode: GIFT_DEFAULTS.TrackingCode,
    Description: GIFT_DEFAULTS.Description,
    Notes: GIFT_DEFAULTS.Notes,
    ExpirationDate: fields.ExpirationDate,
    ShippedAt: fields.ShippedAt,
    AvailableFrom: fields.AvailableFrom,
    PreparationTime: fields.PreparationTime,
  });
}

test.describe('Phase 9a — Gift date/time ISO formatting', () => {
  test('add form renders correct input types for each date/time field', async ({
    page,
    formFor,
  }) => {
    const form = formFor('Gift');
    await form.gotoAdd();

    await expect(form.input('ExpirationDate')).toHaveAttribute('type', 'date');
    await expect(form.input('ShippedAt')).toHaveAttribute('type', 'text');
    await expect(form.input('AvailableFrom')).toHaveAttribute('type', 'text');
    await expect(form.input('PreparationTime')).toHaveAttribute('type', 'text');
  });

  test('create with all date/time types and edit shows ISO values', async ({
    page,
    formFor,
  }) => {
    const form = formFor('Gift');
    const name = uniqueName('Gift');
    const initial = {
      Name: name,
      ExpirationDate: '2029-12-25',
      ShippedAt: '2028-06-15T10:30:00+05:30',
      AvailableFrom: '14:30:00',
      PreparationTime: '02:15:00',
    };

    await form.gotoAdd();
    await fillGift(form, initial);
    await form.clickSave('continue');
    await expect(page).toHaveURL(/\/admin\/Gift\/\d+\/change\/$/);

    await expect(form.input('ExpirationDate')).toHaveValue(initial.ExpirationDate);
    await expect(form.input('ShippedAt')).toHaveValue(initial.ShippedAt);
    await expect(form.input('AvailableFrom')).toHaveValue(initial.AvailableFrom);
    await expect(form.input('PreparationTime')).toHaveValue(initial.PreparationTime);
  });

  test('round-trip: update fields with different timezone offset', async ({
    page,
    formFor,
  }) => {
    const form = formFor('Gift');
    const name = uniqueName('Gift');

    await form.gotoAdd();
    await fillGift(form, {
      Name: name,
      ExpirationDate: '2029-12-25',
      ShippedAt: '2028-06-15T10:30:00+05:30',
      AvailableFrom: '14:30:00',
      PreparationTime: '02:15:00',
    });
    await form.clickSave('continue');
    await expect(page).toHaveURL(/\/admin\/Gift\/\d+\/change\/$/);

    const updated = {
      ExpirationDate: '2031-01-15',
      ShippedAt: '2030-11-20T18:45:00-03:00',
      AvailableFrom: '09:00:00',
      PreparationTime: '04:30:00',
    };
    await form.fillField('ExpirationDate', updated.ExpirationDate);
    await form.fillField('ShippedAt', updated.ShippedAt);
    await form.fillField('AvailableFrom', updated.AvailableFrom);
    await form.fillField('PreparationTime', updated.PreparationTime);
    await form.clickSave('continue');

    await expect(form.input('ExpirationDate')).toHaveValue(updated.ExpirationDate);
    await expect(form.input('ShippedAt')).toHaveValue(updated.ShippedAt);
    await expect(form.input('AvailableFrom')).toHaveValue(updated.AvailableFrom);
    await expect(form.input('PreparationTime')).toHaveValue(updated.PreparationTime);
  });

  test('delete a gift', async ({ page, listFor, formFor, deleteConfirmation }) => {
    const form = formFor('Gift');
    const list = listFor('Gift');
    const name = uniqueName('Gift');

    await form.gotoAdd();
    await fillGift(form, {
      Name: name,
      ExpirationDate: '2029-12-25',
      ShippedAt: '2028-06-15T10:30:00+05:30',
      AvailableFrom: '14:30:00',
      PreparationTime: '02:15:00',
    });
    await form.clickSave('save');

    await list.gotoLatest();
    await expect(list.rowByText(name)).toBeVisible();

    await list.clickRowLink(name);
    await form.deleteLink.click();
    await deleteConfirmation.confirm();

    await expect(page).toHaveURL(/\/admin\/Gift\/(\?|$)/);
    await list.gotoLatest();
    await expect(list.rowByText(name)).toHaveCount(0);
  });
});
