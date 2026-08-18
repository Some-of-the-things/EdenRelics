import { HttpErrorResponse } from '@angular/common/http';

/**
 * Whether a failed request looks like the seller tool still waking up.
 *
 * The tool runs on Fly machines that suspend when idle, so the first request after a quiet spell
 * lands on a machine that is still starting and fails outright. It is indistinguishable, on screen,
 * from the tool being broken — which matters, because the same page showed a genuine "cannot reach
 * the tool" error for a month and taught its only user to believe it.
 *
 * Deliberately narrow. A 401 or 403 will say exactly the same thing in two seconds' time, so
 * retrying one only delays a true answer and hides the fact that the seller needs to sign in. Only
 * failures that a running server would not have produced are worth waiting on:
 *
 *   status 0    the request never got an HTTP answer — connection refused, DNS, aborted
 *   408 / 504   a timeout, at our end or a proxy's
 *   502 / 503   a proxy that has no healthy machine to talk to yet
 */
export function isColdStart(error: unknown): boolean {
  if (!(error instanceof HttpErrorResponse)) {
    return false;
  }
  return error.status === 0 || error.status === 408 || error.status === 502
    || error.status === 503 || error.status === 504;
}
