import { test, expect } from '@fixtures/admin-mongo';
import { OBJECT_ID_PATTERN } from '@helpers/object-id';
import { uniqueName } from '@helpers/random';

test.describe('Mongo Phase 7 — MongoAuthUser', () => {
  test('add form exposes ALL non-PK fields (no DB defaults like EF Core)', async ({
    formFor,
  }) => {
    const form = formFor('MongoAuthUser');
    await form.gotoAdd();

    // Username + Password
    await expect(form.input('Username')).toBeVisible();
    await expect(form.input('Password')).toBeVisible();
    // Booleans are editable, not readonly defaults.
    await expect(form.checkbox('IsActive')).toBeVisible();
    await expect(form.checkbox('IsSuperuser')).toBeVisible();
  });

  test('creating a user with IsActive checked persists IsActive=True', async ({
    page,
    listFor,
    formFor,
    deleteConfirmation,
  }) => {
    const username = uniqueName('e2e-user');
    const form = formFor('MongoAuthUser');
    const list = listFor('MongoAuthUser');

    await form.gotoAdd();
    await form.fillField('Username', username);
    await form.fillField('Password', 'pw');
    await form.fillField('IsActive', true);
    await form.clickSave('continue');

    await expect(page).toHaveURL(
      new RegExp(`/admin/MongoAuthUser/${OBJECT_ID_PATTERN.source}/change/`)
    );
    await expect(form.checkbox('IsActive')).toBeChecked();
    await expect(form.checkbox('IsSuperuser')).not.toBeChecked();

    // The Password input is write-only on Mongo — the dashboard renders it
    // empty on edit so a hash is never echoed to the browser. Just confirm
    // the field is still present and that the plaintext was NOT echoed back.
    const stored = await form.input('Password').inputValue();
    expect(stored).not.toBe('pw');

    // Cleanup.
    await form.deleteLink.click();
    await deleteConfirmation.confirm();
  });
});
