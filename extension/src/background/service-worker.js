/**
 * The extension's only long-lived decision-maker.
 *
 * It routes messages, owns the queue and is the only place that talks to the Eden Relics API. The
 * content scripts know how to fill a form and nothing else; they are handed a plan and report what
 * happened. That split is what keeps the per-platform modules disposable — the half that breaks
 * monthly has no state and no credentials in it.
 */

import { FailureReason, Message, describeFailure } from '../shared/protocol.js';
import { moduleForUrl } from '../content/platforms/registry.js';
import * as auth from './auth.js';
import * as store from './storage.js';
import { events, flushOutbox, outboxDepth } from './api.js';
import { explain } from '../shared/wording.js';
import { PaceAlarm, badge, enqueue, pump } from './queue.js';
import { checkSales, SaleAlarm, scheduleSaleCheck } from './sales.js';

/** First run: explain the tool before it does anything, rather than after (brief §8). */
chrome.runtime.onInstalled.addListener(async (details) => {
  if (details.reason === 'install') {
    await chrome.tabs.create({ url: chrome.runtime.getURL('src/onboarding/onboarding.html') });
  }
  await scheduleSaleCheck();
});

chrome.runtime.onStartup.addListener(async () => {
  // A worker start is the cheapest moment to retry anything the network lost last session.
  await flushOutbox();
  await scheduleSaleCheck();
});

chrome.alarms.onAlarm.addListener(async (alarm) => {
  if (alarm.name === PaceAlarm) {
    await pump();
  }
  if (alarm.name === SaleAlarm) {
    await checkSales();
    await scheduleSaleCheck();
  }
});

/**
 * A tab closing is a legitimate ending.
 *
 * If the seller looks at the filled form and decides not to list the piece, that is not an extension
 * failure and must not be recorded as one — it would put the failure rate up for the one thing the
 * seller is explicitly invited to do. The job simply ends.
 */
chrome.tabs.onRemoved.addListener(async (tabId) => {
  const active = await store.get(store.Keys.Active, {});
  if (!active[tabId]) {
    return;
  }
  const { [tabId]: closed, ...rest } = active;
  await store.set(store.Keys.Active, rest);
  if (closed.state === 'filled') {
    await store.pushRecent({
      at: Date.now(),
      platform: closed.platform,
      productId: closed.productId,
      state: 'abandoned',
      message: `You closed the ${closed.platform} tab before publishing. Nothing went live.`,
      fallback: closed.fallback ?? null,
    });
  }
  await pump();
});

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  handle(message, sender)
    .then(sendResponse)
    .catch((error) => sendResponse({ ok: false, error: String(error?.message ?? error) }));
  // Keeps the channel open for the async work above.
  return true;
});

async function handle(message, sender) {
  switch (message?.kind) {
    case Message.Pair:
      return handlePair(message, sender);
    case Message.Unpair:
      await auth.unpair();
      return { ok: true };
    case Message.GetStatus:
      return handleStatus();
    case Message.QueueListing:
      return handleQueue(message, sender);
    case Message.ContentReady:
      return handleContentReady(message, sender);
    case Message.FillOutcome:
      return handleOutcome(message, sender);
    default:
      return { ok: false, error: 'unknown-message' };
  }
}

/**
 * Pairing, and the two things that make it safe.
 *
 * The message has to come from a content script (so a page cannot post directly to the extension),
 * and that script has to be running on one of our own origins. Without both, any site the seller
 * visits could hand us a token and point the extension at its own API.
 */
async function handlePair(message, sender) {
  const origin = sender?.origin ?? (sender?.url ? new URL(sender.url).origin : null);
  if (!sender?.tab || !auth.isTrustedOrigin(origin)) {
    return { ok: false, error: 'untrusted-origin' };
  }
  const result = await auth.pair(message.payload ?? {});
  if (result.paired) {
    await badge('');
    await flushOutbox();
  }
  return { ok: result.paired, error: result.error ?? null };
}

async function handleStatus() {
  const [session, queue, active, recent, saleChecks, outbox] = await Promise.all([
    auth.status(),
    store.get(store.Keys.Queue, []),
    store.get(store.Keys.Active, {}),
    store.get(store.Keys.Recent, []),
    store.get(store.Keys.SaleChecks, {}),
    outboxDepth(),
  ]);
  return {
    ok: true,
    session,
    queued: queue.length,
    active: Object.values(active),
    recent,
    saleChecks,
    unsentEvents: outbox,
  };
}

async function handleQueue(message, sender) {
  const origin = sender?.origin ?? (sender?.url ? new URL(sender.url).origin : null);
  if (!sender?.tab || !auth.isTrustedOrigin(origin)) {
    return { ok: false, error: 'untrusted-origin' };
  }
  const { productId, platform, garmentId } = message.payload ?? {};
  if (!productId || !platform) {
    return { ok: false, error: 'incomplete-job' };
  }
  const session = await auth.status();
  if (!session.paired || session.expired) {
    return { ok: false, error: session.paired ? 'session-expired' : 'not-paired' };
  }
  await enqueue({ productId, platform, garmentId: garmentId ?? null });
  return { ok: true };
}

