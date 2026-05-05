import { test as base, expect } from '@playwright/test';
import { DashboardPage } from '@pages/dashboard-page';
import { LoginPage } from '@pages/login-page';
import { ListPage } from '@pages/list-page';
import { FormPage } from '@pages/form-page';
import { DeleteConfirmationPage } from '@pages/delete-confirmation-page';
import { PopupPage } from '@pages/popup-page';

type Fixtures = {
  dashboard: DashboardPage;
  loginPage: LoginPage;
  listFor: (entity: string) => ListPage;
  formFor: (entity: string) => FormPage;
  popupFor: (entity: string) => PopupPage;
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
    await use((entity: string) => new FormPage(page, entity));
  },
  popupFor: async ({ page }, use) => {
    await use((entity: string) => new PopupPage(page, entity));
  },
  deleteConfirmation: async ({ page }, use) => {
    await use(new DeleteConfirmationPage(page));
  },
});

export { expect };
