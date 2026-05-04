import { test, expect } from '@fixtures/admin-mongo';
import { uniqueName } from '@helpers/random';
import { OBJECT_ID_PATTERN } from '@helpers/object-id';

test.describe('Mongo Phase 3 — Restaurant CRUD', () => {
  test('create, list, edit and delete a restaurant', async ({
    page,
    listFor,
    formFor,
    deleteConfirmation,
  }) => {
    const name = uniqueName('Rest');
    const list = listFor('Restaurant');
    const form = formFor('Restaurant');

    await form.gotoAdd();
    await form.submit(
      { Name: name, Address: '123 Test St', Phone: '555-0100' },
      'save'
    );
    await expect(page).toHaveURL(/\/admin\/Restaurant\/(\?|$)/);

    // Restaurant has SearchFields(Name) so ?q= is honored.
    await list.goto({ q: name });
    await expect(list.rowByText(name)).toBeVisible();

    await list.clickRowLink(name);
    await expect(page).toHaveURL(
      new RegExp(`/admin/Restaurant/${OBJECT_ID_PATTERN.source}/change/`)
    );

    await form.fillField('Address', '456 New Ave');
    await form.clickSave('save');

    await list.goto({ q: name });
    await list.clickRowLink(name);
    await expect(form.input('Address')).toHaveValue('456 New Ave');

    await form.deleteLink.click();
    await deleteConfirmation.confirm();
    await list.goto({ q: name });
    await expect(list.rowByText(name)).toHaveCount(0);
  });
});
