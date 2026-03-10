import { test, expect } from '@playwright/test';

test.describe('Email Verification', () => {
  test('verify-email page loads with form', async ({ page }) => {
    await page.goto('/verify-email');
    await expect(page.getByRole('heading', { name: /verify email/i })).toBeVisible();
    await expect(page.locator('input[name="email"]')).toBeVisible();
    await expect(page.locator('input[name="token"]')).toBeVisible();
  });

  test('verify-email auto-fills from query params', async ({ page }) => {
    await page.goto('/verify-email?email=test@example.com&token=abc123');
    await expect(page.locator('input[name="email"]')).toHaveValue('test@example.com');
    await expect(page.locator('input[name="token"]')).toHaveValue('abc123');
  });

  test('verify-email shows error for invalid token', async ({ page }) => {
    await page.goto('/verify-email');
    await page.locator('input[name="email"]').fill('anyone@example.com');
    await page.locator('input[name="token"]').fill('invalid-token');
    await page.getByRole('button', { name: /verify email/i }).click();
    await expect(page.locator('[role="alert"]')).toBeVisible({ timeout: 5_000 });
  });
});

test.describe('Reset Password', () => {
  test('reset-password page loads', async ({ page }) => {
    await page.goto('/reset-password');
    await expect(page.getByRole('heading', { name: /reset password/i })).toBeVisible();
  });

  test('reset-password auto-fills from query params', async ({ page }) => {
    await page.goto('/reset-password?email=test@example.com&token=abc123');
    await expect(page.locator('input[name="email"]')).toHaveValue('test@example.com');
    await expect(page.locator('input[name="token"]')).toHaveValue('abc123');
  });

  test('reset-password validates matching passwords', async ({ page }) => {
    await page.goto('/reset-password');
    await page.locator('input[name="email"]').fill('test@example.com');
    await page.locator('input[name="token"]').fill('sometoken');
    await page.locator('input[name="newPassword"]').fill('NewPass123!');
    await page.locator('input[name="confirmPassword"]').fill('DifferentPass!');
    await page.getByRole('button', { name: /reset password/i }).click();
    await expect(page.locator('[role="alert"]')).toBeVisible({ timeout: 5_000 });
  });
});
