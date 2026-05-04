import { test, expect } from '@fixtures/admin';
import { createRestaurant } from '@helpers/data';
import { uniqueName } from '@helpers/random';

test.describe('Phase 7 — Custom action: Mark restaurants as featured', () => {
  test('execute custom action shows green success banner with count', async ({
    page,
    listFor,
  }) => {
    const r1 = await createRestaurant(page, { Name: uniqueName('R-CA1') });

    const list = listFor('Restaurant');
    await list.gotoLatest();

    await list.checkRows(r1.id);
    await list.runAction('mark_featured');

    await expect(page).toHaveURL(/\/admin\/Restaurant\//);
    await expect(list.successMessage).toContainText(/successfully marked 1 restaurant\(s\) as featured/i);
  });

  test('multiple selections show correct count in success message', async ({
    page,
    listFor,
  }) => {
    const r1 = await createRestaurant(page, { Name: uniqueName('R-CA2') });
    const r2 = await createRestaurant(page, { Name: uniqueName('R-CA3') });
    const r3 = await createRestaurant(page, { Name: uniqueName('R-CA4') });

    const list = listFor('Restaurant');
    await list.gotoLatest();

    await list.checkRows(r1.id, r2.id, r3.id);
    await list.runAction('mark_featured');

    await expect(list.successMessage).toContainText(/successfully marked 3 restaurant\(s\) as featured/i);
  });
});
