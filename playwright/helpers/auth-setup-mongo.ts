import { type Page } from '@playwright/test';
import { adminUrls } from '@helpers/admin-urls';
import { extractObjectIdFromUrl, OBJECT_ID_PATTERN } from '@helpers/object-id';
import { uniqueName } from '@helpers/random';

interface MongoCreatedAccount {
  username: string;
  password: string;
  userId: string;
  groupId: string;
}

async function createUserViaForm(
  page: Page,
  username: string,
  password: string
): Promise<string> {
  await page.goto(adminUrls.add('MongoAuthUser'));
  await page.locator('input[name="Username"]').fill(username);
  await page.locator('input[name="Password"]').fill(password);
  // Mongo's MongoAuthUser has no DB defaults — IsActive must be checked manually
  // or the user is created inactive and login is rejected.
  await page.locator('input#id_IsActive').check();
  await page
    .locator('button[type="submit"][name="_save_action"][value="continue"]')
    .click();
  await page.waitForURL(
    new RegExp(`/admin/MongoAuthUser/${OBJECT_ID_PATTERN.source}/change/`)
  );
  return extractObjectIdFromUrl(page.url(), 'MongoAuthUser');
}

async function createGroupViaForm(page: Page, name: string): Promise<string> {
  await page.goto(adminUrls.add('MongoAuthGroup'));
  await page.locator('input[name="Name"]').fill(name);
  await page
    .locator('button[type="submit"][name="_save_action"][value="continue"]')
    .click();
  await page.waitForURL(
    new RegExp(`/admin/MongoAuthGroup/${OBJECT_ID_PATTERN.source}/change/`)
  );
  return extractObjectIdFromUrl(page.url(), 'MongoAuthGroup');
}

async function findPermissionId(page: Page, codename: string): Promise<string> {
  const exactCodename = new RegExp(`^${codename}$`);
  for (let pageNum = 1; pageNum <= 10; pageNum++) {
    await page.goto(`${adminUrls.list('MongoAuthPermission')}?page=${pageNum}`);
    const row = page
      .locator('table#result_list tbody tr')
      .filter({ has: page.locator('td').filter({ hasText: exactCodename }) })
      .first();
    if ((await row.count()) === 0) {
      break;
    }
    const href = await row.locator('a').first().getAttribute('href');
    const match = href?.match(
      new RegExp(`/admin/MongoAuthPermission/(${OBJECT_ID_PATTERN.source})/`)
    );
    if (match) return match[1];
  }
  throw new Error(`MongoAuthPermission "${codename}" not found`);
}

async function assignPermissionToGroup(
  page: Page,
  groupId: string,
  permissionId: string
): Promise<void> {
  await page.goto(adminUrls.add('MongoAuthGroupPermission'));
  await page.locator('input[name="GroupId"]').fill(groupId);
  await page.locator('input[name="PermissionId"]').fill(permissionId);
  await page
    .locator('button[type="submit"][name="_save_action"][value="save"]')
    .click();
  await page.waitForURL(/\/admin\/MongoAuthGroupPermission\/(\?|$)/);
}

async function assignUserToGroup(
  page: Page,
  userId: string,
  groupId: string
): Promise<void> {
  await page.goto(adminUrls.add('MongoAuthUserGroup'));
  await page.locator('input[name="UserId"]').fill(userId);
  await page.locator('input[name="GroupId"]').fill(groupId);
  await page
    .locator('button[type="submit"][name="_save_action"][value="save"]')
    .click();
  await page.waitForURL(/\/admin\/MongoAuthUserGroup\/(\?|$)/);
}

export async function provisionMongoUserWithPermissions(
  adminPage: Page,
  permissionCodenames: string[]
): Promise<MongoCreatedAccount> {
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
