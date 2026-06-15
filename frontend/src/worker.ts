import { AngularAppEngine, createRequestHandler } from '@angular/ssr';

const angularApp = new AngularAppEngine();

const SSR_CACHE_TTL = 300; // 5 minutes

interface WorkerEnv {
  ASSETS: { fetch: typeof fetch };
  /** Shared secret for the first-party analytics beacon. When unset, beaconing is off. */
  ANALYTICS_INGEST_SECRET?: string;
  /** Backend API base; defaults to production. */
  API_BASE?: string;
}

const DEFAULT_API_BASE = 'https://api.edenrelics.co.uk';

/**
 * First-party, cookieless page-view beacon. Fired server-to-server (Worker → backend)
 * once per successful SSR navigation render — no client JS, no cookies, 100% of renders.
 * Cloudflare's request.cf gives us country + network org for free (used for geo + bot
 * heuristics on the backend). Best-effort and non-blocking via ctx.waitUntil; failures
 * never affect the response. No-op until ANALYTICS_INGEST_SECRET is configured.
 */
function sendPageViewBeacon(
  request: Request,
  env: WorkerEnv,
  ctx: ExecutionContext,
  pathname: string,
): void {
  const secret = env.ANALYTICS_INGEST_SECRET;
  if (!secret || request.method !== 'GET') {
    return;
  }

  const cf = (request as { cf?: { country?: string; asOrganization?: string } }).cf;
  const apiBase = env.API_BASE ?? DEFAULT_API_BASE;

  ctx.waitUntil(
    fetch(`${apiBase}/api/analytics/pageview`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Analytics-Secret': secret,
      },
      body: JSON.stringify({
        path: pathname,
        country: cf?.country ?? null,
        userAgent: request.headers.get('User-Agent'),
        asOrganization: cf?.asOrganization ?? null,
      }),
    }).catch(() => {
      // Analytics is best-effort; swallow errors so a beacon never breaks a page render.
    }),
  );
}

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
    "connect-src 'self' https://api.edenrelics.co.uk https://api-staging.edenrelics.co.uk https://www.google-analytics.com https://region1.google-analytics.com https://*.clarity.ms https://accounts.google.com; " +
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
  async fetch(request: Request, env: WorkerEnv, ctx: ExecutionContext): Promise<Response> {
    const url = new URL(request.url);

    // Dynamic sitemap — proxy to API for live product/blog data
    if (url.pathname === '/sitemap.xml') {
      try {
        const apiRes = await fetch('https://api.edenrelics.co.uk/api/sitemap.xml');
        return withSecurityHeaders(new Response(apiRes.body, {
          status: apiRes.status,
          headers: { 'Content-Type': 'application/xml', 'Cache-Control': 'public, max-age=3600' },
        }));
      } catch {
        return withSecurityHeaders(new Response('Sitemap temporarily unavailable', { status: 503 }));
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
        const propagated = new Response(response.body, response);
        if (response.status >= 200 && response.status < 300) {
          propagated.headers.set('Cache-Control', 'public, max-age=60, must-revalidate');
          // Count this render in our first-party analytics (cookieless, non-blocking).
          sendPageViewBeacon(request, env, ctx, url.pathname);
        } else {
          propagated.headers.set('Cache-Control', 'no-store');
        }
        return withSecurityHeaders(propagated);
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
