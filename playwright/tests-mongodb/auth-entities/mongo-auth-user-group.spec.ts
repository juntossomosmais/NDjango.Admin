import { test, expect } from '@fixtures/admin-mongo';

test.describe('Mongo Phase 7 — MongoAuthUserGroup add form', () => {
  test('UserId and GroupId render as plain ObjectId text inputs', async ({ formFor }) => {
    const form = formFor('MongoAuthUserGroup');
    await form.gotoAdd();

    await form.expectObjectIdInput('UserId');
    await form.expectObjectIdInput('GroupId');
    await expect(form.saveButton('save')).toBeVisible();
  });
});
