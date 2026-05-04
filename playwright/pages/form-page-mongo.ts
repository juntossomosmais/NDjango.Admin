import { expect, type Locator, type Page } from '@playwright/test';
import { FormPage } from '@pages/form-page';
import { OBJECT_ID_PATTERN_ANCHORED } from '@helpers/object-id';

/**
 * MongoDB variant of FormPage. ObjectId reference fields render as plain text
 * inputs (no `vForeignKeyRawIdAdminField` class, no lookup popup), so the
 * EF-Core-only `expectFkInput` assertion is replaced by a no-popup variant.
 */
export class MongoFormPage extends FormPage {
  constructor(page: Page, entity: string) {
    super(page, entity);
  }

  objectIdInput(propName: string): Locator {
    return this.input(propName);
  }

  /**
   * Asserts an ObjectId reference field renders as a plain text input with no
   * FK-popup chrome. Use for `RestaurantId`, `MenuItemId`, `IngredientId`, etc.
   */
  async expectObjectIdInput(propName: string): Promise<void> {
    const input = this.objectIdInput(propName);
    await expect(input).toBeVisible();
    await expect(input).toHaveAttribute('type', 'text');
    // Must NOT carry the EF-only FK class or lookup affordance.
    await expect(this.fkInput(propName)).toHaveCount(0);
    await expect(this.fkLookupLink(propName)).toHaveCount(0);
  }

  /**
   * Reads the current value of an ObjectId field and asserts it matches the
   * expected hex string.
   */
  async expectObjectIdValue(propName: string, expected: string): Promise<void> {
    expect(expected).toMatch(OBJECT_ID_PATTERN_ANCHORED);
    await expect(this.objectIdInput(propName)).toHaveValue(expected);
  }
}
