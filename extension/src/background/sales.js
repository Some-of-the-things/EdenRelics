/**
 * Watching for sales on the platforms we cannot watch from a server.
 *
 * Decision 2 (14 August 2026): there is no server-side route for Vinted or Depop, so the extension
 * checks the seller's own session on a jittered interval — and we say so at onboarding rather than
 * letting them find out. The documented failure this is honest about is the one sellers abandon a
 * tool over: a piece sells at 3am with the laptop shut, nothing syncs, and they wake to a double
 * sale. Etsy and eBay are watched server-side and continuously, and a sale *there* still propagates
 * outward without the seller's machine being on.
 *
 * `chrome.alarms` only fires while Chrome is running, which happens to be exactly the honest
 * semantics: "while your browser is open" is not a caveat bolted onto this, it is what it does.
 *
 * The mechanism per platform is unresearched, and stays inert rather than guessing. Guessing which
 * request lists sold items would be guessing about money, and a delist driven by a wrong guess is
 * worse than no delist at all.
 */

import { Jitter, Pace } from '../shared/pacing.js';
import { platforms } from '../content/platforms/registry.js';
import { Keys, get, update } from './storage.js';

export const SaleAlarm = 'eden.sales';

const jitter = new Jitter();

/**
 * Book the next check.
 *
 * One-shot and re-armed each time rather than periodic, because a periodic alarm is a fixed interval
 * — the machine-regular signature rule 4 rules out.
 */
export async function scheduleSaleCheck() {
  chrome.alarms.create(SaleAlarm, { when: Date.now() + jitter.next(Pace.SaleCheckMs) });
}

/**
 * Look for sales on every platform that has a researched way of being asked.
 *
 * Returns a per-platform status so the popup can tell the seller what is actually being watched and
 * when it last worked — which, on a best-effort channel, is the only useful thing to show.
 */
export async function checkSales(now = Date.now()) {
  const results = {};

  for (const platformModule of platforms) {
    const { platform, sales } = platformModule;
    if (!sales || sales.research === 'unresearched' || !sales.url) {
      results[platform] = { watched: false, reason: 'unresearched', at: now };
      continue;
    }

    try {
      // The seller's own logged-in session carries this; we never hold their credentials, so an
      // unauthenticated answer means they are signed out, not that anything is broken.
      const response = await fetch(sales.url, { credentials: 'include' });
      if (response.status === 401 || response.status === 403) {
        results[platform] = { watched: false, reason: 'signed-out', at: now };
        continue;
      }
      if (!response.ok) {
        results[platform] = { watched: false, reason: `http-${response.status}`, at: now };
        continue;
      }
      results[platform] = { watched: true, reason: null, at: now, checkedAt: now };
    } catch {
      results[platform] = { watched: false, reason: 'network', at: now };
    }
  }

  await update(Keys.SaleChecks, {}, (previous) => merge(previous, results));
  return results;
}

/**
 * Keep the last *successful* check time even when the latest attempt failed.
 *
 * "Last checked 3 minutes ago (failed)" and "last checked never" are different situations, and a
 * seller deciding whether to trust the sync needs to be able to tell them apart.
 */
function merge(previous, latest) {
  const merged = { ...previous };
  for (const [platform, status] of Object.entries(latest)) {
    merged[platform] = {
      ...status,
      lastSuccessAt: status.watched ? status.at : (previous[platform]?.lastSuccessAt ?? null),
    };
  }
  return merged;
}

export async function saleStatus() {
  return get(Keys.SaleChecks, {});
}
