/**
 * Human pacing: how long the extension waits before doing anything.
 *
 * Brief §4 is unusually blunt about why this exists. Vinted's enforcement is automated, harsh and
 * effectively unappealable, suspensions come from *cumulative* risk signals rather than one
 * violation, and third-party extensions are known to contribute to those signals. The consequence
 * lands on the seller's account, not ours. So every delay here is jittered and none of them are
 * machine-regular — rule 4 of the design rules, and the reason this is its own module with its own
 * tests rather than a scattering of setTimeout calls.
 *
 * Pure by design (no `chrome.*`, no wall-clock reads except through an injected `now`) so the
 * behaviour can actually be tested rather than asserted in a comment.
 */

/**
 * Delay ranges in milliseconds.
 *
 * Between fields is roughly the time it takes to look at a form and type into the next box. Between
 * listings is deliberately long — the tool is assistive, not a bulk poster, and a seller who wants
 * to list ten things quickly is exactly the pattern the rule above is aimed at.
 */
export const Pace = Object.freeze({
  BetweenFieldsMs: Object.freeze([280, 1400]),
  BetweenListingsMs: Object.freeze([45_000, 180_000]),
  /** How often we look for new sales while the browser happens to be open. */
  SaleCheckMs: Object.freeze([7 * 60_000, 23 * 60_000]),
});

/**
 * The ceiling, per platform, per rolling hour.
 *
 * Not a throughput target — a backstop. Nothing in the UI can queue this much work, because posting
 * is one button press per piece, so hitting this limit means something is wrong and stopping is the
 * right answer.
 */
export const MaxActionsPerHour = 20;

/**
 * A jittered delay that avoids repeating itself.
 *
 * A uniform random draw is already not machine-regular, but a run of similar draws still reads as
 * one, and the cheapest way to break that is to refuse a value too close to the one before. The
 * retry is bounded because on a narrow range every draw may be "too close" and a delay that never
 * resolves would be a far worse bug than a slightly repetitive one.
 */
export class Jitter {
  /** @param {() => number} rng Injected for tests; the real one is Math.random. */
  constructor(rng = Math.random) {
    this.rng = rng;
    this.previous = null;
  }

  /** Fraction of the previous delay within which a new draw counts as a repeat. */
  static get DecorrelationBand() {
    return 0.05;
  }

  static get MaxRedraws() {
    return 4;
  }

  /** @param {readonly [number, number]} range */
  next([min, max]) {
    let value = this.#draw(min, max);
    for (let i = 0; i < Jitter.MaxRedraws && this.#tooClose(value); i += 1) {
      value = this.#draw(min, max);
    }
    this.previous = value;
    return value;
  }

  #draw(min, max) {
    return Math.round(min + this.rng() * (max - min));
  }

  #tooClose(value) {
    if (this.previous === null) {
      return false;
    }
    return Math.abs(value - this.previous) < this.previous * Jitter.DecorrelationBand;
  }
}

/**
 * A rolling-window rate limit, one instance per platform.
 *
 * Per platform rather than global because the platforms watch separately: a busy Depop session
 * should not spend Vinted's allowance, and Vinted's is the one that carries the account risk.
 */
export class RateLimiter {
  /**
   * @param {number} maxPerHour
   * @param {() => number} now Injected clock, in ms.
   */
  constructor(maxPerHour = MaxActionsPerHour, now = () => Date.now()) {
    this.maxPerHour = maxPerHour;
    this.now = now;
    /** @type {number[]} */
    this.stamps = [];
  }

  static get WindowMs() {
    return 60 * 60_000;
  }

  #prune() {
    const cutoff = this.now() - RateLimiter.WindowMs;
    this.stamps = this.stamps.filter((t) => t > cutoff);
  }

  allows() {
    this.#prune();
    return this.stamps.length < this.maxPerHour;
  }

  record() {
    this.#prune();
    this.stamps.push(this.now());
  }

  /** How long until the limit would allow another action; 0 when it already does. */
  retryAfterMs() {
    this.#prune();
    if (this.stamps.length < this.maxPerHour) {
      return 0;
    }
    const oldest = Math.min(...this.stamps);
    return Math.max(0, oldest + RateLimiter.WindowMs - this.now());
  }

  /** Restores counts across a service-worker restart, which happens constantly in MV3. */
  hydrate(stamps) {
    this.stamps = Array.isArray(stamps) ? stamps.filter((t) => Number.isFinite(t)) : [];
    this.#prune();
  }

  serialise() {
    this.#prune();
    return [...this.stamps];
  }
}

/** Promise-based sleep, with the timer injectable so tests don't wait in real time. */
export function sleep(ms, timer = setTimeout) {
  return new Promise((resolve) => timer(resolve, ms));
}
