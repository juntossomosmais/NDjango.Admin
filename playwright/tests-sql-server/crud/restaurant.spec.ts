import { test, expect } from '@fixtures/admin';
import { uniqueName } from '@helpers/random';

test.describe('Phase 3 — Restaurant CRUD', () => {
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
    await form.submit({ Name: name, Address: '123 Main St', Phone: '+1-555-0100' }, 'save');
    await expect(page).toHaveURL(/\/admin\/Restaurant\/(\?|$)/);

    await list.gotoLatest();
    await expect(list.rowByText(name)).toBeVisible();

    await list.clickRowLink(name);
    await expect(form.input('Address')).toHaveValue('123 Main St');

    await form.fillField('Address', '456 Updated Ave');
    await form.clickSave('save');

    await list.gotoLatest();
    await expect(list.rowByText('456 Updated Ave')).toBeVisible();

    await list.clickRowLink(name);
    await form.deleteLink.click();
    await deleteConfirmation.confirm();

    await list.gotoLatest({ q: name });
    await expect(list.rowByText(name)).toHaveCount(0);
  });
});
