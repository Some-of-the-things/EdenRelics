import { AngularAppEngine, createRequestHandler } from '@angular/ssr';

const angularApp = new AngularAppEngine();

const SSR_CACHE_TTL = 300; // 5 minutes

const SECURITY_HEADERS: Record<string, string> = {
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
    "connect-src 'self' https://api.edenrelics.co.uk https://www.google-analytics.com https://region1.google-analytics.com https://*.clarity.ms https://accounts.google.com; " +
    "font-src 'self'; " +
    "worker-src 'self' blob:; " +
    "frame-src https://accounts.google.com; " +
    "frame-ancestors 'none'",
};

function withSecurityHeaders(response: Response): Response {
  const secured = new Response(response.body, response);
  for (const [key, value] of Object.entries(SECURITY_HEADERS)) {
    secured.headers.set(key, value);
  }
  return secured;
}

export default {
  async fetch(request: Request, env: { ASSETS: { fetch: typeof fetch } }, ctx: ExecutionContext): Promise<Response> {
    const url = new URL(request.url);

    // Dynamic sitemap — proxy to API for live product/blog data
    if (url.pathname === '/sitemap.xml') {
      try {
        const apiRes = await fetch('https://api.edenrelics.co.uk/api/sitemap.xml');
        if (apiRes.ok) {
          return withSecurityHeaders(new Response(apiRes.body, {
            status: 200,
            headers: { 'Content-Type': 'application/xml', 'Cache-Control': 'public, max-age=3600' },
          }));
        }
      } catch {
        // API unavailable — fall through to static sitemap
      }
    }

    // Serve static assets (files with extensions) via ASSETS binding
    if (url.pathname.includes('.')) {
      try {
        const assetResponse = await env.ASSETS.fetch(request);
        if (assetResponse.ok) {
          return withSecurityHeaders(assetResponse);
        }
      } catch {
        // Asset not found — return 404 instead of crashing
      }
      return withSecurityHeaders(new Response('Not Found', { status: 404 }));
    }

    // Try Angular SSR for all routes
    try {
      const response = await angularApp.handle(request);

      if (response) {
        const cached = new Response(response.body, response);
        cached.headers.set('Cache-Control', 'public, max-age=60, must-revalidate');
        return withSecurityHeaders(cached);
      }
    } catch {
      // SSR failed, fall through to CSR shell
    }

    // Fallback: serve the CSR shell for client-rendered or failed routes
    const fallback = await env.ASSETS.fetch(new Request(new URL('/index.csr.html', request.url), request));
    return withSecurityHeaders(fallback);
  },
};

/**
 * Request handler used by the Angular CLI (dev-server and build).
 */
export const reqHandler = createRequestHandler(async (req) => {
  return angularApp.handle(req) ?? new Response('Not Found', { status: 404 });
});
