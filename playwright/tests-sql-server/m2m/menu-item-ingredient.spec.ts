import { test, expect } from '@fixtures/admin';
import {
  createIngredient,
  createMenuItem,
  createMenuItemIngredient,
  createRestaurant,
} from '@helpers/data';
import { uniqueName } from '@helpers/random';
import { adminUrls } from '@helpers/admin-urls';

test.describe('Phase 3b — Junction entity in dashboard', () => {
  test('MenuItemIngredient appears on dashboard home', async ({ page, dashboard }) => {
    await dashboard.goto();
    await expect(
      dashboard.appModules
        .locator('tbody tr')
        .filter({ hasText: /Menu item ingredients/i })
    ).toBeVisible();
  });

  test('MenuItemIngredient appears in the sidebar', async ({ page, dashboard }) => {
    await page.goto('/admin/Category/');
    await expect(
      page.locator('#sidebar ul.sidebar-models a').filter({ hasText: /Menu item ingredients/i })
    ).toBeVisible();
  });
});

test.describe('Phase 3b — Junction add form (composite key create)', () => {
  test('add form renders two FK lookup fields and no Id/CreatedAt/UpdatedAt', async ({
    page,
    formFor,
  }) => {
    const form = formFor('MenuItemIngredient');
    await form.gotoAdd();

    await form.expectFkInput('MenuItemId', 'MenuItem');
    await form.expectFkInput('IngredientId', 'Ingredient');

    await expect(form.input('Id')).toHaveCount(0);
    await expect(form.readonlyValue('CreatedAt')).toHaveCount(0);
    await expect(form.readonlyValue('UpdatedAt')).toHaveCount(0);

    await expect(form.saveButton('save')).toBeVisible();
    await expect(form.saveButton('add_another')).toBeVisible();
    await expect(form.saveButton('continue')).toBeVisible();
  });

  test('Save redirects to list', async ({ page, formFor, listFor }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Rest-MI-S') });
    const ingredient = await createIngredient(page, { Name: uniqueName('Ing-S') });
    const menuItem = await createMenuItem(page, restaurant.id, { Name: uniqueName('Item-S') });

    const form = formFor('MenuItemIngredient');
    await form.gotoAdd();
    await form.submit({ MenuItemId: menuItem.id, IngredientId: ingredient.id }, 'save');

    await expect(page).toHaveURL(/\/admin\/MenuItemIngredient\/(\?|$)/);
    const list = listFor('MenuItemIngredient');
    // Verify the new junction exists by hitting its composite-key change URL
    // (page-1 visibility depends on accumulated data + sort order).
    const resp = await page.goto(`/admin/MenuItemIngredient/${menuItem.id},${ingredient.id}/change/`, {
      waitUntil: 'commit',
    });
    expect(resp?.status()).toBe(200);
  });

  test('Save and continue editing redirects to composite-key URL', async ({ page, formFor }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Rest-MI-C') });
    const ingredient = await createIngredient(page, { Name: uniqueName('Ing-C') });
    const menuItem = await createMenuItem(page, restaurant.id, { Name: uniqueName('Item-C') });

    const form = formFor('MenuItemIngredient');
    await form.gotoAdd();
    await form.submit({ MenuItemId: menuItem.id, IngredientId: ingredient.id }, 'continue');

    await expect(page).toHaveURL(
      new RegExp(`/admin/MenuItemIngredient/${menuItem.id},${ingredient.id}/change/`)
    );
  });

  test('Save and add another redirects to blank add form', async ({ page, formFor }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Rest-MI-AA') });
    const ingredient = await createIngredient(page, { Name: uniqueName('Ing-AA') });
    const menuItem = await createMenuItem(page, restaurant.id, { Name: uniqueName('Item-AA') });

    const form = formFor('MenuItemIngredient');
    await form.gotoAdd();
    await form.submit({ MenuItemId: menuItem.id, IngredientId: ingredient.id }, 'add_another');

    await expect(page).toHaveURL(/\/admin\/MenuItemIngredient\/add\/$/);
    await expect(form.fkInput('MenuItemId')).toHaveValue('');
    await expect(form.fkInput('IngredientId')).toHaveValue('');
  });
});

