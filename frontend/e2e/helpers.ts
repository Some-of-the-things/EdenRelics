import { Page, expect } from '@playwright/test';

const API = 'http://localhost:5260/api';

/** Register a new user via the API and return the token */
export async function registerUser(
  page: Page,
  email: string,
  password = 'TestPass123!'
): Promise<string> {
  const res = await page.request.post(`${API}/auth/register`, {
    data: { email, password, firstName: 'Test', lastName: 'User' },
  });
  if (res.status() === 409) {
    // Already registered, just login
    return loginUser(page, email, password);
  }
  const body = await res.json();
  return body.token;
}

/** Login a user via the API and return the token */
export async function loginUser(
  page: Page,
  email: string,
  password = 'TestPass123!'
): Promise<string> {
  const res = await page.request.post(`${API}/auth/login`, {
    data: { email, password },
  });
  const body = await res.json();
  return body.token;
}

/** Set auth state in localStorage so the app treats user as logged in */
export async function setAuthInBrowser(
  page: Page,
  token: string,
  email: string,
  firstName = 'Test',
  lastName = 'User',
  role = 'Customer'
): Promise<void> {
  const user = {
    id: parseJwtSub(token),
    email,
    firstName,
    lastName,
    role,
    emailVerified: false,
  };
  await page.addInitScript(
    ({ token, user }) => {
      localStorage.setItem('eden_token', token);
      localStorage.setItem('eden_user', JSON.stringify(user));
    },
    { token, user }
  );
}

/** Register a user, promote to Admin (dev only), and return the admin token */
export async function registerAdmin(
  page: Page,
  email: string,
  password = 'TestPass123!'
): Promise<string> {
  const userToken = await registerUser(page, email, password);
  const res = await page.request.post(`${API}/auth/promote-admin`, {
    headers: { Authorization: `Bearer ${userToken}` },
  });
  const body = await res.json();
  return body.token;
}

/** Crude JWT sub extraction (no verification needed for tests) */
function parseJwtSub(token: string): string {
  const payload = JSON.parse(
    Buffer.from(token.split('.')[1], 'base64').toString()
  );
  return (
    payload[
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'
    ] ?? payload.sub
  );
}

/** Generate a unique email for test isolation */
export function uniqueEmail(prefix: string): string {
  return `${prefix}-${Date.now()}@e2etest.com`;
}
