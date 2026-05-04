import { test, expect } from '@fixtures/admin-mongo';
import { uniqueName } from '@helpers/random';
import { OBJECT_ID_PATTERN } from '@helpers/object-id';

test.describe('Mongo Phase 3 — Category CRUD', () => {
  test('create → list → edit → save and continue → delete', async ({
    page,
    listFor,
    formFor,
    deleteConfirmation,
  }) => {
    const name = uniqueName('Cat');
    const description = 'Original description';

    const list = listFor('Category');
    const form = formFor('Category');

    // Add form: only Name and Description on create — no Id, CreatedAt, UpdatedAt.
    await form.gotoAdd();
    await expect(form.input('Name')).toBeVisible();
    await expect(form.input('Description')).toBeVisible();
    await expect(form.input('Id')).toHaveCount(0);
    await expect(form.readonlyValue('CreatedAt')).toHaveCount(0);
    await expect(form.readonlyValue('UpdatedAt')).toHaveCount(0);
    await expect(form.saveButton('save')).toBeVisible();
    await expect(form.saveButton('add_another')).toBeVisible();
    await expect(form.saveButton('continue')).toBeVisible();

    await form.submit({ Name: name, Description: description }, 'save');
    await expect(page).toHaveURL(/\/admin\/Category\/(\?|$)/);

    // Edit form: ObjectId in URL, Id/CreatedAt/UpdatedAt readonly with valid timestamps.
    await list.goto({ q: name });
    await list.clickRowLink(name);
    await expect(page).toHaveURL(
      new RegExp(`/admin/Category/${OBJECT_ID_PATTERN.source}/change/`)
    );

    await expect(form.readonlyValue('Id')).toBeVisible();
    await expect(form.readonlyValue('Id')).toContainText(OBJECT_ID_PATTERN);
    await expect(form.readonlyValue('CreatedAt')).toBeVisible();
    await expect(form.readonlyValue('UpdatedAt')).toBeVisible();
    await expect(form.readonlyValue('CreatedAt')).not.toContainText('0001-01-01');
    await expect(form.input('Id')).toHaveCount(0);

    await expect(form.input('Name')).toHaveValue(name);
    await expect(form.input('Description')).toHaveValue(description);

    // Edit: change Name and save.
    const updatedName = `${name}-updated`;
    await form.fillField('Name', updatedName);
    await form.clickSave('save');
    await expect(page).toHaveURL(/\/admin\/Category\/(\?|$)/);

    await list.goto({ q: updatedName });
    await expect(list.rowByText(updatedName)).toBeVisible();

    // Save and continue: stays on edit form with the new value.
    await list.clickRowLink(updatedName);
    await form.fillField('Description', 'Updated description');
    await form.clickSave('continue');
    await expect(page).toHaveURL(
      new RegExp(`/admin/Category/${OBJECT_ID_PATTERN.source}/change/`)
    );
    await expect(form.input('Description')).toHaveValue('Updated description');

    // Save and add another: lands on a blank add form.
    await form.clickSave('add_another');
    await expect(page).toHaveURL(/\/admin\/Category\/add\/$/);
    await expect(form.input('Name')).toHaveValue('');

    // Delete via the edit form's Delete link.
    await list.goto({ q: updatedName });
    await list.clickRowLink(updatedName);
    await form.deleteLink.click();
    await expect(page).toHaveURL(
      new RegExp(`/admin/Category/${OBJECT_ID_PATTERN.source}/delete/`)
    );
    await deleteConfirmation.expectVisible();
    await deleteConfirmation.confirm();

    await expect(page).toHaveURL(/\/admin\/Category\/(\?|$)/);
    await list.goto({ q: updatedName });
    await expect(list.rowByText(updatedName)).toHaveCount(0);
  });

  test('cancel delete leaves the record intact', async ({
    page,
    formFor,
    listFor,
    deleteConfirmation,
  }) => {
    const name = uniqueName('CatCancel');
    const form = formFor('Category');
    const list = listFor('Category');

    await form.gotoAdd();
    await form.submit({ Name: name, Description: 'keep me' }, 'continue');

    await form.deleteLink.click();
    await expect(page).toHaveURL(
      new RegExp(`/admin/Category/${OBJECT_ID_PATTERN.source}/delete/`)
    );
    await deleteConfirmation.cancel();

    await list.goto({ q: name });
    await expect(list.rowByText(name)).toBeVisible();

    // Cleanup
    await list.clickRowLink(name);
    await form.deleteLink.click();
    await deleteConfirmation.confirm();
  });
});
