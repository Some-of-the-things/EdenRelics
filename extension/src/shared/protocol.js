/**
 * The vocabulary shared by the background worker, the content scripts and the Eden Relics page.
 *
 * Kept free of any `chrome.*` call on purpose: this module is imported by the service worker, by
 * content scripts and by the Node test suite, and only the first two have an extension API.
 */

/** Messages passed between the extension's own halves. */
export const Message = Object.freeze({
  /** Eden Relics page → background: post this piece to this platform. Always a button press. */
  QueueListing: 'queue-listing',
  /** Eden Relics page → background: here is my session token, connect the extension. */
  Pair: 'pair',
  /** Popup → background: forget the token. */
  Unpair: 'unpair',
  /** Popup → background: what is going on right now? */
  GetStatus: 'get-status',
  /** Content script → background: I am alive on a marketplace page, what do you have for me? */
  ContentReady: 'content-ready',
  /** Background → content script: fill this. */
  FillPlan: 'fill-plan',
  /** Content script → background: here is what happened, good or bad. */
  FillOutcome: 'fill-outcome',
});

/**
 * Why a fill did not happen, as a small closed set.
 *
 * These strings travel to the tool API as the `detail` on an `ExtensionPublishFailed` event and come
 * back out as the "commonest reason" column in the metrics panel. That only tells you what to fix
 * after a marketplace redesign if the values group — so this is a fixed vocabulary, and anything
 * variable (which field broke) is appended after a colon rather than spliced into the reason itself.
 */
export const FailureReason = Object.freeze({
  /** A selector no longer matches. The monthly maintenance tax, and the one worth alerting on. */
  FieldNotFound: 'field-not-found',
  /** We are not on the page we expected — a redesigned flow, or a redirect. */
  PageNotRecognised: 'page-not-recognised',
  /** The form never became interactive. */
  Timeout: 'timeout',
  /** The platform's field mapping has not been researched. We refuse rather than guess. */
  Unresearched: 'mapping-unresearched',
  /** The server says this listing may not be published at all. */
  Blocked: 'listing-blocked',
  /** No marketplace session. We never hold credentials, so only the seller can fix this. */
  NotSignedIn: 'not-signed-in',
  /** The extension was asked for a platform it has no module for. */
  UnknownPlatform: 'unknown-platform',
});

/** The tool API's event kinds this extension is allowed to report. */
export const EventKind = Object.freeze({
  Attempted: 'ExtensionPublishAttempted',
  Succeeded: 'ExtensionPublishSucceeded',
  Failed: 'ExtensionPublishFailed',
});

/** The server's `detail` column is 120 chars. Truncate here so a long reason can't lose the event. */
export const MaxDetailLength = 120;

/**
 * A failure reason plus its subject, e.g. `field-not-found:price`.
 *
 * The subject is deliberately second so that grouping on the leading token still works if someone
 * later groups by prefix rather than by whole string.
 */
export function describeFailure(reason, subject) {
  const text = subject ? `${reason}:${subject}` : reason;
  return text.length <= MaxDetailLength ? text : text.slice(0, MaxDetailLength);
}

/**
 * Pull one platform's plan out of a cross-listing preview.
 *
 * The API serialises its enums kebab-cased (`seller-browser-extension`, `unresearched`), and platform
 * names are compared case-insensitively because they are typed by hand in two codebases.
 */
export function planFor(preview, platform) {
  if (!preview || !Array.isArray(preview.platforms)) {
    return null;
  }
  const wanted = String(platform).toLowerCase();
  return preview.platforms.find((p) => String(p.platform).toLowerCase() === wanted) ?? null;
}

/**
 * Whether we may touch a marketplace form with this plan, and if not, why.
 *
 * Two independent gates, and both have to open:
 *
 *   - the *server* has to say this listing can publish and that it knows the platform's fields;
 *   - the *extension* has to have a researched selector table for that platform.
 *
 * They are separate because they fail at different times. The server's mapping can be documented
 * while our selectors are stale from a redesign last Tuesday, and refusing on either is the same
 * rule the dating engine already follows: something unresearched must never affect output, because a
 * tool that silently fills the wrong fields is worse than one that admits it cannot.
 */
export function gateFill(plan, platformModule) {
  if (!plan) {
    return { allowed: false, reason: FailureReason.UnknownPlatform, subject: null };
  }
  if (plan.research === 'unresearched') {
    return { allowed: false, reason: FailureReason.Unresearched, subject: 'server' };
  }
  if (!plan.validation?.canPublish) {
    const first = plan.validation?.blocking?.[0];
    return { allowed: false, reason: FailureReason.Blocked, subject: first?.field ?? null };
  }
  if (!platformModule) {
    return { allowed: false, reason: FailureReason.UnknownPlatform, subject: null };
  }
  if (platformModule.research === 'unresearched') {
    return { allowed: false, reason: FailureReason.Unresearched, subject: 'selectors' };
  }
  return { allowed: true, reason: null, subject: null };
}
