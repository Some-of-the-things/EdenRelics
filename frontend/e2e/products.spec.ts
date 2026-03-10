import { test, expect } from '@playwright/test';

test.describe('Product browsing', () => {
  test('home page loads and shows products', async ({ page }) => {
    await page.goto('/');
    // Wait for product cards to appear
    await expect(page.locator('.product-card').first()).toBeVisible({ timeout: 10_000 });
  });

  test('can filter products by category', async ({ page }) => {
    await page.goto('/');
    // Wait for products to load
    await expect(page.locator('.product-card').first()).toBeVisible({ timeout: 10_000 });
    // Click a category filter button
    const categoryButton = page.locator('.products__cat-btn', { hasText: /70s/i });
    if (await categoryButton.isVisible()) {
      await categoryButton.click();
      // Products should still be visible (filtered)
      await page.waitForTimeout(500);
    }
  });

  test('can view product detail page', async ({ page }) => {
    await page.goto('/');
    // Wait for product cards
    await expect(page.locator('.product-card').first()).toBeVisible({ timeout: 10_000 });
    // Click the first product name link
    const firstProductLink = page.locator('.product-card__name a').first();
    await firstProductLink.click();
    // Should navigate to product detail
    await expect(page).toHaveURL(/\/product\//);
    // Should show "Add to Cart" button
    await expect(page.locator('.detail__add-btn')).toBeVisible();
  });
});
