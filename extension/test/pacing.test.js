import test from 'node:test';
import assert from 'node:assert/strict';

import { Jitter, MaxActionsPerHour, Pace, RateLimiter } from '../src/shared/pacing.js';

test('a jittered delay stays inside its range', () => {
  const jitter = new Jitter(() => 0);
  assert.equal(jitter.next([1000, 2000]), 1000);

  const top = new Jitter(() => 1);
  assert.equal(top.next([1000, 2000]), 2000);
});

test('a repeated delay is redrawn, so the sequence is not machine-regular', () => {
  // A generator that would otherwise return the same value forever — the exact shape of a machine
  // signature. The first draw stands; the second is inside the decorrelation band and is redrawn.
  const draws = [0.5, 0.5, 0.5, 0.9];
  let index = 0;
  const jitter = new Jitter(() => draws[Math.min(index++, draws.length - 1)]);

  const first = jitter.next([1000, 2000]);
  const second = jitter.next([1000, 2000]);

  assert.equal(first, 1500);
  assert.notEqual(second, first);
  assert.ok(Math.abs(second - first) >= first * Jitter.DecorrelationBand);
});

test('redrawing gives up rather than looping forever on a narrow range', () => {
  // Every draw from a one-value range is a repeat. A delay that never resolved would be a far worse
  // bug than a repetitive one, so the redraw is bounded and the value is accepted.
  const jitter = new Jitter(() => 0.5);
  const first = jitter.next([1000, 1000]);
  const second = jitter.next([1000, 1000]);
  assert.equal(first, 1000);
  assert.equal(second, 1000);
});

test('the between-listings pace is long enough to read as a person', () => {
  const [min] = Pace.BetweenListingsMs;
  assert.ok(min >= 30_000, 'a gap under half a minute is bulk posting by another name');
});

test('the rate limiter counts inside a rolling hour', () => {
  let clock = 0;
  const limiter = new RateLimiter(3, () => clock);

  limiter.record();
  limiter.record();
  limiter.record();
  assert.equal(limiter.allows(), false);

  // Just under the hour: still counted.
  clock += 59 * 60_000;
  assert.equal(limiter.allows(), false);

  // Past it: the window has rolled and the oldest actions no longer count.
  clock += 2 * 60_000;
  assert.equal(limiter.allows(), true);
});

test('retryAfterMs says when the window next opens', () => {
  let clock = 0;
  const limiter = new RateLimiter(1, () => clock);
  limiter.record();

  clock += 10 * 60_000;
  assert.equal(limiter.retryAfterMs(), 50 * 60_000);
});

test('retryAfterMs is zero when the limiter already allows an action', () => {
  const limiter = new RateLimiter(2, () => 0);
  limiter.record();
  assert.equal(limiter.retryAfterMs(), 0);
});

test('the limiter survives a service-worker restart', () => {
  // MV3 kills the worker between events. A rate limiter that forgot itself on restart would be no
  // rate limiter at all — the seller would simply need to wait long enough for Chrome to reap it.
  let clock = 5 * 60_000;
  const before = new RateLimiter(2, () => clock);
  before.record();
  before.record();
  const saved = before.serialise();

  const after = new RateLimiter(2, () => clock);
  after.hydrate(saved);
  assert.equal(after.allows(), false);
});

test('hydrate drops stamps that have aged out and ignores rubbish', () => {
  const clock = 10 * 60 * 60_000;
  const limiter = new RateLimiter(2, () => clock);
  limiter.hydrate([1, 2, null, 'x', clock - 1000]);
  assert.equal(limiter.serialise().length, 1);
});

test('the per-hour ceiling is a backstop, not a throughput target', () => {
  // Nothing in the UI can queue this much: posting is one button press per piece. Hitting it means
  // something has gone wrong, and stopping is the right answer.
  assert.ok(MaxActionsPerHour <= 30);
});
