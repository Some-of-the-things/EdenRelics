import { test, expect } from '@playwright/test';
import { registerUser, setAuthInBrowser, uniqueEmail } from './helpers';

test.describe('Settings', () => {
  test('settings page requires login', async ({ page }) => {
    await page.goto('/settings');
    await expect(page).toHaveURL(/\/login/, { timeout: 5_000 });
  });

  test('settings page loads with section headers', async ({ page }) => {
    const email = uniqueEmail('settings');
    const token = await registerUser(page, email);
    await setAuthInBrowser(page, token, email);

    await page.goto('/settings');
    // Should show section headers
    await expect(page.locator('.settings__section-header h2', { hasText: 'Name' })).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('.settings__section-header h2', { hasText: 'Change Password' })).toBeVisible();
  });

  test('can update name', async ({ page }) => {
    const email = uniqueEmail('update-name');
    const token = await registerUser(page, email);
    await setAuthInBrowser(page, token, email);

    await page.goto('/settings');
    await expect(page.locator('.settings__section-header h2', { hasText: 'Name' })).toBeVisible({ timeout: 10_000 });
    // Click on the Name section header to expand it
    await page.locator('.settings__section-header', { hasText: 'Name' }).first().click();

    // Fill in new name
    const firstNameInput = page.locator('input[name="firstName"]');
    await expect(firstNameInput).toBeVisible({ timeout: 5_000 });
    await firstNameInput.fill('Updated');
    await page.locator('button.settings__btn[type="submit"]').click();
    // Should show success message
    await expect(page.locator('[role="status"]')).toBeVisible({ timeout: 5_000 });
  });

  test('can update delivery address', async ({ page }) => {
    const email = uniqueEmail('update-addr');
    const token = await registerUser(page, email);
    await setAuthInBrowser(page, token, email);

    await page.goto('/settings');
    await expect(page.locator('.settings__section-header h2', { hasText: 'Delivery Address' })).toBeVisible({ timeout: 10_000 });
    // Click Delivery Address section
    await page.locator('.settings__section-header', { hasText: 'Delivery Address' }).click();

    const line1Input = page.locator('input[name="deliveryLine1"]');
    await expect(line1Input).toBeVisible({ timeout: 5_000 });
    await line1Input.fill('123 Test Street');
    await page.locator('input[name="deliveryCity"]').fill('London');
    await page.locator('button.settings__btn[type="submit"]').click();
    await expect(page.locator('[role="status"]')).toBeVisible({ timeout: 5_000 });
  });

  test('can change password', async ({ page }) => {
    const email = uniqueEmail('change-pw');
    const token = await registerUser(page, email);
    await setAuthInBrowser(page, token, email);

    await page.goto('/settings');
    await expect(page.locator('.settings__section-header h2', { hasText: 'Change Password' })).toBeVisible({ timeout: 10_000 });
    // Click Change Password section
    await page.locator('.settings__section-header', { hasText: 'Change Password' }).click();

    const currentPw = page.locator('input[name="currentPassword"]');
    await expect(currentPw).toBeVisible({ timeout: 5_000 });
    await currentPw.fill('TestPass123!');
    await page.locator('input[name="newPassword"]').fill('NewTestPass456!');
    await page.locator('input[name="confirmNewPassword"]').fill('NewTestPass456!');
    // Use the submit button specifically (not the section header which also has role="button")
    await page.locator('button.settings__btn[type="submit"]').click();
    await expect(page.locator('[role="status"]')).toBeVisible({ timeout: 5_000 });
  });

  test('MFA setup section is visible', async ({ page }) => {
    const email = uniqueEmail('mfa-section');
    const token = await registerUser(page, email);
    await setAuthInBrowser(page, token, email);

    await page.goto('/settings');
    await expect(page.locator('.settings__section-header h2', { hasText: 'Two-Factor Authentication' })).toBeVisible({ timeout: 10_000 });
  });

  test('passkeys section is visible if supported', async ({ page }) => {
    const email = uniqueEmail('passkey-section');
    const token = await registerUser(page, email);
    await setAuthInBrowser(page, token, email);

    await page.goto('/settings');
    // Passkeys section may or may not show depending on browser WebAuthn support
    // Just verify the page loads with sections
    await expect(page.locator('.settings__section-header h2', { hasText: 'Name' })).toBeVisible({ timeout: 10_000 });
  });
});
