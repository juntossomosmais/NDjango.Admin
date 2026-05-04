import { expect, type Locator, type Page } from '@playwright/test';
import { adminUrls } from '@helpers/admin-urls';

export type SaveAction = 'save' | 'add_another' | 'continue';

export class FormPage {
  readonly page: Page;
  readonly entity: string;

  constructor(page: Page, entity: string) {
    this.page = page;
    this.entity = entity;
  }

  async gotoAdd(): Promise<void> {
    await this.page.goto(adminUrls.add(this.entity));
  }

  async gotoEdit(id: string | number): Promise<void> {
    await this.page.goto(adminUrls.change(this.entity, id));
  }

  get form(): Locator {
    return this.page.locator('form.entity-form');
  }

  fieldRow(propName: string): Locator {
    return this.form.locator(`div.form-row:has(label[for="id_${propName}"])`);
  }

  input(propName: string): Locator {
    return this.page.locator(`input#id_${propName}, textarea#id_${propName}`).first();
  }

  textarea(propName: string): Locator {
    return this.page.locator(`textarea#id_${propName}`);
  }

  checkbox(propName: string): Locator {
    return this.page.locator(`input#id_${propName}[type="checkbox"]`);
  }

  fkInput(propName: string): Locator {
    return this.page.locator(`input#id_${propName}.vForeignKeyRawIdAdminField`);
  }

  fkLookupLink(propName: string): Locator {
    return this.page.locator(`a#lookup_id_${propName}.related-lookup`);
  }

  readonlyValue(propName: string): Locator {
    return this.fieldRow(propName).locator('span.readonly-value');
  }

  fieldErrors(propName: string): Locator {
    return this.fieldRow(propName).locator('ul.errorlist li');
  }

  saveButton(action: SaveAction = 'save'): Locator {
    return this.form.locator(`button[type="submit"][name="_save_action"][value="${action}"]`);
  }

  get deleteLink(): Locator {
    return this.form.locator('a.deletelink');
  }

  async fillField(name: string, value: string | number | boolean): Promise<void> {
    if (typeof value === 'boolean') {
      const cb = this.checkbox(name);
      if (value) await cb.check();
      else await cb.uncheck();
    } else {
      await this.input(name).fill(String(value));
    }
  }

  async fill(fields: Record<string, string | number | boolean>): Promise<void> {
    for (const [name, value] of Object.entries(fields)) {
      await this.fillField(name, value);
    }
  }

  async clickSave(action: SaveAction = 'save'): Promise<void> {
    await this.saveButton(action).click();
  }

  async submit(fields: Record<string, string | number | boolean>, action: SaveAction = 'save'): Promise<void> {
    await this.fill(fields);
    await this.clickSave(action);
  }

  async expectFkInput(propName: string, popupEntity: string): Promise<void> {
    const fk = this.fkInput(propName);
    await expect(fk).toBeVisible();
    const lookup = this.fkLookupLink(propName);
    await expect(lookup).toBeVisible();
    await expect(lookup).toHaveAttribute(
      'href',
      new RegExp(`/admin/${popupEntity}/\\?_to_field=id&_popup=1`)
    );
  }
}
