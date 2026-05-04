import { expect, type Locator, type Page } from '@playwright/test';
import { adminUrls } from '@helpers/admin-urls';

export class PopupPage {
  readonly page: Page;
  readonly entity: string;

  constructor(page: Page, entity: string) {
    this.page = page;
    this.entity = entity;
  }

  async goto(params: Record<string, string> = {}): Promise<void> {
    await this.page.goto(adminUrls.popup(this.entity, params));
  }

  get body(): Locator {
    return this.page.locator('body');
  }

  get table(): Locator {
    return this.page.locator('table#result_list');
  }

  get popupSelectLinks(): Locator {
    return this.page.locator('a.popup-select');
  }

  get header(): Locator {
    return this.page.locator('#header');
  }

  get sidebar(): Locator {
    return this.page.locator('#sidebar');
  }

  get searchInput(): Locator {
    return this.page.locator('input[name="q"]');
  }

  hiddenInput(name: string): Locator {
    return this.page.locator(`input[type="hidden"][name="${name}"]`);
  }

  async expectIsPopup(): Promise<void> {
    await expect(this.body).toHaveClass(/popup/);
    await expect(this.header).toHaveCount(0);
    await expect(this.sidebar).toHaveCount(0);
  }
}
