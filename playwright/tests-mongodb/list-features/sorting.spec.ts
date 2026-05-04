import { test, expect } from '@fixtures/admin-mongo';

test.describe('Mongo Phase 5 — Sorting', () => {
  test('?sort=Name&dir=asc orders Categories alphabetically', async ({ listFor, page }) => {
    const list = listFor('Category');
    await list.goto({ sort: 'Name', dir: 'asc' });

    const cells = page.locator('table#result_list tbody tr td:nth-child(2)');
    const names = (await cells.allTextContents()).map((s) => s.trim());

    const seeded = names.filter((n) => ['Italian', 'Japanese', 'Mexican'].includes(n));
    expect(seeded).toEqual(['Italian', 'Japanese', 'Mexican']);
  });

  test('?dir=desc reverses the order', async ({ listFor, page }) => {
    const list = listFor('Category');
    await list.goto({ sort: 'Name', dir: 'desc' });

    const cells = page.locator('table#result_list tbody tr td:nth-child(2)');
    const names = (await cells.allTextContents()).map((s) => s.trim());

    const seeded = names.filter((n) => ['Italian', 'Japanese', 'Mexican'].includes(n));
    expect(seeded).toEqual(['Mexican', 'Japanese', 'Italian']);
  });

  test('column headers are sort links and active sort renders an arrow', async ({
    listFor,
    page,
  }) => {
    const list = listFor('Category');
    await list.goto({ sort: 'Name', dir: 'asc' });
    await expect(list.sortedHeader).toContainText(/[▲▼]/);

    // The headers should expose anchor children (sortable).
    await expect(list.headerCells.locator('a').first()).toBeVisible();
  });
});
