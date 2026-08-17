/**
 * The platform modules, and the one invariant that binds all of them.
 *
 * **NO MODULE MAY SUBMIT A FORM.** Not a convention — the rule the whole design rests on. Brief §4.1:
 * "Human-paced and user-initiated. Fill the form, let the seller review and press publish. Never
 * silent bulk-blasting." It is simultaneously the honest characterisation of the tool as assistive
 * and the thing that keeps a seller's Vinted account off the automated-enforcement radar, and the
 * risk of getting it wrong lands on them rather than on us.
 *
 * So there is no submit selector anywhere in this directory, no click helper in fill.js, and a test
 * (`test/platforms.test.js`) that reads these files as text and fails if one appears. That test is
 * the actual enforcement; this comment only explains it.
 */

import { vinted } from './vinted.js';
import { depop } from './depop.js';

export const platforms = [vinted, depop];

/** Platform names are typed by hand in two codebases, so matching is case-insensitive. */
export function moduleFor(platform) {
  const wanted = String(platform ?? '').toLowerCase();
  return platforms.find((p) => p.platform.toLowerCase() === wanted) ?? null;
}

/** The module whose listing page this URL is, if any. */
export function moduleForUrl(url) {
  return platforms.find((p) => p.isListingPage(url)) ?? null;
}
