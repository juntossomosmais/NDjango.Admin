import { test, expect } from '@fixtures/admin';

const REQUIRES_LARGE_DATASET =
  process.env.PLAYWRIGHT_LARGE_DATASET === 'true' || process.env.PLAYWRIGHT_LARGE_DATASET === '1';

test.describe('Phase 8 — Time-limited pagination COUNT', () => {
  test.skip(
    !REQUIRES_LARGE_DATASET,
    'Requires 5M categories seeded (set PLAYWRIGHT_LARGE_DATASET=true and run sample-project/scripts/seed-millions-of-categories.sql first).'
  );

  test('large Category table returns fallback count and loads quickly', async ({
    page,
    listFor,
  }) => {
    const list = listFor('Category');
    const start = Date.now();
    await list.goto();
    const elapsed = Date.now() - start;

    expect(elapsed).toBeLessThan(5000);
    await expect(list.paginatorText).toContainText('9999999999');
    await expect(list.rows).toHaveCount(25);
  });

  test('sliding window pagination renders ellipsis and last page link', async ({
    listFor,
  }) => {
    const list = listFor('Category');
    await list.goto();

    await expect(list.pagination).toBeVisible();
    await expect(list.pageEllipsis.first()).toBeVisible();
    await expect(list.pagination.locator('a').last()).toBeVisible();
  });

  test('navigating to page 2 renders different rows', async ({ page, listFor }) => {
    const list = listFor('Category');
    await list.goto();
    const firstPageFirstRow = await list.rows.first().textContent();

    await list.goto({ page: '2' });
    const secondPageFirstRow = await list.rows.first().textContent();

    expect(secondPageFirstRow).not.toEqual(firstPageFirstRow);
    await expect(list.paginatorText).toContainText('9999999999');
  });

  test('small table (Restaurant) shows real count, not fallback', async ({ page, listFor }) => {
    const list = listFor('Restaurant');
    await list.goto();
    const text = await list.paginatorText.textContent();
    expect(text).not.toContain('9999999999');
    expect(text).toMatch(/^\d+\s+restaurant/i);
  });
});
