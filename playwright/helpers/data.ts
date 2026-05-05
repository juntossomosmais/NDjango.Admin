import { type Page, expect } from '@playwright/test';
import { FormPage } from '@pages/form-page';
import { ListPage } from '@pages/list-page';
import { adminUrls } from '@helpers/admin-urls';
import { uniqueName } from '@helpers/random';

export interface CreatedRecord {
  id: number;
}

async function readNewIdFromUrl(page: Page, entity: string): Promise<number> {
  const match = page.url().match(new RegExp(`/admin/${entity}/(\\d+)/change/`));
  if (!match) {
    throw new Error(`Failed to extract id from URL after creating ${entity}: ${page.url()}`);
  }
  return parseInt(match[1], 10);
}

async function createAndContinue(
  page: Page,
  entity: string,
  fields: Record<string, string | number | boolean>
): Promise<CreatedRecord> {
  const form = new FormPage(page, entity);
  await form.gotoAdd();
  await form.submit(fields, 'continue');
  await page.waitForURL(new RegExp(`/admin/${entity}/\\d+/change/`));
  return { id: await readNewIdFromUrl(page, entity) };
}

export async function createCategory(
  page: Page,
  overrides: Partial<{ Name: string; Description: string }> = {}
): Promise<CreatedRecord & { name: string }> {
  const name = overrides.Name ?? uniqueName('Cat');
  const description = overrides.Description ?? 'desc';
  const record = await createAndContinue(page, 'Category', { Name: name, Description: description });
  return { ...record, name };
}

export async function createRestaurant(
  page: Page,
  overrides: Partial<{ Name: string; Address: string; Phone: string }> = {}
): Promise<CreatedRecord & { name: string }> {
  const name = overrides.Name ?? uniqueName('Rest');
  const address = overrides.Address ?? '123 Main St';
  const phone = overrides.Phone ?? '+1-555-0100';
  const record = await createAndContinue(page, 'Restaurant', { Name: name, Address: address, Phone: phone });
  return { ...record, name };
}

export async function createIngredient(
  page: Page,
  overrides: Partial<{ Name: string; IsAllergen: boolean }> = {}
): Promise<CreatedRecord & { name: string }> {
  const name = overrides.Name ?? uniqueName('Ing');
  const record = await createAndContinue(page, 'Ingredient', {
    Name: name,
    IsAllergen: overrides.IsAllergen ?? false,
  });
  return { ...record, name };
}

export async function createMenuItem(
  page: Page,
  restaurantId: number,
  overrides: Partial<{ Name: string; Description: string; Price: number; IsAvailable: boolean }> = {}
): Promise<CreatedRecord & { name: string }> {
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
  menuItemId: number,
  ingredientId: number
): Promise<{ menuItemId: number; ingredientId: number }> {
  const form = new FormPage(page, 'MenuItemIngredient');
  await form.gotoAdd();
  await form.fill({ MenuItemId: menuItemId, IngredientId: ingredientId });
  await form.clickSave('continue');
  await page.waitForURL(new RegExp(`/admin/MenuItemIngredient/${menuItemId},${ingredientId}/change/`));
  return { menuItemId, ingredientId };
}

export async function deleteRecordFromList(page: Page, entity: string, displayName: string): Promise<void> {
  const list = new ListPage(page, entity);
  await list.goto();
  await list.clickRowLink(displayName);
  const form = new FormPage(page, entity);
  await form.deleteLink.click();
  await page.locator('button.delete-btn').click();
  await page.waitForURL(new RegExp(`/admin/${entity}/`));
}

export async function expectListContains(page: Page, entity: string, text: string): Promise<void> {
  const list = new ListPage(page, entity);
  if (!page.url().endsWith(adminUrls.list(entity))) {
    await list.goto();
  }
  await expect(list.rowByText(text)).toBeVisible();
}

export async function expectListDoesNotContain(page: Page, entity: string, text: string): Promise<void> {
  const list = new ListPage(page, entity);
  await list.goto();
  await expect(list.rowByText(text)).toHaveCount(0);
}
