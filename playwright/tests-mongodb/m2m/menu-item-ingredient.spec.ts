import { test, expect } from '@fixtures/admin-mongo';
import {
  createIngredient,
  createMenuItem,
  createMenuItemIngredient,
  createRestaurant,
} from '@helpers/data-mongo';
import { adminUrls } from '@helpers/admin-urls';
import { OBJECT_ID_PATTERN } from '@helpers/object-id';
import { uniqueName } from '@helpers/random';

test.describe('Mongo Phase 3a — Junction collection in dashboard', () => {
  test('MenuItemIngredient appears on the dashboard home', async ({ page, dashboard }) => {
    await dashboard.goto();
    await expect(
      dashboard.appModules
        .locator('tbody tr')
        .filter({ hasText: /Menu item ingredients/i })
    ).toBeVisible();
  });

  test('MenuItemIngredient appears in the sidebar under "Restaurant"', async ({ page }) => {
    await page.goto('/admin/Category/');
    const sidebar = page.locator('#sidebar');
    const restaurantSection = sidebar.locator('h3:text-is("Restaurant") + ul.sidebar-models');
    await expect(
      restaurantSection.locator('a').filter({ hasText: /Menu item ingredients/i })
    ).toBeVisible();
  });
});

test.describe('Mongo Phase 3a — Junction list view', () => {
  test('list shows seeded junction documents with ObjectId hex columns', async ({ listFor }) => {
    const list = listFor('MenuItemIngredient');
    await list.goto();

    await expect(list.rows.first()).toBeVisible();
    // Each row links to /admin/MenuItemIngredient/{objectId}/change/ — single-key URL.
    const firstHref = await list.rows
      .first()
      .getByRole('link')
      .first()
      .getAttribute('href');
    expect(firstHref).toMatch(
      new RegExp(`/admin/MenuItemIngredient/${OBJECT_ID_PATTERN.source}/change/`)
    );
  });
});

test.describe('Mongo Phase 3a — Junction add form', () => {
  test('renders MenuItemId and IngredientId as plain text inputs and hides Id/timestamps', async ({
    formFor,
  }) => {
    const form = formFor('MenuItemIngredient');
    await form.gotoAdd();

    await form.expectObjectIdInput('MenuItemId');
    await form.expectObjectIdInput('IngredientId');

    await expect(form.input('Id')).toHaveCount(0);
    await expect(form.readonlyValue('CreatedAt')).toHaveCount(0);
    await expect(form.readonlyValue('UpdatedAt')).toHaveCount(0);

    await expect(form.saveButton('save')).toBeVisible();
    await expect(form.saveButton('add_another')).toBeVisible();
    await expect(form.saveButton('continue')).toBeVisible();
  });

  test('Save and continue editing redirects to a standard ObjectId edit URL', async ({
    page,
    formFor,
  }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Rest-MIIC') });
    const ingredient = await createIngredient(page, { Name: uniqueName('Ing-MIIC') });
    const menuItem = await createMenuItem(page, restaurant.id, {
      Name: uniqueName('Item-MIIC'),
    });

    const form = formFor('MenuItemIngredient');
    await form.gotoAdd();
    await form.submit(
      { MenuItemId: menuItem.id, IngredientId: ingredient.id },
      'continue'
    );

    await expect(page).toHaveURL(
      new RegExp(`/admin/MenuItemIngredient/${OBJECT_ID_PATTERN.source}/change/`)
    );
    // No comma in URL — single ObjectId PK on Mongo.
    expect(page.url()).not.toMatch(/,/);
    await form.expectObjectIdValue('MenuItemId', menuItem.id);
    await form.expectObjectIdValue('IngredientId', ingredient.id);
  });
});

test.describe('Mongo Phase 3a — Junction edit form', () => {
  test('FK-like fields are editable on edit (unlike EF Core composite-PK case)', async ({
    page,
    formFor,
  }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Rest-MIIE') });
    const ingredient = await createIngredient(page, { Name: uniqueName('Ing-MIIE') });
    const menuItem = await createMenuItem(page, restaurant.id, {
      Name: uniqueName('Item-MIIE'),
    });
    const junction = await createMenuItemIngredient(page, menuItem.id, ingredient.id);

    await page.goto(adminUrls.change('MenuItemIngredient', junction.id));
    const form = formFor('MenuItemIngredient');

    // Both ObjectId fields are editable (not read-only) — single ObjectId PK
    // means MenuItemId and IngredientId are NOT part of the primary key.
    await form.expectObjectIdInput('MenuItemId');
    await form.expectObjectIdInput('IngredientId');

    await expect(form.readonlyValue('Id')).toBeVisible();
    await expect(form.readonlyValue('CreatedAt')).toBeVisible();
    await expect(form.readonlyValue('UpdatedAt')).toBeVisible();
    await expect(form.deleteLink).toBeVisible();
  });

  test('updating a junction document persists the new ObjectId', async ({
    page,
    formFor,
  }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Rest-MIIU') });
    const i1 = await createIngredient(page, { Name: uniqueName('Ing-A') });
    const i2 = await createIngredient(page, { Name: uniqueName('Ing-B') });
    const menuItem = await createMenuItem(page, restaurant.id, {
      Name: uniqueName('Item-MIIU'),
    });
    const junction = await createMenuItemIngredient(page, menuItem.id, i1.id);

    const form = formFor('MenuItemIngredient');
    await page.goto(adminUrls.change('MenuItemIngredient', junction.id));
    await form.fillField('IngredientId', i2.id);
    await form.clickSave('continue');

    await form.expectObjectIdValue('IngredientId', i2.id);
  });
});

