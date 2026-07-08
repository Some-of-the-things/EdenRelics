import { AngularAppEngine, createRequestHandler } from '@angular/ssr';
import { findDesignerForProduct } from './app/pages/designers/designers.data';

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

/** Durable owner opt-out cookie. Set via /?mute-analytics; read here to skip beaconing. */
const MUTE_COOKIE = 'er_mute';

/**
 * Referrers we never want counted as prospective customers: the owner's own
 * sites / preview hosts and the staging Access gate (that's us reviewing the
 * site), plus directory / lead-gen crawlers that show up as referral spam.
 * Only the entry navigation carries an external Referer — the durable mute
 * cookie covers an owner's subsequent same-site clicks and direct visits.
 */
const EXCLUDED_REFERRER_HOSTS = ['petercarter.co.uk', 'bizify.com'];
const EXCLUDED_REFERRER_SUFFIXES = ['.cloudflareaccess.com', '.netlify.app'];

/** True when the request carries the owner opt-out cookie. */
function hasMuteCookie(request: Request): boolean {
  const cookie = request.headers.get('Cookie');
  return cookie != null && /(?:^|;\s*)er_mute=1(?:;|$)/.test(cookie);
}

/** True when the Referer host is one we deliberately keep out of the human counts. */
function isExcludedReferrer(request: Request): boolean {
  const referer = request.headers.get('Referer');
  if (!referer) {
    return false;
  }
  let host: string;
  try {
    host = new URL(referer).hostname.toLowerCase();
  } catch {
    return false;
  }
  return (
    EXCLUDED_REFERRER_HOSTS.includes(host) ||
    EXCLUDED_REFERRER_SUFFIXES.some((suffix) => host.endsWith(suffix))
  );
}

/**
 * First-party, cookieless page-view beacon. Fired server-to-server (Worker → backend)
 * once per successful SSR navigation render — no client JS, no cookies, 100% of renders.
 * Cloudflare's request.cf gives us country + network org for free (used for geo + bot
 * heuristics on the backend). Best-effort and non-blocking via ctx.waitUntil; failures
 * never affect the response. No-op until ANALYTICS_INGEST_SECRET is configured.
 *
 * Owner / internal traffic is excluded so the human counts approximate real
 * prospective customers: the durable mute cookie drops the owner's own browsing,
 * and a small referrer list drops self-referrals and directory-crawler spam.
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
  if (hasMuteCookie(request) || isExcludedReferrer(request)) {
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

/**
 * Product-URL redirect layer — the "no dead 404s" rule. A sold piece's page stays
 * live for a grace window (enforced by the backend), then this 301s it to a
 * relevant page instead of letting the URL dead-end; soft-deleted pieces and known
 * legacy/renamed URLs 301 too. Genuinely unknown URLs fall through to the app's
 * 404. Target: the piece's designer hub, else its decade shop page, else /shop.
 */
const VALID_DECADES = new Set(['1950s', '1960s', '1970s', '1980s', '1990s']);

/** Known legacy/renamed product URLs still indexed by search engines. */
const LEGACY_REDIRECTS: Record<string, string> = {
  // ER-00008 was renamed off this early placeholder slug.
  'velvet-mini-dress': '/product/1980s-90s-st-michael-tartan-wool-pencil-skirt-jewel-tones',
  // ER-00002…00010 were seeded with placeholder slugs that described the wrong
  // garment entirely; renamed to descriptive, keyword-accurate slugs, with the
  // old (indexed) URLs 301'd here so their search authority carries over.
  'wrap-dress': '/product/1970s-80s-martha-s-miniatures-prairie-dress-scarlet-red',
  'power-shoulder-dress': '/product/late-1970s-prairie-dress-indigo-patchwork',
  'sequin-party-dress': '/product/1980s-laura-ashley-prairie-midi-dress-burgundy-floral',
  'grunge-babydoll-dress': '/product/1970s-paganne-by-gene-berk-printed-dress-red',
  'asymmetric-midi-dress': '/product/1990s-c-a-floral-cotton-smock-dress-navy',
  'cut-out-maxi-dress': '/product/1970s-algo-ettes-striped-midi-dress-jewel-tones',
  // Legacy numeric id from the pre-slug catalogue.
  '10': '/shop',
};

