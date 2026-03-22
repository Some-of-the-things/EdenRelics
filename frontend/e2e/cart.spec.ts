import { test, expect } from '@playwright/test';

test.describe('Cart', () => {
  test('can add product to cart and view cart', async ({ page }) => {
    await page.goto('/');
    // Wait for product cards
    await expect(page.locator('.product-card').first()).toBeVisible({ timeout: 10_000 });

    // Click "Add to Cart" on the first product card
    await page.locator('.product-card__add-btn').first().click();

    // Navigate to cart via the header link (stays in SPA, preserves in-memory cart)
    await page.locator('a[href="/cart"]').first().click();
    await expect(page).toHaveURL(/\/cart/);

    // Cart should have at least one item
    await expect(page.locator('.cart-item').first()).toBeVisible({ timeout: 5_000 });
    // Should show a price
    await expect(page.locator('.cart__total-price')).toBeVisible();
  });

  test('can remove item from cart', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('.product-card').first()).toBeVisible({ timeout: 10_000 });
    await page.locator('.product-card__add-btn').first().click();

    // Navigate to cart via SPA link
    await page.locator('a[href="/cart"]').first().click();
    await expect(page).toHaveURL(/\/cart/);
    await expect(page.locator('.cart-item').first()).toBeVisible({ timeout: 5_000 });

    // Remove the item (× button)
    await page.locator('.cart-item__remove').first().click();
    // Cart should now be empty
    await expect(page.getByText(/your cart is empty/i)).toBeVisible({ timeout: 5_000 });
  });

  test('guest checkout shows checkout form after clicking proceed', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('.product-card').first()).toBeVisible({ timeout: 10_000 });
    await page.locator('.product-card__add-btn').first().click();

    // Navigate to cart via SPA link
    await page.locator('a[href="/cart"]').first().click();
    await expect(page).toHaveURL(/\/cart/);
    await expect(page.locator('.cart-item').first()).toBeVisible({ timeout: 5_000 });

    // Click Proceed to Checkout
    await page.locator('.cart__checkout-btn').click();

    // Should show checkout form with shipping address and email for guests
    await expect(page.locator('.checkout')).toBeVisible({ timeout: 5_000 });
    await expect(page.getByRole('heading', { name: 'Shipping Address' })).toBeVisible();
    await expect(page.locator('.checkout').getByRole('heading', { name: 'Contact' })).toBeVisible();
  });
});
