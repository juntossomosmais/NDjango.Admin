import { test, expect } from '@fixtures/admin';
import { uniqueName } from '@helpers/random';

test.describe('Phase 3 — Category CRUD', () => {
  test('create → list → edit → delete', async ({ page, listFor, formFor, deleteConfirmation }) => {
    const name = uniqueName('Cat');
    const description = 'Original description';

    const list = listFor('Category');
    const form = formFor('Category');

    // 1. Create
    await form.gotoAdd();
    await form.submit({ Name: name, Description: description }, 'save');
    await expect(page).toHaveURL(/\/admin\/Category\/(\?|$)/);

    // 2. Verify in list (sort by Id desc so the newest record is on page 1)
    await list.gotoLatest({ q: name });
    await expect(list.rowByText(name)).toBeVisible();

    // 3. Open edit form
    await list.clickRowLink(name);
    await expect(page).toHaveURL(/\/admin\/Category\/\d+\/change\/$/);

    // 4. Auto-generated fields are readonly text
    await expect(form.readonlyValue('Id')).toBeVisible();
    await expect(form.readonlyValue('CreatedAt')).toBeVisible();
    await expect(form.readonlyValue('UpdatedAt')).toBeVisible();
    // and not rendered as inputs
    await expect(form.input('Id')).toHaveCount(0);

    // 5. Pre-filled values
    await expect(form.input('Name')).toHaveValue(name);
    await expect(form.input('Description')).toHaveValue(description);

    // 6. Edit and save
    const updatedName = `${name}-updated`;
    await form.fillField('Name', updatedName);
    await form.clickSave('save');
    await expect(page).toHaveURL(/\/admin\/Category\/(\?|$)/);

    await list.gotoLatest({ q: updatedName });
    await expect(list.rowByText(updatedName)).toBeVisible();

    // 7. Delete via edit form
    await list.clickRowLink(updatedName);
    await form.deleteLink.click();
    await expect(page).toHaveURL(/\/admin\/Category\/\d+\/delete\/$/);
    await deleteConfirmation.expectVisible();
    await deleteConfirmation.confirm();

    // 8. Record gone
    await expect(page).toHaveURL(/\/admin\/Category\/(\?|$)/);
    await list.goto({ q: updatedName });
    await expect(list.rowByText(updatedName)).toHaveCount(0);
  });

  test('save and continue editing returns to the same record', async ({ page, formFor }) => {
    const form = formFor('Category');
    const name = uniqueName('Cat');
    await form.gotoAdd();
    await form.submit({ Name: name, Description: 'x' }, 'continue');
    await expect(page).toHaveURL(/\/admin\/Category\/\d+\/change\/$/);
    await expect(form.input('Name')).toHaveValue(name);
  });

  test('save and add another returns to a blank add form', async ({ page, formFor }) => {
    const form = formFor('Category');
    await form.gotoAdd();
    await form.submit({ Name: uniqueName('Cat'), Description: 'x' }, 'add_another');
    await expect(page).toHaveURL(/\/admin\/Category\/add\/$/);
    await expect(form.input('Name')).toHaveValue('');
  });
});
