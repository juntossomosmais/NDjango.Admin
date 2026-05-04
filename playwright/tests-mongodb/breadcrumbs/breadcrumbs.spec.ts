import { test, expect } from '@fixtures/admin-mongo';
import { adminUrls } from '@helpers/admin-urls';
import { readFirstObjectId } from '@helpers/data-mongo';

test.describe('Mongo Phase 10 — Breadcrumbs and navigation', () => {
  test('list page breadcrumb shows Home > Categories', async ({ page }) => {
    await page.goto(adminUrls.list('Category'));
    const breadcrumbs = page.locator('div.breadcrumbs');
    await expect(breadcrumbs).toBeVisible();
    await expect(breadcrumbs).toContainText(/home/i);
    await expect(breadcrumbs).toContainText(/categor/i);
  });

  test('detail page breadcrumb shows Home > Category > Change', async ({ page }) => {
    const id = await readFirstObjectId(page, 'Category');
    await page.goto(adminUrls.change('Category', id));

    const breadcrumbs = page.locator('div.breadcrumbs');
    await expect(breadcrumbs).toContainText(/home/i);
    await expect(breadcrumbs).toContainText(/category/i);
    await expect(breadcrumbs).toContainText(/change/i);
  });

  test('clicking the Home breadcrumb returns to /admin/', async ({ page }) => {
    await page.goto(adminUrls.list('Category'));
    await page.locator('div.breadcrumbs').getByRole('link', { name: /home/i }).click();
    await expect(page).toHaveURL(new RegExp(`${adminUrls.home()}$`));
  });
});
