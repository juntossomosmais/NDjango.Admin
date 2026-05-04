import { expect, type Locator, type Page } from '@playwright/test';
import { adminUrls } from '@helpers/admin-urls';

export class ListPage {
  readonly page: Page;
  readonly entity: string;

  constructor(page: Page, entity: string) {
    this.page = page;
    this.entity = entity;
  }

  async goto(query?: Record<string, string>): Promise<void> {
    const url = query
      ? `${adminUrls.list(this.entity)}?${new URLSearchParams(query).toString()}`
      : adminUrls.list(this.entity);
    await this.page.goto(url);
  }

  /**
   * Navigates to the list sorted by Id descending so the newest records appear
   * on page 1. Use this when the test must locate a record it just created
   * against a database that may already contain many rows from prior runs.
   */
  async gotoLatest(extraQuery: Record<string, string> = {}): Promise<void> {
    await this.goto({ sort: 'Id', dir: 'desc', ...extraQuery });
  }

  get changeList(): Locator {
    return this.page.locator('#changelist');
  }

  get toolbar(): Locator {
    return this.page.locator('#toolbar');
  }

  get addLink(): Locator {
    return this.toolbar.locator('a.addlink');
  }

  get searchBox(): Locator {
    return this.page.locator('.search-box');
  }

  get searchInput(): Locator {
    return this.searchBox.locator('input[name="q"]');
  }

  get searchSubmitButton(): Locator {
    return this.searchBox.getByRole('button', { name: /^search$/i });
  }

  get table(): Locator {
    return this.page.locator('table#result_list');
  }

  get rows(): Locator {
    return this.table.locator('tbody tr');
  }

  get headerCells(): Locator {
    return this.table.locator('thead th');
  }

  get paginatorText(): Locator {
    return this.page.locator('p.paginator');
  }

  get pagination(): Locator {
    return this.page.locator('div.pagination');
  }

  get currentPage(): Locator {
    return this.pagination.locator('span.this-page');
  }

  get pageEllipsis(): Locator {
    return this.pagination.locator('span.page-ellipsis');
  }

  get changelistForm(): Locator {
    return this.page.locator('form#changelist-form');
  }

  get selectAllCheckbox(): Locator {
    return this.page.locator('#action-toggle');
  }

  get rowCheckboxes(): Locator {
    return this.page.locator('tbody input[type="checkbox"][name="_selected_ids"]');
  }

  get actionDropdown(): Locator {
    return this.page.locator('select[name="action"]');
  }

  get actionGoButton(): Locator {
    return this.changelistForm.locator('button.action-btn');
  }

  get actionCounter(): Locator {
    return this.page.locator('.action-counter');
  }

  get successMessage(): Locator {
    return this.page.locator('ul.messagelist li.success');
  }

  get errorMessage(): Locator {
    return this.page.locator('ul.messagelist li.error');
  }

  get sortedHeader(): Locator {
    return this.headerCells.filter({ has: this.page.locator('a') }).filter({ hasText: /[▲▼]/ });
  }

  rowByText(text: string | RegExp): Locator {
    return this.rows.filter({ hasText: text });
  }

  rowCheckbox(value: string | number): Locator {
    return this.page.locator(`tbody input[type="checkbox"][name="_selected_ids"][value="${value}"]`);
  }

  async clickRowLink(text: string): Promise<void> {
    await this.rowByText(text).getByRole('link').first().click();
  }

  async search(term: string): Promise<void> {
    await this.searchInput.fill(term);
    await this.searchSubmitButton.click();
  }

  async expectRowCount(count: number): Promise<void> {
    await expect(this.rows).toHaveCount(count);
  }

  async checkRows(...values: Array<string | number>): Promise<void> {
    for (const v of values) {
      await this.rowCheckbox(v).check();
    }
  }

  async runAction(actionValue: string): Promise<void> {
    await this.actionDropdown.selectOption(actionValue);
    await this.actionGoButton.click();
  }
}
