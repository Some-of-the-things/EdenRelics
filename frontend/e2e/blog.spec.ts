import { test, expect } from '@playwright/test';
import { registerAdmin, setAuthInBrowser, uniqueEmail } from './helpers';

const API = 'http://localhost:5260/api';

// Tests share state (the post created in beforeAll is consumed by tests 2–4),
// so they must run in one worker in declared order.
test.describe.configure({ mode: 'serial' });

test.describe('Blog', () => {
  let adminToken: string;
  let adminEmail: string;
  let postTitle: string;
  let postExcerpt: string;
  let draftTitle: string;
  let uiPostTitle: string;
  const postContent = '<p>This is a test blog post created by e2e tests.</p>';
  const postAuthor = 'E2E Tester';
  const createdPostIds: string[] = [];

  test.beforeAll(async ({ browser }) => {
    const page = await browser.newPage();
    adminEmail = uniqueEmail('blog-admin');
    adminToken = await registerAdmin(page, adminEmail);

    // Set unique-per-run identifiers so reruns don't collide on prior data
    const runId = Date.now();
    postTitle = `E2E Test Post ${runId}`;
    postExcerpt = `A short test excerpt ${runId}`;
    draftTitle = `Draft Post ${runId}`;
    uiPostTitle = `UI Blog Post ${runId}`;

    // Create the post in beforeAll so dependent tests always find it
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
    const body = await res.json();
    createdPostIds.push(body.id);

    await page.close();
  });

  test.afterAll(async ({ browser }) => {
    if (createdPostIds.length === 0) {
      return;
    }
    const page = await browser.newPage();
    for (const id of createdPostIds) {
      await page.request.delete(`${API}/blog/${id}`, {
        headers: { Authorization: `Bearer ${adminToken}` },
      }).catch(() => undefined);
    }
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
    createdPostIds.push(body.id);
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
    const res = await page.request.post(`${API}/blog`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: {
        title: draftTitle,
        content: '<p>Draft content</p>',
        published: false,
      },
    });
    expect(res.status()).toBe(201);
    const body = await res.json();
    createdPostIds.push(body.id);

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

    // Track for cleanup
    const list = await page.request.get(`${API}/blog/admin/all`, {
      headers: { Authorization: `Bearer ${adminToken}` },
    });
    if (list.ok()) {
      const posts: Array<{ id: string; title: string }> = await list.json();
      const created = posts.find((p) => p.title === uiPostTitle);
      if (created) {
        createdPostIds.push(created.id);
      }
    }
  });
});
