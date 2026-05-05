import { test, expect } from '@fixtures/admin';
import { createCategory } from '@helpers/data';
import { uniqueName } from '@helpers/random';

const SEARCHABLE_ENTITIES = ['Category', 'Restaurant'];
const NON_SEARCHABLE_ENTITIES = ['RestaurantProfile', 'Ingredient', 'MenuItem'];

test.describe('Phase 3a — Conditional search visibility', () => {
  for (const entity of SEARCHABLE_ENTITIES) {
    test(`${entity} list shows the search box`, async ({ listFor }) => {
      const list = listFor(entity);
      await list.goto();
      await expect(list.searchBox).toBeVisible();
      await expect(list.searchInput).toBeVisible();
      await expect(list.searchSubmitButton).toBeVisible();
    });
  }

  for (const entity of NON_SEARCHABLE_ENTITIES) {
    test(`${entity} list hides the search box`, async ({ listFor }) => {
      const list = listFor(entity);
      await list.goto();
      await expect(list.searchBox).toHaveCount(0);
      await expect(list.addLink).toBeVisible();
    });
  }
});

test.describe('Phase 3a — Conditional search filtering', () => {
  test('Category search filters by name (matches Italian, hides Japanese)', async ({
    page,
    listFor,
  }) => {
    const list = listFor('Category');
    const italian = await createCategory(page, { Name: uniqueName('Italian'), Description: 'Pasta' });
    const japanese = await createCategory(page, { Name: uniqueName('Japanese'), Description: 'Sushi' });

    await list.goto({ q: italian.name });
    await expect(list.rowByText(italian.name)).toBeVisible();
    await expect(list.rowByText(japanese.name)).toHaveCount(0);
    await expect(list.paginatorText).toContainText(/^1\s+category/i);
  });

  test('non-searchable entity ignores ?q= parameter', async ({ page, listFor }) => {
    const list = listFor('RestaurantProfile');
    await list.goto();
    const totalBefore = await list.rows.count();

    await list.goto({ q: 'something-that-does-not-exist' });
    await expect(list.rows).toHaveCount(totalBefore);
  });
});
