import { type Page, type APIRequestContext, request } from '@playwright/test';
import { ADMIN_PASSWORD, ADMIN_USERNAME, adminUrls } from '@helpers/admin-urls';
import { uniqueName } from '@helpers/random';

interface AdminCreatedAccount {
  username: string;
  password: string;
  userId: number;
  groupId: number;
}

async function createUserViaForm(page: Page, username: string, password: string): Promise<number> {
  await page.goto(adminUrls.add('AuthUser'));
  await page.locator('input[name="Username"]').fill(username);
  await page.locator('input[name="Password"]').fill(password);
  await page.locator('button[type="submit"][name="_save_action"][value="continue"]').click();
  await page.waitForURL(/\/admin\/AuthUser\/\d+\/change\/$/);
  return parseInt(page.url().match(/AuthUser\/(\d+)\//)![1], 10);
}

async function createGroupViaForm(page: Page, name: string): Promise<number> {
  await page.goto(adminUrls.add('AuthGroup'));
  await page.locator('input[name="Name"]').fill(name);
  await page.locator('button[type="submit"][name="_save_action"][value="continue"]').click();
  await page.waitForURL(/\/admin\/AuthGroup\/\d+\/change\/$/);
  return parseInt(page.url().match(/AuthGroup\/(\d+)\//)![1], 10);
}

async function findPermissionId(page: Page, codename: string): Promise<number> {
  // AuthPermission does not implement IAdminSettings, so ?q= is ignored.
  // Iterate paginated list and match the codename column exactly.
  const exactCodename = new RegExp(`^${codename}$`);
  for (let pageNum = 1; pageNum <= 10; pageNum++) {
    await page.goto(`${adminUrls.list('AuthPermission')}?page=${pageNum}`);
    const row = page
      .locator('table#result_list tbody tr')
      .filter({ has: page.locator('td').filter({ hasText: exactCodename }) })
      .first();
    if ((await row.count()) === 0) {
      break;
    }
    const href = await row.locator('a').first().getAttribute('href');
    const m = href?.match(/AuthPermission\/(\d+)\//);
    if (m) return parseInt(m[1], 10);
  }
  throw new Error(`Permission "${codename}" not found`);
}

async function assignPermissionToGroup(
  page: Page,
  groupId: number,
  permissionId: number
): Promise<void> {
  await page.goto(adminUrls.add('AuthGroupPermission'));
  await page.locator('input[name="GroupId"]').fill(String(groupId));
  await page.locator('input[name="PermissionId"]').fill(String(permissionId));
  await page.locator('button[type="submit"][name="_save_action"][value="save"]').click();
  await page.waitForURL(/\/admin\/AuthGroupPermission\/$/);
}

async function assignUserToGroup(page: Page, userId: number, groupId: number): Promise<void> {
  await page.goto(adminUrls.add('AuthUserGroup'));
  await page.locator('input[name="UserId"]').fill(String(userId));
  await page.locator('input[name="GroupId"]').fill(String(groupId));
  await page.locator('button[type="submit"][name="_save_action"][value="save"]').click();
  await page.waitForURL(/\/admin\/AuthUserGroup\/$/);
}

export async function provisionUserWithPermissions(
  adminPage: Page,
  permissionCodenames: string[]
): Promise<AdminCreatedAccount> {
  const username = uniqueName('e2e-user');
  const password = 'e2e-password';
  const groupName = uniqueName('e2e-group');

  const userId = await createUserViaForm(adminPage, username, password);
  const groupId = await createGroupViaForm(adminPage, groupName);

  for (const codename of permissionCodenames) {
    const permId = await findPermissionId(adminPage, codename);
    await assignPermissionToGroup(adminPage, groupId, permId);
  }

  await assignUserToGroup(adminPage, userId, groupId);

  return { username, password, userId, groupId };
}

export async function loginAs(
  baseURL: string,
  username: string,
  password: string
): Promise<APIRequestContext> {
  const ctx = await request.newContext({ baseURL });
  await ctx.get(adminUrls.login());
  const res = await ctx.post(adminUrls.login(), {
    form: { username, password },
    maxRedirects: 0,
    failOnStatusCode: false,
  });
  if (res.status() !== 302 && res.status() !== 200) {
    throw new Error(`Login as ${username} failed: HTTP ${res.status()}`);
  }
  return ctx;
}

export async function loginAdminContext(baseURL: string): Promise<APIRequestContext> {
  return loginAs(baseURL, ADMIN_USERNAME, ADMIN_PASSWORD);
}