test.describe('Mongo Phase 3a — Junction delete', () => {
  test('delete only removes the junction document (parents survive)', async ({
    page,
    formFor,
    deleteConfirmation,
  }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Rest-MIID') });
    const ingredient = await createIngredient(page, { Name: uniqueName('Ing-MIID') });
    const menuItem = await createMenuItem(page, restaurant.id, {
      Name: uniqueName('Item-MIID'),
    });
    const junction = await createMenuItemIngredient(page, menuItem.id, ingredient.id);

    await page.goto(adminUrls.change('MenuItemIngredient', junction.id));
    const form = formFor('MenuItemIngredient');
    await form.deleteLink.click();
    await deleteConfirmation.confirm();
    await expect(page).toHaveURL(/\/admin\/MenuItemIngredient\/(\?|$)/);

    // Parents still reachable.
    const menuItemResp = await page.goto(adminUrls.change('MenuItem', menuItem.id), {
      waitUntil: 'commit',
    });
    expect(menuItemResp?.status()).toBe(200);
    const ingredientResp = await page.goto(adminUrls.change('Ingredient', ingredient.id), {
      waitUntil: 'commit',
    });
    expect(ingredientResp?.status()).toBe(200);
  });
});

test.describe('Mongo Phase 3a — Bulk delete', () => {
  test('select two junction documents and bulk-delete them', async ({
    page,
    listFor,
    deleteConfirmation,
  }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Rest-MIIB') });
    const i1 = await createIngredient(page, { Name: uniqueName('Ing-1') });
    const i2 = await createIngredient(page, { Name: uniqueName('Ing-2') });
    const menuItem = await createMenuItem(page, restaurant.id, {
      Name: uniqueName('Item-MIIB'),
    });
    const j1 = await createMenuItemIngredient(page, menuItem.id, i1.id);
    const j2 = await createMenuItemIngredient(page, menuItem.id, i2.id);

    const list = listFor('MenuItemIngredient');
    // No Id sort param needed — by default rows come back in insertion order;
    // jump to the latest by sorting on Id desc so the freshly-inserted docs
    // are on page 1.
    await list.gotoLatest();

    await list.checkRows(j1.id, j2.id);
    await list.runAction('delete_selected');

    await expect(page).toHaveURL(/\/admin\/MenuItemIngredient\/action\/delete\//);
    await expect(deleteConfirmation.summary.first()).toContainText('2');
    await deleteConfirmation.confirm();

    await expect(list.successMessage).toContainText(/successfully deleted 2/i);
  });
});

test.describe('Mongo Phase 3a — Cascade delete behavior', () => {
  test('deleting a parent Ingredient does NOT cascade to its junction documents', async ({
    page,
    formFor,
    deleteConfirmation,
  }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Rest-MIIC') });
    const ingredient = await createIngredient(page, { Name: uniqueName('TempIng') });
    const menuItem = await createMenuItem(page, restaurant.id, {
      Name: uniqueName('Item-MIIC'),
    });
    const junction = await createMenuItemIngredient(page, menuItem.id, ingredient.id);

    // Delete the parent ingredient.
    await page.goto(adminUrls.change('Ingredient', ingredient.id));
    const ingredientForm = formFor('Ingredient');
    await ingredientForm.deleteLink.click();
    await deleteConfirmation.confirm();

    // Junction document still exists — Mongo has no FK constraints, so no cascade.
    const junctionResp = await page.goto(
      adminUrls.change('MenuItemIngredient', junction.id),
      { waitUntil: 'commit' }
    );
    expect(junctionResp?.status()).toBe(200);

    // Cleanup: remove the orphan.
    const junctionForm = formFor('MenuItemIngredient');
    await junctionForm.deleteLink.click();
    await deleteConfirmation.confirm();
  });
});