test.describe('Phase 3b — Junction list view (composite-key links)', () => {
  test('list rows link to composite-key edit URL and checkboxes use composite values', async ({
    page,
    listFor,
  }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Rest-LV') });
    const ingredient = await createIngredient(page, { Name: uniqueName('Ing-LV') });
    const menuItem = await createMenuItem(page, restaurant.id, { Name: uniqueName('Item-LV') });
    await createMenuItemIngredient(page, menuItem.id, ingredient.id);

    const list = listFor('MenuItemIngredient');
    // Junction has no Id column; sort by MenuItemId desc so the freshly-created
    // menu item's junction row surfaces on page 1 regardless of accumulated data.
    await list.gotoLatest({ sort: 'MenuItemId', dir: 'desc' });

    const compositeKey = `${menuItem.id},${ingredient.id}`;
    const row = page.locator(`tbody tr:has(input[name="_selected_ids"][value="${compositeKey}"])`);
    await expect(row).toBeVisible();

    const link = row.getByRole('link').first();
    await expect(link).toHaveAttribute(
      'href',
      new RegExp(`/admin/MenuItemIngredient/${menuItem.id},${ingredient.id}/change/`)
    );
  });
});

test.describe('Phase 3b — Junction edit form (read-only PK fields)', () => {
  test('edit page loads with composite key URL and PK fields are read-only text', async ({
    page,
    formFor,
  }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Rest-EF') });
    const ingredient = await createIngredient(page, { Name: uniqueName('Ing-EF') });
    const menuItem = await createMenuItem(page, restaurant.id, { Name: uniqueName('Item-EF') });
    await createMenuItemIngredient(page, menuItem.id, ingredient.id);

    const form = formFor('MenuItemIngredient');
    await page.goto(adminUrls.change('MenuItemIngredient', `${menuItem.id},${ingredient.id}`));
    await expect(page).toHaveURL(
      new RegExp(`/admin/MenuItemIngredient/${menuItem.id},${ingredient.id}/change/`)
    );

    await expect(form.readonlyValue('MenuItemId')).toContainText(String(menuItem.id));
    await expect(form.readonlyValue('IngredientId')).toContainText(String(ingredient.id));

    await expect(form.fkInput('MenuItemId')).toHaveCount(0);
    await expect(form.fkInput('IngredientId')).toHaveCount(0);

    await expect(form.deleteLink).toBeVisible();
  });
});

