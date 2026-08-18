/**
 * The security headers the SSR Worker puts on every rendered document.
 *
 * Pulled out of worker.ts so it can be imported by a test without dragging in the Worker runtime.
 * `public/_headers` carries the same policy for static asset responses and has to be kept in step;
 * `csp-connect-src.spec.ts` checks both.
 */
export const SECURITY_HEADERS: Record<string, string> = {
  'Strict-Transport-Security': 'max-age=31536000; includeSubDomains',
  'X-Frame-Options': 'DENY',
  'X-Content-Type-Options': 'nosniff',
  'Referrer-Policy': 'strict-origin-when-cross-origin',
  'Permissions-Policy': 'camera=(), microphone=(), geolocation=()',
  'Content-Security-Policy':
    "default-src 'self'; " +
    "script-src 'self' 'unsafe-inline' https://www.googletagmanager.com https://*.clarity.ms https://accounts.google.com; " +
    "style-src 'self' 'unsafe-inline' https://accounts.google.com; " +
    "img-src 'self' data: https: blob:; " +
    // eden-relics-tool.fly.dev is the seller tool's own API, on its own origin. Without it here the
    // gated /seller-tool page cannot make a single call: the browser blocks the request before it is
    // sent, so it surfaces as "Failed to fetch" with no network entry and no server-side trace. That
    // is why prod held zero garments while the API, its auth and its CORS were all working —
    // everything had been verified with curl, and curl has no CSP.
    "connect-src 'self' https://api.edenrelics.co.uk https://api-staging.edenrelics.co.uk https://eden-relics-tool.fly.dev https://www.google-analytics.com https://region1.google-analytics.com https://*.clarity.ms https://accounts.google.com; " +
    "font-src 'self'; " +
    "worker-src 'self' blob:; " +
    "frame-src https://accounts.google.com; " +
    "frame-ancestors 'none'",
};
