import { test, expect } from '@fixtures/admin';
import { uniqueName } from '@helpers/random';

test.describe('Phase 3 — Ingredient CRUD (boolean field handling)', () => {
  test('create with IsAllergen=true persists as True in list and edit form', async ({
    listFor,
    formFor,
  }) => {
    const name = uniqueName('Ing');
    const list = listFor('Ingredient');
    const form = formFor('Ingredient');

    await form.gotoAdd();
    await form.submit({ Name: name, IsAllergen: true }, 'save');

    await list.gotoLatest();
    const row = list.rowByText(name);
    await expect(row).toContainText('True');

    await list.clickRowLink(name);
    await expect(form.checkbox('IsAllergen')).toBeChecked();
  });

  test('create with IsAllergen unchecked defaults to False (browser does not submit unchecked checkboxes)', async ({
    listFor,
    formFor,
  }) => {
    const name = uniqueName('Ing');
    const list = listFor('Ingredient');
    const form = formFor('Ingredient');

    await form.gotoAdd();
    await form.submit({ Name: name, IsAllergen: false }, 'save');

    await list.gotoLatest();
    const row = list.rowByText(name);
    await expect(row).toContainText('False');

    await list.clickRowLink(name);
    await expect(form.checkbox('IsAllergen')).not.toBeChecked();
  });

  test('toggle IsAllergen on edit and persist', async ({ listFor, formFor }) => {
    const name = uniqueName('Ing');
    const list = listFor('Ingredient');
    const form = formFor('Ingredient');

    await form.gotoAdd();
    await form.submit({ Name: name, IsAllergen: false }, 'save');

    await list.gotoLatest();
    await list.clickRowLink(name);
    await form.checkbox('IsAllergen').check();
    await form.clickSave('save');

    await list.gotoLatest();
    await expect(list.rowByText(name)).toContainText('True');
  });
});
