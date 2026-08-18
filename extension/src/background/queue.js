/**
 * One job at a time, human-paced.
 *
 * This is where brief §4's design rules stop being prose. Every listing is a button press by the
 * seller, jobs never overlap, the gap between them is drawn fresh each time and long enough that the
 * sequence does not read as a machine, and a per-platform ceiling stops anything that goes wrong
 * from going wrong twenty times.
 *
 * The pacing state is persisted rather than held in memory because an MV3 worker is killed between
 * events, and a rate limiter that forgets itself on restart is not a rate limiter.
 */

import { Jitter, MaxActionsPerHour, Pace, RateLimiter } from '../shared/pacing.js';
import { FailureReason, describeFailure, gateFill, planFor } from '../shared/protocol.js';
import { explain } from '../shared/wording.js';
import { moduleFor } from '../content/platforms/registry.js';
import { Keys, get, pushRecent, set, update } from './storage.js';
import { events, fetchPreview } from './api.js';

const jitter = new Jitter();

/** The alarm that wakes us when the between-listings gap has elapsed. */
export const PaceAlarm = 'eden.pace';

/**
 * Queue a listing. Always the result of the seller pressing a button on our own site — there is no
 * path that enqueues on a timer, and nothing in the UI can enqueue more than one press at a time.
 */
export async function enqueue(job) {
  await update(Keys.Queue, [], (list) => [...list, { ...job, queuedAt: Date.now() }]);
  await pump();
}

/** Start the next job if the pace and the rate limit both allow it. */
export async function pump() {
  const queue = await get(Keys.Queue, []);
  if (queue.length === 0) {
    return;
  }

  const active = await get(Keys.Active, {});
  if (Object.keys(active).length > 0) {
    // A form is already open and waiting for the seller. Starting a second one would be the bulk
    // behaviour the whole design is built to avoid, and would also be unusable.
    return;
  }

  const wait = await waitRemainingMs();
  if (wait > 0) {
    // Alarms rather than setTimeout: the worker will very likely be dead before this elapses.
    chrome.alarms.create(PaceAlarm, { when: Date.now() + wait });
    return;
  }

  const [job, ...rest] = queue;
  await set(Keys.Queue, rest);
  await start(job);
}

/**
 * How long until another listing may start.
 *
 * The target is drawn once and stored, so a worker restart cannot re-roll it into something shorter
 * — which would turn "the seller opened the popup" into a way to go faster.
 */
async function waitRemainingMs(now = Date.now()) {
  const last = await get(Keys.LastStartedAt, null);
  if (!last) {
    return 0;
  }
  const nextAllowedAt = last.at + last.gapMs;
  return Math.max(0, nextAllowedAt - now);
}

async function markStarted(now = Date.now()) {
  await set(Keys.LastStartedAt, { at: now, gapMs: jitter.next(Pace.BetweenListingsMs) });
}

/** The per-platform limiter, rehydrated from storage on every use. */
async function limiterFor(platform) {
  const all = await get(Keys.RateStamps, {});
  const limiter = new RateLimiter(MaxActionsPerHour);
  limiter.hydrate(all[platform] ?? []);
  return {
    limiter,
    async commit() {
      await update(Keys.RateStamps, {}, (bag) => ({ ...bag, [platform]: limiter.serialise() }));
    },
  };
}

async function start(job) {
  const { platform, productId } = job;
  const attemptedAt = Date.now();

  await events.attempted(platform, job.garmentId);

  const platformModule = moduleFor(platform);
  if (!platformModule) {
    return refuse(job, FailureReason.UnknownPlatform, null, attemptedAt, null);
  }

  const { limiter, commit } = await limiterFor(platform);
  if (!limiter.allows()) {
    // Not a failure of the marketplace or of our selectors, so it is not reported as one — it is
    // us stopping ourselves. The seller is told, and the job goes back to the front of the queue.
    await update(Keys.Queue, [], (list) => [job, ...list]);
    chrome.alarms.create(PaceAlarm, { when: Date.now() + limiter.retryAfterMs() });
    await pushRecent({
      at: attemptedAt,
      platform,
      productId,
      state: 'paused',
      message: `Paused — ${MaxActionsPerHour} ${platform} actions in the last hour is our own ceiling.`,
    });
    return;
  }

  const result = await fetchPreview(productId);
  if (!result.ok) {
    return refuse(job, FailureReason.Blocked, result.error, attemptedAt, null);
  }

  const plan = planFor(result.preview, platform);
  const gate = gateFill(plan, platformModule);
  if (!gate.allowed) {
    return refuse(job, gate.reason, gate.subject, attemptedAt, plan);
  }

  limiter.record();
  await commit();
  await markStarted(attemptedAt);

  const tab = await chrome.tabs.create({ url: platformModule.newListingUrl, active: true });
  await update(Keys.Active, {}, (bag) => ({
    ...bag,
    [tab.id]: {
      ...job,
      attemptedAt,
      fields: plan.fields,
      fallback: plan.fallback,
      warnings: plan.validation?.warnings ?? [],
      state: 'opening',
    },
  }));
}

/**
 * Stop, and say why.
 *
 * A refusal always carries the paste fallback, because that is the difference between "we could not
 * do it" and "you cannot do it". Brief §3: never let the seller believe something went live that
 * didn't — the corollary being that when we fail, they still have to be able to list the piece.
 */
async function refuse(job, reason, subject, attemptedAt, plan) {
  const detail = describeFailure(reason, subject);
  await events.failed(job.platform, job.garmentId, detail, Date.now() - attemptedAt);
  await pushRecent({
    at: attemptedAt,
    platform: job.platform,
    productId: job.productId,
    state: 'failed',
    reason,
    detail,
    message: explain(reason, subject, job.platform),
    fallback: plan?.fallback ?? null,
  });
  await badge('!');
  await pump();
}

export async function badge(text) {
  try {
    await chrome.action.setBadgeText({ text });
    await chrome.action.setBadgeBackgroundColor({ color: text === '!' ? '#a33' : '#2c3f78' });
  } catch {
    // Badges are cosmetic; never let one fail a job.
  }
}
