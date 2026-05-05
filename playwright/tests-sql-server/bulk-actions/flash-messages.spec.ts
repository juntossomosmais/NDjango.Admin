import { test, expect } from '@fixtures/admin';
import { createRestaurant } from '@helpers/data';
import { uniqueName } from '@helpers/random';

test.describe('Phase 7 — Flash messages', () => {
  test('success banner appears between heading and changelist content and disappears on next nav', async ({
    page,
    listFor,
  }) => {
    const r = await createRestaurant(page, { Name: uniqueName('R-FM') });

    const list = listFor('Restaurant');
    await list.gotoLatest();

    await list.checkRows(r.id);
    await list.runAction('mark_featured');

    await expect(list.successMessage).toBeVisible();
    await expect(list.successMessage).toContainText(/successfully marked/i);

    // Navigate away and back; the message must be gone (one-time query param).
    await page.goto('/admin/');
    await list.gotoLatest();
    await expect(list.successMessage).toHaveCount(0);
  });
});
