import { test as base, expect } from '@playwright/test';
import { DashboardPage } from '@pages/dashboard-page';
import { LoginPage } from '@pages/login-page';
import { ListPage } from '@pages/list-page';
import { MongoFormPage } from '@pages/form-page-mongo';
import { DeleteConfirmationPage } from '@pages/delete-confirmation-page';

type Fixtures = {
  dashboard: DashboardPage;
  loginPage: LoginPage;
  listFor: (entity: string) => ListPage;
  formFor: (entity: string) => MongoFormPage;
  deleteConfirmation: DeleteConfirmationPage;
};

export const test = base.extend<Fixtures>({
  dashboard: async ({ page }, use) => {
    await use(new DashboardPage(page));
  },
  loginPage: async ({ page }, use) => {
    await use(new LoginPage(page));
  },
  listFor: async ({ page }, use) => {
    await use((entity: string) => new ListPage(page, entity));
  },
  formFor: async ({ page }, use) => {
    await use((entity: string) => new MongoFormPage(page, entity));
  },
  deleteConfirmation: async ({ page }, use) => {
    await use(new DeleteConfirmationPage(page));
  },
});

export { expect };
