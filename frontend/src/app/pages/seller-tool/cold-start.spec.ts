import { HttpErrorResponse } from '@angular/common/http';
import { isColdStart } from './cold-start';

/**
 * The tool's machines suspend when idle, so its first request of the day fails. Retrying that is
 * worth doing; retrying an authorisation failure is not, and the difference is the whole point of
 * this predicate.
 */
describe('isColdStart', () => {
  const response = (status: number) => new HttpErrorResponse({ status });

  it('treats a request that never got an answer as a cold start', () => {
    // status 0 is what a suspended machine actually produces: no HTTP response at all.
    expect(isColdStart(response(0))).toBe(true);
  });

  for (const status of [408, 502, 503, 504]) {
    it(`treats ${status} as a cold start`, () => {
      expect(isColdStart(response(status))).toBe(true);
    });
  }

  for (const status of [400, 401, 403, 404, 500]) {
    it(`does not retry ${status}`, () => {
      // These say the same thing in two seconds. Retrying delays a true answer, and on 401/403 it
      // hides the one thing the seller can actually act on: that they need to sign in.
      expect(isColdStart(response(status))).toBe(false);
    });
  }

  it('ignores anything that is not an HTTP failure', () => {
    expect(isColdStart(new Error('boom'))).toBe(false);
    expect(isColdStart(null)).toBe(false);
    expect(isColdStart(undefined)).toBe(false);
  });
});
