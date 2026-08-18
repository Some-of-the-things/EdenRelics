/**
 * The Eden Relics API client: where a listing plan comes from, and where instrumentation goes.
 *
 * Two origins, on purpose. The cross-listing plan comes from the main API (it is built from a shop
 * piece), and events go to the seller tool's own API, because that is where the tool's numbers live
 * — per-seller and joinable to a garment, which the cookieless site analytics cannot do.
 *
 * Requests go out from the service worker, whose host permissions cover both origins, so they are
 * not subject to CORS. Nothing here sends a cookie.
 */

import { EventKind, MaxDetailLength } from '../shared/protocol.js';
import { get, set, update } from './storage.js';
import { endpoints, token } from './auth.js';

/** Events that could not be sent yet, oldest first. */
const OutboxKey = 'eden.outbox';

/**
 * How many unsent events we keep.
 *
 * Bounded because an unbounded outbox on a seller who has been offline for a month is a storage
 * quota failure that would take the extension down with it. If we ever hit this we have lost data,
 * so the cap is far above any plausible burst: one event per listing action, and the rate limiter
 * already caps those at 20 an hour per platform.
 */
const OutboxLimit = 500;

/** The server accepts at most this many per request. */
const BatchLimit = 100;

async function authorised() {
  const [stored, urls] = await Promise.all([token(), endpoints()]);
  if (!stored || !urls) {
    return null;
  }
  return { headers: { Authorization: `Bearer ${stored}`, 'Content-Type': 'application/json' }, urls };
}

/**
 * Every platform's readiness for one piece.
 *
 * The extension deliberately does not compute any of this itself. The adapter that knows what Vinted
 * needs lives on the server and is the same one the admin readiness view uses, so there is exactly
 * one answer to "can this go out", and the extension is not a second opinion that can drift.
 */
export async function fetchPreview(productId) {
  const ctx = await authorised();
  if (!ctx) {
    return { ok: false, error: 'not-paired' };
  }
  try {
    const response = await fetch(`${ctx.urls.apiUrl}/api/cross-listing/preview/${productId}`, {
      headers: ctx.headers,
    });
    if (response.status === 401 || response.status === 403) {
      return { ok: false, error: 'not-authorised' };
    }
    if (!response.ok) {
      return { ok: false, error: `http-${response.status}` };
    }
    return { ok: true, preview: await response.json() };
  } catch {
    return { ok: false, error: 'network' };
  }
}

/**
 * Record one event, or park it until we can.
 *
 * Instrumentation must never interfere with the seller's work, so nothing here throws and nothing
 * here blocks a fill. But it also must not quietly lose data — brief §10, "retrofitting analytics
 * means losing the first months", and an event dropped because the wifi blinked is the same loss in
 * miniature. So a failed send goes to the outbox and rides along with the next successful one; the
 * API accepts backdated events for exactly this reason (30 days, and it clamps anything older).
 */
export async function recordEvent({ kind, platform, garmentId, durationMs, detail }) {
  const event = {
    kind,
    platform,
    garmentId: garmentId ?? undefined,
    durationMs: durationMs ?? undefined,
    detail: detail ? String(detail).slice(0, MaxDetailLength) : undefined,
    occurredAtUtc: new Date().toISOString(),
  };
  await update(OutboxKey, [], (list) => [...list, event].slice(-OutboxLimit));
  await flushOutbox();
}

/** Try to send whatever is parked. Safe to call at any time; silent when there is nothing to do. */
export async function flushOutbox() {
  const parked = await get(OutboxKey, []);
  if (parked.length === 0) {
    return { sent: 0 };
  }
  const ctx = await authorised();
  if (!ctx) {
    return { sent: 0, error: 'not-paired' };
  }

  const batch = parked.slice(0, BatchLimit);
  try {
    const response = await fetch(`${ctx.urls.toolApiUrl}/events`, {
      method: 'POST',
      headers: ctx.headers,
      body: JSON.stringify({ events: batch }),
    });
    if (!response.ok) {
      // A 400 means the server refuses this batch on its merits — an unknown kind, or a kind only
      // the server may record. Retrying forever would wedge the outbox behind a poison event, so
      // drop the batch and keep the rest. Anything else (401, 5xx, offline) is worth retrying.
      if (response.status === 400) {
        await set(OutboxKey, parked.slice(batch.length));
      }
      return { sent: 0, error: `http-${response.status}` };
    }
    await set(OutboxKey, parked.slice(batch.length));
    return { sent: batch.length };
  } catch {
    return { sent: 0, error: 'network' };
  }
}

export async function outboxDepth() {
  return (await get(OutboxKey, [])).length;
}

/** Convenience wrappers, so callers name the metric rather than the string. */
export const events = {
  attempted: (platform, garmentId) =>
    recordEvent({ kind: EventKind.Attempted, platform, garmentId }),
  succeeded: (platform, garmentId, durationMs) =>
    recordEvent({ kind: EventKind.Succeeded, platform, garmentId, durationMs }),
  failed: (platform, garmentId, detail, durationMs) =>
    recordEvent({ kind: EventKind.Failed, platform, garmentId, detail, durationMs }),
};

