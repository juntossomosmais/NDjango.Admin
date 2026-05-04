import { test, expect } from '@fixtures/admin-mongo';
import { OBJECT_ID_PATTERN } from '@helpers/object-id';

test.describe('Mongo Phase 7 — MongoAuthGroupPermission add form', () => {
  test('GroupId and PermissionId render as plain ObjectId text inputs', async ({
    formFor,
  }) => {
    const form = formFor('MongoAuthGroupPermission');
    await form.gotoAdd();

    await form.expectObjectIdInput('GroupId');
    await form.expectObjectIdInput('PermissionId');
    await expect(form.saveButton('save')).toBeVisible();
  });
});
