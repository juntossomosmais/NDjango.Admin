import { test, expect } from '@fixtures/admin-mongo';
import { OBJECT_ID_PATTERN } from '@helpers/object-id';
import { uniqueName } from '@helpers/random';

test.describe('Mongo Phase 7 — MongoAuthGroup CRUD', () => {
  test('create, edit and delete a group', async ({
    page,
    listFor,
    formFor,
    deleteConfirmation,
  }) => {
    const name = uniqueName('viewers');
    const list = listFor('MongoAuthGroup');
    const form = formFor('MongoAuthGroup');

    await form.gotoAdd();
    await form.submit({ Name: name }, 'continue');
    await expect(page).toHaveURL(
      new RegExp(`/admin/MongoAuthGroup/${OBJECT_ID_PATTERN.source}/change/`)
    );

    const renamed = `${name}-renamed`;
    await form.fillField('Name', renamed);
    await form.clickSave('save');

    await list.gotoLatest();
    await expect(list.rowByText(renamed)).toBeVisible();

    await list.clickRowLink(renamed);
    await form.deleteLink.click();
    await deleteConfirmation.confirm();

    await list.gotoLatest();
    await expect(list.rowByText(renamed)).toHaveCount(0);
  });
});
