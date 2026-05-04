import { test, expect } from '@fixtures/admin-mongo';
import { OBJECT_ID_PATTERN } from '@helpers/object-id';
import { uniqueName } from '@helpers/random';

const GUID = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890';

test.describe('Mongo Phase 3 — Gift (Guid, DateTimeOffset, decimal, long, double, float, short, byte)', () => {
  test('add form renders all numeric and Guid/DateTimeOffset inputs', async ({ formFor }) => {
    const form = formFor('Gift');
    await form.gotoAdd();

    await expect(form.input('Name')).toBeVisible();
    await expect(form.checkbox('IsWrapped')).toBeVisible();
    await expect(form.input('TrackingCode')).toBeVisible();
    await expect(form.input('Price')).toHaveAttribute('type', 'number');
    await expect(form.input('Barcode')).toHaveAttribute('type', 'number');
    await expect(form.input('Weight')).toHaveAttribute('type', 'number');
    await expect(form.input('Rating')).toHaveAttribute('type', 'number');
    await expect(form.input('QuantityInStock')).toHaveAttribute('type', 'number');
    await expect(form.input('MinAge')).toHaveAttribute('type', 'number');
    // ShippedAt (DateTimeOffset) renders as text to preserve the offset.
    await expect(form.input('ShippedAt')).toHaveAttribute('type', 'text');
  });

  test('create, edit and delete a Gift with all field types', async ({
    page,
    formFor,
    deleteConfirmation,
  }) => {
    const name = uniqueName('Gift');
    const form = formFor('Gift');

    await form.gotoAdd();
    await form.submit(
      {
        Name: name,
        IsWrapped: true,
        TrackingCode: GUID,
        Price: 29.99,
        Barcode: 1234567890,
        Weight: 1.5,
        Rating: 4.5,
        QuantityInStock: 100,
        MinAge: 12,
        ShippedAt: '2025-06-15T10:30:00+00:00',
        Description: 'A test gift',
        Notes: 'Handle with care',
      },
      'continue'
    );

    await expect(page).toHaveURL(
      new RegExp(`/admin/Gift/${OBJECT_ID_PATTERN.source}/change/`)
    );

    // Verify fields round-tripped.
    await expect(form.input('Name')).toHaveValue(name);
    await expect(form.checkbox('IsWrapped')).toBeChecked();
    await expect(form.input('TrackingCode')).toHaveValue(GUID);
    await expect(form.input('Price')).toHaveValue('29.99');
    await expect(form.input('Rating')).toHaveValue('4.5');
    await expect(form.input('ShippedAt')).toHaveValue('2025-06-15T10:30:00+00:00');

    // Update numeric fields and re-verify.
    await form.fillField('Price', 39.99);
    await form.fillField('Rating', 3.5);
    await form.clickSave('continue');
    await expect(form.input('Price')).toHaveValue('39.99');
    await expect(form.input('Rating')).toHaveValue('3.5');

    await form.deleteLink.click();
    await deleteConfirmation.confirm();
  });
});
