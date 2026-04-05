import { test, expect } from '@playwright/test';
import { registerAdmin, setAuthInBrowser, uniqueEmail } from './helpers';

const API = 'http://localhost:5260/api';

test.describe('Blog', () => {
  let adminToken: string;
  let adminEmail: string;
  let postTitle: string;
  const postContent = '<p>This is a test blog post created by e2e tests.</p>';
  const postExcerpt = 'A short test excerpt';
  const postAuthor = 'E2E Tester';

  test.beforeAll(async ({ browser }) => {
    const page = await browser.newPage();
    adminEmail = uniqueEmail('blog-admin');
    adminToken = await registerAdmin(page, adminEmail);

    // Create the post in beforeAll so dependent tests always find it
    postTitle = `E2E Test Post ${Date.now()}`;
    const res = await page.request.post(`${API}/blog`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: {
        title: postTitle,
        content: postContent,
        excerpt: postExcerpt,
        author: postAuthor,
        published: true,
      },
    });
    expect(res.status()).toBe(201);

    await page.close();
  });

  test('admin can create a published blog post via API', async ({ page }) => {
    const title = `API Blog Post ${Date.now()}`;
    const res = await page.request.post(`${API}/blog`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: {
        title,
        content: postContent,
        excerpt: postExcerpt,
        author: postAuthor,
        published: true,
      },
    });
    expect(res.status()).toBe(201);
    const body = await res.json();
    expect(body.title).toBe(title);
    expect(body.slug).toBeTruthy();
    expect(body.published).toBe(true);
  });

  test('blog list page shows the published post', async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('eden_cookie_consent', 'all');
    });
    await page.goto('/blog');
    const postCard = page.locator('a', { hasText: postTitle });
    await expect(postCard).toBeVisible({ timeout: 15_000 });
    await expect(postCard.getByText(postExcerpt)).toBeVisible();
  });

  test('clicking a post navigates to the detail page', async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('eden_cookie_consent', 'all');
    });
    await page.goto('/blog');
    await page.getByText(postTitle).click();
    await expect(page).toHaveURL(/\/blog\//);
    await expect(page.getByText(postTitle)).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText(postAuthor)).toBeVisible();
  });

  test('draft posts are not visible on the public blog page', async ({
    page,
  }) => {
    await page.addInitScript(() => {
      localStorage.setItem('eden_cookie_consent', 'all');
    });
    const draftTitle = `Draft Post ${Date.now()}`;
    const res = await page.request.post(`${API}/blog`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: {
        title: draftTitle,
        content: '<p>Draft content</p>',
        published: false,
      },
    });
    expect(res.status()).toBe(201);

    await page.goto('/blog');
    await expect(page.getByText(postTitle)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText(draftTitle)).not.toBeVisible();
  });

  test('admin can create a post from the admin UI', async ({ page }) => {
    await setAuthInBrowser(page, adminToken, adminEmail, 'Test', 'User', 'Admin');
    await page.goto('/admin');

    // Switch to blog tab
    await page.getByRole('button', { name: /blog/i }).click();

    // Open new post form
    await page.getByRole('button', { name: /new post/i }).click();

    // Fill the form
    const uiPostTitle = `UI Blog Post ${Date.now()}`;
    await page.getByRole('textbox', { name: /title/i }).fill(uiPostTitle);
    await page.locator('textarea[name="blogContent"]').fill('<p>Post created from admin UI</p>');
    await page.locator('select[name="blogPublished"]').selectOption({ label: 'Published' });

    // Submit
    await page.getByRole('button', { name: /create/i }).click();

    // Verify it appears in the admin table
    await expect(page.getByText(uiPostTitle)).toBeVisible({ timeout: 10_000 });

    // Verify it's visible on the public blog page
    await page.goto('/blog');
    await expect(page.getByText(uiPostTitle)).toBeVisible({ timeout: 10_000 });
  });
});
