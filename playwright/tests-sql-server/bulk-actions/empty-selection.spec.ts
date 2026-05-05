import { test, expect } from '@fixtures/admin';
import { createRestaurant } from '@helpers/data';
import { uniqueName } from '@helpers/random';

test.describe('Phase 7 — Empty selection guards', () => {
  test('Go button with no selection and Delete action triggers JS guard or no-op', async ({
    page,
    listFor,
  }) => {
    await createRestaurant(page, { Name: uniqueName('R-ES-1') });

    const list = listFor('Restaurant');
    await list.gotoLatest();

    // Set up dialog handler in case the JS uses confirm()
    page.once('dialog', (dialog) => dialog.dismiss());

    await list.actionDropdown.selectOption('delete_selected');
    await list.actionGoButton.click();

    // Either we stay on the list page (JS prevented submit) or are redirected back.
    await expect(page).toHaveURL(/\/admin\/Restaurant\/(\?|$)/);
    await expect(list.rowByText(/R-ES-1/)).toBeVisible();
  });

  test('No action selected → Go does nothing', async ({ page, listFor }) => {
    await createRestaurant(page, { Name: uniqueName('R-ES-2') });

    const list = listFor('Restaurant');
    await list.gotoLatest();

    // Check one row but leave the dropdown on the placeholder.
    await list.rowCheckboxes.first().check();
    await list.actionGoButton.click();

    await expect(page).toHaveURL(/\/admin\/Restaurant\/(\?|$)/);
  });
});
