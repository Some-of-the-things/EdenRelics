import { test, expect } from '@playwright/test';
import { registerAdmin, setAuthInBrowser, uniqueEmail } from './helpers';

const API = 'http://localhost:5260/api';

test.describe('Product View Count', () => {
  test.describe.configure({ mode: 'serial' });

  let adminToken: string;
  let adminEmail: string;

  test.beforeAll(async ({ browser }) => {
    const page = await browser.newPage();
    adminEmail = uniqueEmail('views-admin');
    adminToken = await registerAdmin(page, adminEmail);
    await page.close();
  });

  test('viewing a product increments the view count', async ({ page, request }) => {
    // Create a product
    const createRes = await request.post(`${API}/products`, {
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

    // Visit the product detail page (triggers view) and wait for the view API call to complete
    const [viewResponse] = await Promise.all([
      page.waitForResponse(resp => resp.url().includes('/api/analytics/') && resp.status() === 200, { timeout: 15_000 }).catch(() => null),
      page.goto(`/product/${product.id}`),
    ]);

    await expect(page.locator('.detail__name')).toBeVisible({ timeout: 10_000 });

    // Verify view count via standalone API context (won't die with page)
    const getRes = await request.get(`${API}/products/${product.id}`, {
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