test.describe('Phase 3b — Junction delete', () => {
  test('delete a junction record only removes the relationship', async ({
    page,
    formFor,
    listFor,
    deleteConfirmation,
  }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Rest-DEL') });
    const ingredient = await createIngredient(page, { Name: uniqueName('Ing-DEL') });
    const menuItem = await createMenuItem(page, restaurant.id, { Name: uniqueName('Item-DEL') });
    await createMenuItemIngredient(page, menuItem.id, ingredient.id);

    await page.goto(adminUrls.change('MenuItemIngredient', `${menuItem.id},${ingredient.id}`));
    const form = formFor('MenuItemIngredient');
    await form.deleteLink.click();

    await expect(page).toHaveURL(
      new RegExp(`/admin/MenuItemIngredient/${menuItem.id},${ingredient.id}/delete/`)
    );
    await deleteConfirmation.confirm();

    await expect(page).toHaveURL(/\/admin\/MenuItemIngredient\/(\?|$)/);
    // Verify the junction is gone via direct URL (its composite-key change URL
    // returns 404 once the record is deleted).
    const resp = await page.goto(`/admin/MenuItemIngredient/${menuItem.id},${ingredient.id}/change/`, {
      waitUntil: 'commit',
    });
    expect([404, 400]).toContain(resp?.status());

    // Parents still exist — verify by hitting their change URL directly (avoids
    // pagination issues when the list has accumulated many rows from prior runs).
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

test.describe('Phase 3b — Bulk delete junctions', () => {
  test('select multiple junction records and bulk-delete', async ({
    page,
    listFor,
    deleteConfirmation,
  }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Rest-BD') });
    const i1 = await createIngredient(page, { Name: uniqueName('Ing-BD-1') });
    const i2 = await createIngredient(page, { Name: uniqueName('Ing-BD-2') });
    const m = await createMenuItem(page, restaurant.id, { Name: uniqueName('Item-BD') });
    await createMenuItemIngredient(page, m.id, i1.id);
    await createMenuItemIngredient(page, m.id, i2.id);

    const list = listFor('MenuItemIngredient');
    // Junction has no Id column; sort by MenuItemId desc so the freshly-created
    // menu item's two junction rows surface on page 1 regardless of accumulated data.
    await list.gotoLatest({ sort: 'MenuItemId', dir: 'desc' });

    await list.checkRows(`${m.id},${i1.id}`, `${m.id},${i2.id}`);
    await list.runAction('delete_selected');

    await expect(page).toHaveURL(/\/admin\/MenuItemIngredient\/action\/delete\//);
    await expect(deleteConfirmation.summary.first()).toContainText('2');
    await deleteConfirmation.confirm();

    await expect(list.successMessage).toContainText(/successfully deleted 2/i);

    // Verify the junctions are gone via direct URL.
    for (const ingredientId of [i1.id, i2.id]) {
      const resp = await page.goto(
        `/admin/MenuItemIngredient/${m.id},${ingredientId}/change/`,
        { waitUntil: 'commit' }
      );
      expect([404, 400]).toContain(resp?.status());
    }
  });
});

test.describe('Phase 3b — Malformed composite key URLs', () => {
  test('non-numeric composite key returns 400', async ({ page }) => {
    const response = await page.goto(adminUrls.change('MenuItemIngredient', 'INVALID'), {
      waitUntil: 'commit',
    });
    expect(response?.status()).toBe(400);
  });

  test('single-value composite key returns 400', async ({ page }) => {
    const response = await page.goto(adminUrls.change('MenuItemIngredient', '1'), {
      waitUntil: 'commit',
    });
    expect(response?.status()).toBe(400);
  });
});

test.describe('Phase 3b — Cascade delete from parent', () => {
  test('deleting an Ingredient cascades to its MenuItemIngredient rows', async ({
    page,
    formFor,
    deleteConfirmation,
  }) => {
    const restaurant = await createRestaurant(page, { Name: uniqueName('Rest-CD') });
    const ingredient = await createIngredient(page, { Name: uniqueName('TempIng') });
    const menuItem = await createMenuItem(page, restaurant.id, { Name: uniqueName('Item-CD') });
    await createMenuItemIngredient(page, menuItem.id, ingredient.id);

    // Verify the junction exists via direct URL (avoids pagination issues
    // when the list has accumulated many rows from prior runs).
    const junctionUrl = adminUrls.change('MenuItemIngredient', `${menuItem.id},${ingredient.id}`);
    let resp = await page.goto(junctionUrl, { waitUntil: 'commit' });
    expect(resp?.status()).toBe(200);

    // Delete parent ingredient
    await page.goto(adminUrls.change('Ingredient', ingredient.id));
    const ingredientForm = formFor('Ingredient');
    await ingredientForm.deleteLink.click();
    await deleteConfirmation.confirm();

    // Cascade — the junction's composite-key change URL returns 400/404.
    resp = await page.goto(junctionUrl, { waitUntil: 'commit' });
    expect([404, 400]).toContain(resp?.status());
  });
});