function redirectTargetFor(name?: string, era?: string): string {
  const designer = name ? findDesignerForProduct(name) : undefined;
  if (designer) {
    return `/designers/${designer.slug}`;
  }
  if (era && VALID_DECADES.has(era)) {
    return `/shop/${era}`;
  }
  return '/shop';
}

/**
 * Decide whether a /product/{slug} URL should 301, and to where. Returns null to
 * let the request render normally (live piece, sold within grace, or unknown —
 * which the app then 404s). Fails open (null) on any network/parse error.
 */
async function resolveProductRedirect(pathname: string, env: WorkerEnv): Promise<string | null> {
  const raw = pathname.slice('/product/'.length);
  if (!raw || raw.includes('/')) {
    return null;
  }
  let slug: string;
  try {
    slug = decodeURIComponent(raw);
  } catch {
    return null;
  }
  const legacy = LEGACY_REDIRECTS[slug];
  if (legacy) {
    return legacy;
  }
  try {
    const apiBase = env.API_BASE ?? DEFAULT_API_BASE;
    const resp = await fetch(`${apiBase}/api/products/resolve/${encodeURIComponent(slug)}`);
    if (!resp.ok) {
      return null;
    }
    const data = (await resp.json()) as { action?: string; name?: string; era?: string };
    if (data.action === 'redirect') {
      return redirectTargetFor(data.name, data.era);
    }
  } catch {
    // Fail open — never let the redirect check break a page render.
  }
  return null;
}

export default {
  async fetch(request: Request, env: WorkerEnv, ctx: ExecutionContext): Promise<Response> {
    const url = new URL(request.url);

    // Owner analytics opt-out toggle. Visiting /?mute-analytics sets a durable
    // cookie so the owner's own browsing stops inflating the first-party human
    // counts; /?mute-analytics=off clears it. Redirect to a clean URL and never
    // cache this response (the Set-Cookie must not be shared between visitors).
    if (url.searchParams.has('mute-analytics')) {
      const turnOff = url.searchParams.get('mute-analytics') === 'off';
      const cookie = turnOff
        ? `${MUTE_COOKIE}=; Max-Age=0; Path=/; Secure; HttpOnly; SameSite=Lax`
        : `${MUTE_COOKIE}=1; Max-Age=157680000; Path=/; Secure; HttpOnly; SameSite=Lax`;
      return withSecurityHeaders(
        new Response(null, {
          status: 302,
          headers: { Location: '/', 'Set-Cookie': cookie, 'Cache-Control': 'no-store' },
        }),
      );
    }

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

    // Google Merchant Center product feed — proxy to API for live product data
    if (url.pathname === '/merchant-feed.xml') {
      try {
        const apiRes = await fetch('https://api.edenrelics.co.uk/api/merchant-feed.xml');
        return withSecurityHeaders(new Response(apiRes.body, {
          status: apiRes.status,
          headers: { 'Content-Type': 'application/xml', 'Cache-Control': 'public, max-age=3600' },
        }));
      } catch {
        return withSecurityHeaders(new Response('Feed temporarily unavailable', { status: 503 }));
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

    // Product-URL redirect layer — 301 sold-past-grace / soft-deleted / legacy
    // product URLs to a relevant page so they never dead-end (see resolveProductRedirect).
    if (request.method === 'GET' && url.pathname.startsWith('/product/')) {
      const target = await resolveProductRedirect(url.pathname, env);
      if (target) {
        return withSecurityHeaders(
          new Response(null, {
            status: 301,
            headers: { Location: target, 'Cache-Control': 'public, max-age=3600' },
          }),
        );
      }
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
