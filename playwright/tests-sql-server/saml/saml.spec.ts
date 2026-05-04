import { test, expect } from '@fixtures/anonymous';

const SAML_ENABLED = process.env.PLAYWRIGHT_SAML_ENABLED === 'true';

test.describe('Phase 9 — SAML SSO', () => {
  test.skip(
    !SAML_ENABLED,
    'Requires sample-project-sso running with EnableSaml=true and AWS IAM Identity Center configuration. Run with PLAYWRIGHT_SAML_ENABLED=true.'
  );

  test('login page shows "Try single sign-on (SSO)" link', async ({ page }) => {
    await page.goto('/admin/login/');
    const ssoLink = page.getByRole('link', { name: /try single sign-on/i });
    await expect(ssoLink).toBeVisible();
    await expect(ssoLink).toHaveAttribute('href', /\/admin\/saml\/init\//);
  });

  test('IdP-initiated login is documented as a manual flow', async () => {
    test.fixme(
      true,
      'IdP-initiated login requires the AWS access portal: https://<directory>.awsapps.com/start. Run manually following Phase 9 of E2E_TESTING.md.'
    );
  });

  test('SP-initiated login is broken on AWS IAM Identity Center', async () => {
    test.fixme(
      true,
      'AWS returns 403 "No access" on its internal assertion endpoint for SP-initiated login. Documented as a known issue in E2E_TESTING.md.'
    );
  });

  test('Group sync requires AuthGroup with name matching AWS group UUID', async () => {
    test.fixme(true, 'Manual verification — see Phase 9 step 33 of E2E_TESTING.md.');
  });

  test('Password login coexists with SAML', async ({ page }) => {
    await page.goto('/admin/login/');
    await page.locator('input[name="username"]').fill('admin');
    await page.locator('input[name="password"]').fill('admin');
    await page.locator('button[type="submit"]').click();
    await expect(page).toHaveURL(/\/admin\/$/);
  });
});
