import { test, expect } from '@fixtures/admin-mongo';
import { uniqueName } from '@helpers/random';
import { OBJECT_ID_PATTERN } from '@helpers/object-id';

test.describe('Mongo Phase 3 — Ingredient (boolean field)', () => {
  test('create with checkbox unchecked persists IsAllergen=false', async ({
    page,
    listFor,
    formFor,
    deleteConfirmation,
  }) => {
    const name = uniqueName('Ing');
    const form = formFor('Ingredient');
    const list = listFor('Ingredient');

    await form.gotoAdd();
    await form.fillField('Name', name);
    await expect(form.checkbox('IsAllergen')).not.toBeChecked();
    await form.clickSave('save');

    await list.gotoLatest();
    const row = list.rowByText(name);
    await expect(row).toBeVisible();
    await expect(row).toContainText(/false/i);

    await list.clickRowLink(name);
    await expect(page).toHaveURL(
      new RegExp(`/admin/Ingredient/${OBJECT_ID_PATTERN.source}/change/`)
    );
    await expect(form.checkbox('IsAllergen')).not.toBeChecked();

    await form.deleteLink.click();
    await deleteConfirmation.confirm();
  });

  test('create with checkbox checked persists IsAllergen=true and toggles back to false', async ({
    page,
    listFor,
    formFor,
    deleteConfirmation,
  }) => {
    const name = uniqueName('Ing');
    const form = formFor('Ingredient');
    const list = listFor('Ingredient');

    await form.gotoAdd();
    await form.submit({ Name: name, IsAllergen: true }, 'save');

    await list.gotoLatest();
    const row = list.rowByText(name);
    await expect(row).toBeVisible();
    await expect(row).toContainText(/true/i);

    await list.clickRowLink(name);
    await expect(form.checkbox('IsAllergen')).toBeChecked();

    // Toggle off
    await form.fillField('IsAllergen', false);
    await form.clickSave('save');

    await list.gotoLatest();
    await expect(list.rowByText(name)).toContainText(/false/i);

    // Cleanup
    await list.clickRowLink(name);
    await form.deleteLink.click();
    await deleteConfirmation.confirm();
  });
});
