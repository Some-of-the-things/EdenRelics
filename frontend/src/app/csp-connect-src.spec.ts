import { SECURITY_HEADERS } from '../security-headers';
import { environment as staging } from '../environments/environment.staging';

/**
 * Every deployed API origin the app calls must be in the CSP `connect-src`.
 *
 * This exists because one wasn't. `toolApiUrl` points at the seller tool on its own origin, and
 * that origin was missing — so the browser blocked every request from the gated /seller-tool page
 * before it left the tab. It surfaces as "Failed to fetch" with no network entry, no server log and
 * no CORS error, which is about as quiet as a failure gets: the API, its auth and its CORS were all
 * verified working with curl, and curl has no CSP. Prod held zero garments for a month as a result.
 *
 * Staging's origins are read from its environment file, so adding an API there fails this test
 * until the policy is updated. Production's are asserted literally: `ng test` replaces
 * `environment.ts` with the development one, so importing it here would assert that
 * `http://localhost:5260` belongs in the production policy, which it emphatically does not.
 */
describe('CSP connect-src', () => {
  const connectSrc = SECURITY_HEADERS['Content-Security-Policy']
    .split(';')
    .map((directive) => directive.trim())
    .find((directive) => directive.startsWith('connect-src'));

  /** Deployed origins only — localhost is served by the dev server, which sets its own policy. */
  const stagingOrigins = [
    ...new Set([new URL(staging.apiUrl).origin, new URL(staging.toolApiUrl).origin]),
  ].filter((origin) => !origin.includes('localhost'));

  const productionOrigins = ['https://api.edenrelics.co.uk', 'https://eden-relics-tool.fly.dev'];

  it('declares a connect-src at all', () => {
    // Without one it falls back to default-src 'self', which blocks every API call on the site.
    expect(connectSrc).toBeDefined();
  });

  for (const origin of [...new Set([...productionOrigins, ...stagingOrigins])]) {
    it(`allows ${origin}`, () => {
      expect(connectSrc).toContain(origin);
    });
  }

  it('covers the seller tool, which is on an origin of its own', () => {
    // Called out separately because it is the one that is easy to forget: it is not an
    // *.edenrelics.co.uk host, so it does not look like part of the site.
    expect(connectSrc).toContain('https://eden-relics-tool.fly.dev');
  });
});
