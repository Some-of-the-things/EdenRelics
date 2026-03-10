import { test, expect } from '@playwright/test';
import { registerUser, setAuthInBrowser, uniqueEmail } from './helpers';

test.describe('Account', () => {
  test('account page requires login', async ({ page }) => {
    await page.goto('/account');
    // Should redirect to login
    await expect(page).toHaveURL(/\/login/, { timeout: 5_000 });
  });

  test('account page shows user info when logged in', async ({ page }) => {
    const email = uniqueEmail('acct');
    const token = await registerUser(page, email);
    await setAuthInBrowser(page, token, email);

    await page.goto('/account');
    await expect(page.getByText(email)).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText(/Test/)).toBeVisible();
  });

  test('account page shows email verification status', async ({ page }) => {
    const email = uniqueEmail('verify-status');
    const token = await registerUser(page, email);
    await setAuthInBrowser(page, token, email);

    await page.goto('/account');
    // Should show Unverified status (new account)
    await expect(page.locator('.account__unverified, .account__verified').first()).toBeVisible({ timeout: 10_000 });
  });

  test('account page has settings link', async ({ page }) => {
    const email = uniqueEmail('settings-link');
    const token = await registerUser(page, email);
    await setAuthInBrowser(page, token, email);

    await page.goto('/account');
    const settingsLink = page.locator('.account__settings-link');
    await expect(settingsLink).toBeVisible({ timeout: 10_000 });
  });

  test('can sign out from account page', async ({ page }) => {
    const email = uniqueEmail('signout');
    const token = await registerUser(page, email);
    await setAuthInBrowser(page, token, email);

    await page.goto('/account');
    const signOutBtn = page.locator('.account__logout');
    await expect(signOutBtn).toBeVisible({ timeout: 10_000 });
    await signOutBtn.click();
    // Should redirect to home
    await expect(page).toHaveURL(/\/$/, { timeout: 5_000 });
  });
});
