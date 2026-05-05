import { expect, type Locator, type Page } from '@playwright/test';
import { adminUrls } from '@helpers/admin-urls';

export class DashboardPage {
  readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  async goto(): Promise<void> {
    await this.page.goto(adminUrls.home());
  }

  get header(): Locator {
    return this.page.locator('#header');
  }

  get userTools(): Locator {
    return this.page.locator('#user-tools');
  }

  get welcomeText(): Locator {
    return this.userTools;
  }

  get logoutLink(): Locator {
    return this.userTools.getByRole('link', { name: /^log\s?out$/i });
  }

  get sidebar(): Locator {
    return this.page.locator('#sidebar');
  }

  get sidebarFilter(): Locator {
    return this.page.locator('#sidebar-search');
  }

  sidebarSection(headingText: string): Locator {
    return this.sidebar.locator(`h3:text-is("${headingText}") + ul.sidebar-models`);
  }

  sidebarLink(modelPlural: string): Locator {
    return this.sidebar.locator(`ul.sidebar-models a:has-text("${modelPlural}")`);
  }

  get appModules(): Locator {
    return this.page.locator('div.app-module');
  }

  appModuleByCaption(caption: string): Locator {
    return this.page.locator('div.app-module').filter({
      has: this.page.locator(`caption:has-text("${caption}")`),
    });
  }

  homeModelLink(modelPlural: string): Locator {
    return this.appModules.getByRole('link', { name: new RegExp(`^${modelPlural}$`, 'i') }).first();
  }

  async expectLoggedIn(username = 'admin'): Promise<void> {
    await expect(this.userTools).toContainText(`Welcome, ${username}`);
    await expect(this.logoutLink).toBeVisible();
  }

  async logout(): Promise<void> {
    await this.logoutLink.click();
  }
}
