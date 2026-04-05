import { test, expect } from '@playwright/test';
import { registerAdmin, setAuthInBrowser, uniqueEmail } from './helpers';

const API = 'http://localhost:5260/api';

test.describe('Product View Count', () => {

  let adminToken: string;
  let adminEmail: string;

  test.beforeAll(async ({ browser }) => {
    const page = await browser.newPage();
    adminEmail = uniqueEmail('views-admin');
    adminToken = await registerAdmin(page, adminEmail);
    await page.close();
  });

  test('viewing a product increments the view count', async ({ page }) => {
    // Create a product
    const createRes = await page.request.post(`${API}/products`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: {
        name: 'View Count Test Dress',
        description: 'Testing view tracking',
        price: 99,
        era: '1990s',
        category: '90s',
        size: '10',
        condition: 'good',
        imageUrl: 'https://placehold.co/400x500',
        inStock: true,
      },
    });
    expect(createRes.ok()).toBeTruthy();
    const product = await createRes.json();

    // Record a view via API (same endpoint the frontend calls)
    const viewRes = await page.request.post(`${API}/products/${product.id}/view`, {
      data: {},
    });
    expect(viewRes.ok()).toBeTruthy();

    // Verify view count incremented
    const getRes = await page.request.get(`${API}/products/${product.id}`, {
      headers: { Authorization: `Bearer ${adminToken}` },
    });
    const updated = await getRes.json();
    expect(updated.viewCount).toBeGreaterThanOrEqual(1);
  });

  test('admin products show view count', async ({ page }) => {
    await setAuthInBrowser(page, adminToken, adminEmail, 'Test', 'User', 'Admin');
    await page.goto('/admin');

    // Wait for product cards
    await expect(page.locator('.admin__card').first()).toBeVisible({ timeout: 10_000 });

    // Should have Views link in card
    await expect(page.locator('.admin__link-btn', { hasText: /Views:/ }).first()).toBeVisible();
  });
});
