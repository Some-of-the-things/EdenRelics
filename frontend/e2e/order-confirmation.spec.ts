import { test, expect } from '@playwright/test';

test.describe('Order Confirmation', () => {
  test('shows error for invalid order ID', async ({ page }) => {
    await page.goto('/order-confirmation/00000000-0000-0000-0000-000000000000');
    // Should show error message instead of infinite loading
    await expect(page.getByText(/could not load|not found|error/i)).toBeVisible({ timeout: 10_000 });
  });

  test('shows return to shop link on error', async ({ page }) => {
    await page.goto('/order-confirmation/00000000-0000-0000-0000-000000000000');
    await expect(page.getByText(/could not load|not found|error/i)).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('link', { name: /shop|home|return/i })).toBeVisible();
  });
});
