import { test, expect } from '@playwright/test';
import { uniqueEmail } from './helpers';

test.describe('Authentication', () => {
  test('can register a new account', async ({ page }) => {
    const email = uniqueEmail('reg');
    await page.goto('/register');

    await page.locator('input[name="firstName"]').fill('Test');
    await page.locator('input[name="lastName"]').fill('User');
    await page.locator('input[name="email"]').fill(email);
    await page.locator('input[name="password"]').fill('TestPass123!');

    await page.getByRole('button', { name: 'Create Account', exact: true }).click();

    // Should redirect to account or home after registration
    await expect(page).not.toHaveURL(/\/register/, { timeout: 10_000 });
  });

  test('can login with existing account', async ({ page }) => {
    const email = uniqueEmail('login');
    // Register via API first
    await page.request.post('http://localhost:5260/api/auth/register', {
      data: { email, password: 'TestPass123!', firstName: 'Test', lastName: 'User' },
    });

    await page.goto('/login');
    await page.locator('form.login__card input[name="email"]').fill(email);
    await page.locator('form.login__card input[name="password"]').fill('TestPass123!');
    await page.locator('button.login__btn[type="submit"]').click();

    // Should redirect after login
    await expect(page).not.toHaveURL(/\/login/, { timeout: 10_000 });
  });

  test('login with wrong password shows error', async ({ page }) => {
    const email = uniqueEmail('wrongpw');
    await page.request.post('http://localhost:5260/api/auth/register', {
      data: { email, password: 'TestPass123!', firstName: 'Test', lastName: 'User' },
    });

    await page.goto('/login');
    await page.locator('form.login__card input[name="email"]').fill(email);
    await page.locator('form.login__card input[name="password"]').fill('WrongPassword!');
    await page.locator('button.login__btn[type="submit"]').click();

    // Should show error message
    await expect(page.locator('.login__error[role="alert"]')).toBeVisible({ timeout: 5_000 });
  });

  test('can navigate to forgot password', async ({ page }) => {
    await page.goto('/login');
    const forgotLink = page.getByRole('link', { name: /forgot/i });
    await expect(forgotLink).toBeVisible();
    await forgotLink.click();
    await expect(page).toHaveURL(/\/forgot-password/);
  });

  test('forgot password submits successfully', async ({ page }) => {
    await page.goto('/forgot-password');
    await page.locator('input[name="email"]').fill('anyone@example.com');
    await page.getByRole('button', { name: /send reset link/i }).click();
    // After success, title changes to "Check Your Email"
    await expect(page.getByRole('heading', { name: /check your email/i })).toBeVisible({ timeout: 5_000 });
  });

  test('register link is accessible from login page', async ({ page }) => {
    await page.goto('/login');
    const registerLink = page.getByRole('link', { name: /create one/i });
    await expect(registerLink).toBeVisible();
  });
});
