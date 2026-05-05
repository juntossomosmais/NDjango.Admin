import { type Page, expect } from '@playwright/test';
import { adminUrls } from '@helpers/admin-urls';
import { extractObjectIdFromUrl, OBJECT_ID_PATTERN } from '@helpers/object-id';
import { uniqueName } from '@helpers/random';
import { FormPage } from '@pages/form-page';
import { ListPage } from '@pages/list-page';

export interface CreatedMongoRecord {
  id: string;
}

async function createAndContinue(
  page: Page,
  entity: string,
  fields: Record<string, string | number | boolean>
): Promise<CreatedMongoRecord> {
  const form = new FormPage(page, entity);
  await form.gotoAdd();
  await form.submit(fields, 'continue');
  await page.waitForURL(new RegExp(`/admin/${entity}/${OBJECT_ID_PATTERN.source}/change/`));
  return { id: extractObjectIdFromUrl(page.url(), entity) };
}

export async function createCategory(
  page: Page,
  overrides: Partial<{ Name: string; Description: string }> = {}
): Promise<CreatedMongoRecord & { name: string }> {
  const name = overrides.Name ?? uniqueName('Cat');
  const description = overrides.Description ?? 'desc';
  const record = await createAndContinue(page, 'Category', {
    Name: name,
    Description: description,
  });
  return { ...record, name };
}

export async function createRestaurant(
  page: Page,
  overrides: Partial<{ Name: string; Address: string; Phone: string }> = {}
): Promise<CreatedMongoRecord & { name: string }> {
  const name = overrides.Name ?? uniqueName('Rest');
  const address = overrides.Address ?? '123 Main St';
  const phone = overrides.Phone ?? '+1-555-0100';
  const record = await createAndContinue(page, 'Restaurant', {
    Name: name,
    Address: address,
    Phone: phone,
  });
  return { ...record, name };
}

export async function createIngredient(
  page: Page,
  overrides: Partial<{ Name: string; IsAllergen: boolean }> = {}
): Promise<CreatedMongoRecord & { name: string }> {
  const name = overrides.Name ?? uniqueName('Ing');
  const record = await createAndContinue(page, 'Ingredient', {
    Name: name,
    IsAllergen: overrides.IsAllergen ?? false,
  });
  return { ...record, name };
}

export async function createMenuItem(
  page: Page,
  restaurantId: string,
  overrides: Partial<{
    Name: string;
    Description: string;
    Price: number;
    IsAvailable: boolean;
  }> = {}
): Promise<CreatedMongoRecord & { name: string }> {
  const name = overrides.Name ?? uniqueName('Item');
  const record = await createAndContinue(page, 'MenuItem', {
    Name: name,
    Description: overrides.Description ?? 'desc',
    Price: overrides.Price ?? 9.99,
    IsAvailable: overrides.IsAvailable ?? true,
    RestaurantId: restaurantId,
  });
  return { ...record, name };
}

export async function createMenuItemIngredient(
  page: Page,
  menuItemId: string,
  ingredientId: string
): Promise<CreatedMongoRecord> {
  return createAndContinue(page, 'MenuItemIngredient', {
    MenuItemId: menuItemId,
    IngredientId: ingredientId,
  });
}

export async function deleteMongoRecord(
  page: Page,
  entity: string,
  id: string
): Promise<void> {
  const form = new FormPage(page, entity);
  await form.gotoEdit(id);
  await form.deleteLink.click();
  await page.locator('button.delete-btn').click();
  await page.waitForURL(new RegExp(`/admin/${entity}/(\\?|$)`));
}

export async function expectListContains(
  page: Page,
  entity: string,
  text: string
): Promise<void> {
  const list = new ListPage(page, entity);
  if (!page.url().includes(adminUrls.list(entity))) {
    await list.goto();
  }
  await expect(list.rowByText(text)).toBeVisible();
}

/**
 * Reads the first ObjectId visible on the entity's list page.
 * Tests use this to grab a seeded reference (e.g., "give me any Restaurant Id").
 */
export async function readFirstObjectId(page: Page, entity: string): Promise<string> {
  const list = new ListPage(page, entity);
  await list.goto();
  const link = list.rows.first().getByRole('link').first();
  const href = await link.getAttribute('href');
  if (!href) {
    throw new Error(`No row link found on ${entity} list`);
  }
  return extractObjectIdFromUrl(href, entity);
}
