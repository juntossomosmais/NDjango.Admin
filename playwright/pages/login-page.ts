import { expect, type Locator, type Page } from '@playwright/test';
import { adminUrls } from '@helpers/admin-urls';

export class LoginPage {
  readonly page: Page;
  readonly usernameInput: Locator;
  readonly passwordInput: Locator;
  readonly submitButton: Locator;
  readonly errorMessage: Locator;

  constructor(page: Page) {
    this.page = page;
    this.usernameInput = page.locator('input[name="username"]');
    this.passwordInput = page.locator('input[name="password"]');
    this.submitButton = page.getByRole('button', { name: /log in/i });
    this.errorMessage = page.getByText(/invalid credentials/i);
  }

  async goto(next?: string): Promise<void> {
    await this.page.goto(next ? adminUrls.loginNext(next) : adminUrls.login());
  }

  async login(username: string, password: string): Promise<void> {
    await this.usernameInput.fill(username);
    await this.passwordInput.fill(password);
    await this.submitButton.click();
  }

  async expectVisible(): Promise<void> {
    await expect(this.usernameInput).toBeVisible();
    await expect(this.passwordInput).toBeVisible();
    await expect(this.submitButton).toBeVisible();
  }
}
