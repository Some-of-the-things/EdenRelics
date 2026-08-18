/**
 * A promise wrapper over `chrome.storage.local`, and the keys we use.
 *
 * MV3 service workers are killed aggressively and restarted on the next event, so anything that has
 * to survive between two user actions lives here rather than in a module variable. Treating memory
 * as durable is the classic MV3 bug and it would show up as a rate limiter that resets itself —
 * which is the one piece of state whose whole job is not to.
 */

export const Keys = Object.freeze({
  /** The Eden Relics session token. Never a marketplace credential — see auth.js. */
  Token: 'eden.token',
  /** Which Eden environment we were paired against. */
  Endpoints: 'eden.endpoints',
  /** Jobs waiting to be started. */
  Queue: 'eden.queue',
  /** Job currently attached to an open tab, keyed by tab id. */
  Active: 'eden.active',
  /** Rolling action timestamps per platform, for the rate limiter. */
  RateStamps: 'eden.rate',
  /** When a listing was last started, so the between-listings pace survives a worker restart. */
  LastStartedAt: 'eden.lastStarted',
  /** The most recent outcomes, for the popup to show. */
  Recent: 'eden.recent',
  /** Last successful sale check per platform, for the honest "last checked" line. */
  SaleChecks: 'eden.saleChecks',
});

export async function get(key, fallback = null) {
  const bag = await chrome.storage.local.get(key);
  return bag[key] ?? fallback;
}

export async function set(key, value) {
  await chrome.storage.local.set({ [key]: value });
}

export async function remove(key) {
  await chrome.storage.local.remove(key);
}

/**
 * Read, transform, write.
 *
 * Not atomic — `chrome.storage` offers no transaction — but every writer here runs on the service
 * worker's single event loop, and the alternative (a lock in storage) would need the same guarantee
 * it is trying to provide.
 */
export async function update(key, fallback, transform) {
  const current = await get(key, fallback);
  const next = transform(current);
  await set(key, next);
  return next;
}

/** How many outcomes the popup keeps. Enough to explain what just happened, not a history feature. */
export const RecentLimit = 10;

export async function pushRecent(entry) {
  await update(Keys.Recent, [], (list) => [entry, ...list].slice(0, RecentLimit));
}
