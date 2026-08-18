/**
 * What we tell the seller when something doesn't happen.
 *
 * Shared between the background worker (which writes the popup's history) and the content script
 * (which writes the overlay on the page) so that the same failure never gets two descriptions. Kept
 * out of both so it can be read on its own: this is the file where the tool's honesty about its own
 * limits actually lives, and it should be reviewable without reading any plumbing.
 *
 * Two rules run through every string here. Say what did *not* happen, explicitly — "nothing was
 * filled in" — because the failure the brief warns about is a seller believing something went live
 * when it didn't. And where the seller can act, say what to do; where they can't, don't pretend.
 */

import { FailureReason } from './protocol.js';

export function explain(reason, subject, platform) {
  switch (reason) {
    case FailureReason.Unresearched:
      return subject === 'selectors'
        ? `We haven't mapped ${platform}'s form yet, so we won't guess at it. The listing text is here to paste in.`
        : `${platform}'s field requirements aren't recorded yet, so nothing can be posted there. The listing text is here to paste in.`;
    case FailureReason.Blocked:
      return `This piece isn't ready to publish${subject ? ` — check ${subject}` : ''}. Nothing was filled in.`;
    case FailureReason.FieldNotFound:
      return `${platform} has changed its form and we couldn't find the ${subject ?? 'right'} field. Nothing was filled in.`;
    case FailureReason.NotSignedIn:
      return `You're not signed in to ${platform}, so nothing was filled in. Sign in and try again — we never hold your ${platform} password.`;
    case FailureReason.PageNotRecognised:
      return `This isn't the ${platform} listing page we expected. Nothing was filled in.`;
    case FailureReason.Timeout:
      return `${platform}'s form didn't finish loading. Nothing was filled in.`;
    case FailureReason.UnknownPlatform:
      return `This extension doesn't handle ${platform}, so nothing was filled in.`;
    default:
      return `Couldn't post to ${platform}. Nothing was filled in.`;
  }
}
