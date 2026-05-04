import { test, expect } from '@fixtures/admin';
import { createCategory } from '@helpers/data';
import { uniqueName } from '@helpers/random';

test.describe('Phase 6 — List sorting', () => {
  test('clicking a column header toggles sort direction', async ({ page, listFor }) => {
    // Ensure at least 2 rows
    await createCategory(page, { Name: uniqueName('AAA-Cat') });
    await createCategory(page, { Name: uniqueName('ZZZ-Cat') });

    const list = listFor('Category');
    await list.goto();

    const nameHeader = list.headerCells.filter({ hasText: /^Name/i }).getByRole('link').first();
    await nameHeader.click();
    await expect(page).toHaveURL(/sort=Name/);

    // After first click, direction should be asc; the active header has the ▲ arrow.
    await expect(list.headerCells.filter({ hasText: '▲' })).toHaveCount(1);

    // Click again, direction toggles to desc.
    await list.headerCells
      .filter({ hasText: /^Name/i })
      .getByRole('link')
      .first()
      .click();
    await expect(page).toHaveURL(/dir=desc/);
    await expect(list.headerCells.filter({ hasText: '▼' })).toHaveCount(1);
  });
});
