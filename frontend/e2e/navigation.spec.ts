import { test, expect } from '@playwright/test';

test.describe('Navigation and pages', () => {
  test('home page loads', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveTitle(/eden relics/i);
  });

  test('privacy policy page loads', async ({ page }) => {
    await page.goto('/privacy-policy');
    await expect(page.getByText(/privacy/i).first()).toBeVisible();
  });

  test('modern slavery policy page loads', async ({ page }) => {
    await page.goto('/modern-slavery-policy');
    await expect(page.getByText(/slavery|modern/i).first()).toBeVisible();
  });

  test('supply chain policy page loads', async ({ page }) => {
    await page.goto('/supply-chain-policy');
    await expect(page.getByText(/supply chain/i).first()).toBeVisible();
  });

  test('unknown routes redirect to home', async ({ page }) => {
    await page.goto('/this-does-not-exist');
    await expect(page).toHaveURL('/');
  });

  test('header navigation links are present', async ({ page }) => {
    await page.goto('/');
    // Should have cart link
    const cartLink = page.getByRole('link', { name: /cart|bag/i });
    await expect(cartLink).toBeVisible();
  });
});
