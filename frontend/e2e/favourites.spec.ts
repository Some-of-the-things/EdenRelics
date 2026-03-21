import { test, expect } from '@playwright/test';
import { registerUser, setAuthInBrowser, uniqueEmail } from './helpers';

test.describe('Favourites', () => {
  test('favourite button visible on product cards', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('.product-card').first()).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('.product-card__fav').first()).toBeVisible();
  });

  test('clicking favourite when logged out redirects to login', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('.product-card').first()).toBeVisible({ timeout: 10_000 });
    await page.locator('.product-card__fav').first().click();
    await expect(page).toHaveURL(/\/login/, { timeout: 5_000 });
  });

  test('authenticated user can toggle favourite on product card', async ({ page }) => {
    const email = uniqueEmail('fav-toggle');
    const token = await registerUser(page, email);
    await setAuthInBrowser(page, token, email);

    await page.goto('/');
    await expect(page.locator('.product-card').first()).toBeVisible({ timeout: 10_000 });

    const favBtn = page.locator('.product-card__fav').first();
    // Click to favourite
    await favBtn.click();
    await expect(favBtn).toHaveClass(/product-card__fav--active/, { timeout: 5_000 });

    // Click again to unfavourite
    await favBtn.click();
    await expect(favBtn).not.toHaveClass(/product-card__fav--active/, { timeout: 5_000 });
  });

  test('favourite button works on product detail page', async ({ page }) => {
    const email = uniqueEmail('fav-detail');
    const token = await registerUser(page, email);
    await setAuthInBrowser(page, token, email);

    await page.goto('/');
    await expect(page.locator('.product-card').first()).toBeVisible({ timeout: 10_000 });

    // Navigate to product detail
    await page.locator('.product-card__name a').first().click();
    await expect(page).toHaveURL(/\/product\//);

    // Click favourite button
    const favBtn = page.locator('.detail__fav-btn');
    await expect(favBtn).toBeVisible({ timeout: 5_000 });
    await favBtn.click();
    await expect(favBtn).toHaveClass(/detail__fav-btn--active/, { timeout: 5_000 });
  });

  test('favourites persist across page navigation', async ({ page }) => {
    const email = uniqueEmail('fav-persist');
    const token = await registerUser(page, email);
    await setAuthInBrowser(page, token, email);

    await page.goto('/');
    await expect(page.locator('.product-card').first()).toBeVisible({ timeout: 10_000 });

    // Favourite the first product
    await page.locator('.product-card__fav').first().click();
    await expect(page.locator('.product-card__fav').first()).toHaveClass(/product-card__fav--active/, { timeout: 5_000 });

    // Navigate away and back
    await page.locator('a[href="/cart"]').first().click();
    await expect(page).toHaveURL(/\/cart/);
    await page.locator('a[href="/"]').first().click();

    // Favourite should still be active
    await expect(page.locator('.product-card').first()).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('.product-card__fav').first()).toHaveClass(/product-card__fav--active/, { timeout: 5_000 });
  });
});
