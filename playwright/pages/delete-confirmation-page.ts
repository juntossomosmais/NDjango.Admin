import { expect, type Locator, type Page } from '@playwright/test';

export class DeleteConfirmationPage {
  readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  get heading(): Locator {
    return this.page.getByRole('heading', { name: /are you sure/i });
  }

  get confirmButton(): Locator {
    return this.page.locator('button.delete-btn');
  }

  get cancelLink(): Locator {
    return this.page.locator('a.cancel-btn');
  }

  get summary(): Locator {
    return this.page.locator('div.delete-summary');
  }

  get hiddenSelectedIds(): Locator {
    return this.page.locator('input[type="hidden"][name="_selected_ids"]');
  }

  async expectVisible(): Promise<void> {
    await expect(this.heading).toBeVisible();
    await expect(this.confirmButton).toBeVisible();
  }

  async confirm(): Promise<void> {
    await this.confirmButton.click();
  }

  async cancel(): Promise<void> {
    await this.cancelLink.click();
  }
}
