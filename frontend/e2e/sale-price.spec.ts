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

    const updateRes = await page.request.put(`${API}/products/${product.id}`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: { salePrice: 75 },
    });
    expect(updateRes.status()).toBe(200);
    const updated = await updateRes.json();
    expect(updated.salePrice).toBe(75);
  });

  test('API returns showReduction and discount when 28-day rule met', async ({ page }) => {
    test.setTimeout(90_000);
    // Ensure admin token is ready (beforeAll may still be running on first test)
    expect(adminToken).toBeTruthy();
    await setAuthInBrowser(page, adminToken, adminEmail, 'Test', 'User', 'Admin');

    // Create product with sale price AND backdated price in one step
    const createRes = await page.request.post(`${API}/products`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: {
        name: 'Reduction API Test Dress',
        description: 'Testing showReduction flag',
        price: 200,
        salePrice: 150,
        era: '1980s',
        category: '80s',
        size: '12',
        condition: 'excellent',
        imageUrl: 'https://placehold.co/400x500',
        inStock: true,
        backdatePriceDays: 35,
      },
    });
    expect(createRes.status()).toBe(201);
    const product = await createRes.json();

    // Public API should return showReduction: true and correct discount
    const publicRes = await page.request.get(`${API}/products/${product.id}`);
    const publicData = await publicRes.json();
    expect(publicData.showReduction).toBe(true);
    expect(publicData.discountPercent).toBe(25);
    expect(publicData.salePrice).toBe(150);
    expect(publicData.price).toBe(200);
  });

  test('API returns showReduction false when price set less than 28 days ago', async ({ page }) => {
    test.setTimeout(90_000);
    await setAuthInBrowser(page, adminToken, adminEmail, 'Test', 'User', 'Admin');

    // Create product with sale price but NO backdating (price just set)
    const createRes = await page.request.post(`${API}/products`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: {
        name: 'No Reduction Test Dress',
        description: 'Should not show as reduction',
        price: 200,
        salePrice: 150,
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

    // Public API should return showReduction: false
    const publicRes = await page.request.get(`${API}/products/${product.id}`);
    const publicData = await publicRes.json();
    expect(publicData.showReduction).toBe(false);
    expect(publicData.salePrice).toBe(150);
  });

  test('sale price shows on product detail page with discount when 28-day rule met', async ({ page }) => {
    test.setTimeout(90_000);
    await setAuthInBrowser(page, adminToken, adminEmail, 'Test', 'User', 'Admin');

    // Create product with sale price and backdated price in one step
    const createRes = await page.request.post(`${API}/products`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: {
        name: 'Detail Sale Dress',
        description: 'Sale detail test',
        price: 300,
        salePrice: 210,
        era: '2020s',
        category: 'modern',
        size: '8',
        condition: 'mint',
        imageUrl: 'https://placehold.co/400x500',
        inStock: true,
        backdatePriceDays: 35,
      },
    });
    expect(createRes.status()).toBe(201);
    const product = await createRes.json();

    // Verify API returns showReduction: true
    const getRes = await page.request.get(`${API}/products/${product.id}`);
    const productData = await getRes.json();
    expect(productData.showReduction).toBe(true);
    expect(productData.discountPercent).toBe(30);

    // Fresh context for clean product store
    const freshContext = await page.context().browser()!.newContext();
    const freshPage = await freshContext.newPage();
    await freshPage.goto(`/product/${product.id}`);
    await expect(freshPage.locator('.detail__name')).toBeVisible({ timeout: 15_000 });

    await expect(freshPage.locator('.detail__price--original')).toBeVisible({ timeout: 5_000 });
    await expect(freshPage.locator('.detail__price--sale')).toBeVisible();
    await expect(freshPage.locator('.detail__discount')).toBeVisible();
    await expect(freshPage.locator('.detail__discount')).toContainText('30%');
    await freshContext.close();
  });

});

test.describe('Sale Price Clearing', () => {

  test('admin can clear sale price by setting to 0', async ({ page }) => {
    test.setTimeout(90_000);
    const token = await registerAdmin(page, uniqueEmail('sale-clear-admin'));

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

    const updateRes = await page.request.put(`${API}/products/${product.id}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { salePrice: 0 },
    });
    expect(updateRes.status()).toBe(200);
    const updated = await updateRes.json();
    expect(updated.salePrice).toBeNull();
  });
});