/**
 * A content script has loaded on a marketplace page and wants to know if it has work.
 *
 * This is also how a publish is observed. We never press the button, so the only evidence that a
 * listing went live is the page the seller lands on afterwards — which is exactly the right shape:
 * `ExtensionPublishSucceeded` then means a listing genuinely published, not a form we managed to
 * fill in. A form we filled and the seller left alone is neither a success nor a failure.
 */
async function handleContentReady(message, sender) {
  const tabId = sender?.tab?.id;
  if (!tabId) {
    return { ok: false, error: 'no-tab' };
  }
  const active = await store.get(store.Keys.Active, {});
  const job = active[tabId];
  if (!job) {
    return { ok: true, job: null };
  }

  const platformModule = moduleForUrl(message.url) ?? null;
  const url = String(message.url ?? '');

  // The seller published. Record it, and let the job go.
  if (job.state === 'filled' && isPublished(job.platform, url)) {
    const { [tabId]: done, ...rest } = active;
    await store.set(store.Keys.Active, rest);
    await events.succeeded(job.platform, job.garmentId, Date.now() - job.attemptedAt);
    await store.pushRecent({
      at: Date.now(),
      platform: job.platform,
      productId: job.productId,
      state: 'published',
      message: `Published to ${job.platform}.`,
    });
    await badge('');
    await pump();
    return { ok: true, job: null };
  }

  if (job.state !== 'opening') {
    return { ok: true, job: null };
  }

  if (!platformModule || !platformModule.isListingPage(url)) {
    return finishWithFailure(tabId, job, FailureReason.PageNotRecognised, null);
  }

  await store.update(store.Keys.Active, {}, (bag) => ({
    ...bag,
    [tabId]: { ...bag[tabId], state: 'filling' },
  }));

  return {
    ok: true,
    job: {
      platform: job.platform,
      fields: job.fields,
      fallback: job.fallback,
      warnings: job.warnings,
    },
  };
}

async function handleOutcome(message, sender) {
  const tabId = sender?.tab?.id;
  const active = await store.get(store.Keys.Active, {});
  const job = active[tabId];
  if (!job) {
    return { ok: true };
  }

  if (!message.outcome?.ok) {
    return finishWithFailure(
      tabId,
      job,
      message.outcome?.reason ?? FailureReason.FieldNotFound,
      message.outcome?.subject ?? null,
    );
  }

  // Filled, and now it is the seller's turn. Deliberately not recorded as a success — see the note
  // on handleContentReady.
  await store.update(store.Keys.Active, {}, (bag) => ({
    ...bag,
    [tabId]: { ...bag[tabId], state: 'filled', degraded: message.outcome.degraded ?? [] },
  }));
  await store.pushRecent({
    at: Date.now(),
    platform: job.platform,
    productId: job.productId,
    state: 'filled',
    message: `Filled the ${job.platform} form. Review it and press publish yourself.`,
    degraded: message.outcome.degraded ?? [],
  });
  await badge('');
  return { ok: true };
}

async function finishWithFailure(tabId, job, reason, subject) {
  const active = await store.get(store.Keys.Active, {});
  const { [tabId]: failed, ...rest } = active;
  await store.set(store.Keys.Active, rest);

  const detail = describeFailure(reason, subject);
  const message = explain(reason, subject, job.platform);
  await events.failed(job.platform, job.garmentId, detail, Date.now() - job.attemptedAt);
  await store.pushRecent({
    at: Date.now(),
    platform: job.platform,
    productId: job.productId,
    state: 'failed',
    reason,
    detail,
    message,
    fallback: job.fallback ?? null,
  });
  await badge('!');
  await pump();
  // The wording travels with the failure so the overlay and the popup cannot describe the same
  // event two different ways.
  return { ok: true, job: null, failed: { platform: job.platform, message } };
}

function isPublished(platform, url) {
  // Still sitting on the listing form is not a publish, however long they sit there.
  if (moduleForUrl(url)) {
    return false;
  }
  return publishedMatchers[platform]?.(url) ?? false;
}

/**
 * How we recognise a piece that actually went live.
 *
 * A URL shape rather than a DOM selector, because it is far more stable, and because getting this
 * one wrong only undercounts a metric — it can never put a wrong value on a listing. That is the
 * only reason a heuristic is acceptable here when it is not acceptable in the field tables.
 */
const publishedMatchers = {
  Vinted: (url) => /^https:\/\/(www\.)?vinted\.co\.uk\/items\/\d+/.test(url),
  Depop: (url) => /^https:\/\/(www\.)?depop\.com\/products\/[^/]+\/?$/.test(url),
};
