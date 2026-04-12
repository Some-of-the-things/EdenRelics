import { test, expect } from '@playwright/test';
import { registerAdmin, setAuthInBrowser, uniqueEmail } from './helpers';

const API = 'http://localhost:5260/api';

test.describe('Sale Prices', () => {

  let adminToken: string;
  let adminEmail: string;

  test.beforeAll(async ({ browser }) => {
    const page = await browser.newPage();
    adminEmail = uniqueEmail('sale-admin');
    adminToken = await registerAdmin(page, adminEmail);
    await page.close();
  });

  test('admin can set sale price on a product', async ({ page }) => {
    test.setTimeout(90_000);
    await setAuthInBrowser(page, adminToken, adminEmail, 'Test', 'User', 'Admin');

    // Create a product via API
    const createRes = await page.request.post(`${API}/products`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: {
        name: 'Sale Test Dress',
        description: 'A dress for sale testing',
        price: 100,
        era: '1990s',
        category: '90s',
        size: '10',
        condition: 'good',
        imageUrl: 'https://placehold.co/400x500',
        inStock: true,
      },
    });
    expect(createRes.status()).toBe(201);
    const product = await createRes.json();

    // Set sale price via API
    const updateRes = await page.request.put(`${API}/products/${product.id}`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: { salePrice: 75 },
    });
    expect(updateRes.status()).toBe(200);
    const updated = await updateRes.json();
    expect(updated.salePrice).toBe(75);
  });

  test('sale price shows on product card with discount badge when 28-day rule met', async ({ browser }) => {
    test.setTimeout(90_000);
    // Create product WITHOUT sale price first (to establish PriceSetAtUtc)
    const ctx = await browser.newContext();
    const apiPage = await ctx.newPage();
    const createRes = await apiPage.request.post(`${API}/products`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: {
        name: 'Discounted Vintage Dress',
        description: 'On sale now',
        price: 200,
        era: '1980s',
        category: '80s',
        size: '12',
        condition: 'excellent',
        imageUrl: 'https://placehold.co/400x500',
        inStock: true,
      },
    });
    expect(createRes.status()).toBe(201);
    const product = await createRes.json();

    // Backdate PriceSetAtUtc via test helper endpoint so 28-day rule is met
    const backdateRes = await apiPage.request.put(`${API}/products/${product.id}/backdate-price`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: { days: 30 },
    });
    expect(backdateRes.ok()).toBeTruthy();

    // Now set sale price
    const saleRes = await apiPage.request.put(`${API}/products/${product.id}`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: { salePrice: 150 },
    });
    expect(saleRes.ok()).toBeTruthy();
    await apiPage.close();

    // Navigate with a fresh page that has cookie consent set
    const page = await ctx.newPage();
    await page.addInitScript(() => {
      localStorage.setItem('eden_cookie_consent', 'all');
    });
    await page.goto('/cart');
    // Navigate to home via client-side routing to get fresh product data
    await page.getByRole('link', { name: /eden relics/i }).first().click();
    await expect(page.locator('.product-card').first()).toBeVisible({ timeout: 15_000 });

    // Find the sale product card
    const saleCard = page.locator('.product-card', { hasText: 'Discounted Vintage Dress' }).first();
    await saleCard.scrollIntoViewIfNeeded();
    await expect(saleCard).toBeVisible({ timeout: 10_000 });

    // Should show sale badge
    await expect(saleCard.locator('.product-card__sale-badge')).toBeVisible();
    // Should show original price crossed out
    await expect(saleCard.locator('.product-card__price--original')).toBeVisible();
    // Should show sale price
    await expect(saleCard.locator('.product-card__price--sale')).toBeVisible();
  });

  test('sale price shows on product detail page with discount when 28-day rule met', async ({ page }) => {
    test.setTimeout(90_000);
    await setAuthInBrowser(page, adminToken, adminEmail, 'Test', 'User', 'Admin');
    // Create product at full price first
    const createRes = await page.request.post(`${API}/products`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: {
        name: 'Detail Sale Dress',
        description: 'Sale detail test',
        price: 300,
        era: '2020s',
        category: 'modern',
        size: '8',
        condition: 'mint',
        imageUrl: 'https://placehold.co/400x500',
        inStock: true,
      },
    });
    expect(createRes.status()).toBe(201);
    const product = await createRes.json();

    // Backdate PriceSetAtUtc so 28-day rule is met
    const backdateRes = await page.request.put(`${API}/products/${product.id}/backdate-price`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: { days: 35 },
    });
    expect(backdateRes.ok()).toBeTruthy();

    // Now set sale price
    const saleRes = await page.request.put(`${API}/products/${product.id}`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: { salePrice: 210 },
    });
    expect(saleRes.ok()).toBeTruthy();

    // Verify API returns showReduction: true before checking the UI
    const getRes = await page.request.get(`${API}/products/${product.id}`);
    const productData = await getRes.json();
    expect(productData.showReduction).toBe(true);

    await page.goto(`/product/${product.id}`);
    await expect(page.locator('.detail__name')).toBeVisible({ timeout: 10_000 });

    // Should show original price crossed out
    await expect(page.locator('.detail__price--original')).toBeVisible();
    // Should show sale price
    await expect(page.locator('.detail__price--sale')).toBeVisible();
    // Should show discount badge
    await expect(page.locator('.detail__discount')).toBeVisible();
    await expect(page.locator('.detail__discount')).toContainText('30%');
  });

});

test.describe('Sale Price Clearing', () => {

  test('admin can clear sale price by setting to 0', async ({ page }) => {
    test.setTimeout(90_000);
    const token = await registerAdmin(page, uniqueEmail('sale-clear-admin'));

    // Create product with sale price
    const createRes = await page.request.post(`${API}/products`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        name: 'Clear Sale Dress',
        description: 'Test clearing sale',
        price: 150,
        salePrice: 100,
        era: '1970s',
        category: '70s',
        size: '10',
        condition: 'good',
        imageUrl: 'https://placehold.co/400x500',
        inStock: true,
      },
    });
    expect(createRes.ok()).toBeTruthy();
    const product = await createRes.json();

    // Clear sale price
    const updateRes = await page.request.put(`${API}/products/${product.id}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { salePrice: 0 },
    });
    expect(updateRes.status()).toBe(200);
    const updated = await updateRes.json();
    expect(updated.salePrice).toBeNull();
  });
});
