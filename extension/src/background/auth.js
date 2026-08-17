/**
 * The extension's only credential: an Eden Relics session token, handed over by the seller pressing
 * a button on our own site.
 *
 * **We never hold a marketplace credential** — decision 4 (14 Aug 2026), settled by the brief's own
 * strong preference in §9.5. The extension works inside the seller's existing logged-in Vinted or
 * Depop session and never asks for, transmits or stores a marketplace password. It is also the only
 * answer consistent with a brand whose proposition is trustworthiness. If a future change appears to
 * need one, the change is wrong.
 *
 * The Eden token itself is a bearer for *our* API and nothing else, and the seller can revoke it
 * from the popup at any time.
 */

import { Keys, get, remove, set } from './storage.js';

/** The origins a pairing message is allowed to come from. Anything else is ignored outright. */
export const TrustedOrigins = Object.freeze([
  'https://edenrelics.co.uk',
  'https://www.edenrelics.co.uk',
  'https://staging.edenrelics.co.uk',
  'http://localhost:4200',
]);

export function isTrustedOrigin(origin) {
  return TrustedOrigins.includes(origin);
}

/**
 * Read a JWT's expiry without verifying it.
 *
 * Verification is the API's job and we are in no position to do it. This is only so the popup can
 * say "your session has expired, sign in again" instead of letting every request fail with a 401
 * the seller has to interpret.
 */
export function expiryOf(token) {
  try {
    const [, payload] = String(token).split('.');
    const json = JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')));
    return typeof json.exp === 'number' ? json.exp * 1000 : null;
  } catch {
    return null;
  }
}

export function isExpired(token, now = Date.now()) {
  const expiry = expiryOf(token);
  return expiry !== null && expiry <= now;
}

/**
 * Store the token and the endpoints it belongs to.
 *
 * The page tells us which environment it is rather than the extension guessing, so a staging pairing
 * cannot end up posting events into prod's metrics.
 */
export async function pair({ token, apiUrl, toolApiUrl }) {
  if (!token || !apiUrl || !toolApiUrl) {
    return { paired: false, error: 'Incomplete pairing message.' };
  }
  await set(Keys.Token, token);
  await set(Keys.Endpoints, { apiUrl, toolApiUrl });
  return { paired: true };
}

export async function unpair() {
  await remove(Keys.Token);
  await remove(Keys.Endpoints);
}

export async function token() {
  return get(Keys.Token, null);
}

export async function endpoints() {
  return get(Keys.Endpoints, null);
}

export async function status() {
  const stored = await token();
  if (!stored) {
    return { paired: false, expired: false };
  }
  return { paired: true, expired: isExpired(stored) };
}
