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

/** Hard ceiling on any origin subrequest so a slow/hung API can never stall the Worker. */
const API_FETCH_TIMEOUT_MS = 8000;

/** fetch() with an AbortController deadline — aborts (throws) once `ms` elapses. */
async function fetchWithTimeout(
  input: RequestInfo | URL,
  ms: number = API_FETCH_TIMEOUT_MS,
  init?: RequestInit,
): Promise<Response> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), ms);
  try {
    return await fetch(input, { ...init, signal: controller.signal });
  } finally {
    clearTimeout(timer);
  }
}

/** Routes we must never edge-cache: auth-gated, personalised, state-changing, or draft previews. */
const NO_CACHE_PREFIXES = [
  '/admin',
  '/account',
  '/settings',
  '/checkout',
  '/basket',
  '/cart',
  '/orders',
  '/order-confirmation',
  '/review',
  '/wishlist',
  '/login',
  '/register',
  '/forgot-password',
  '/reset-password',
  '/verify-email',
  // Admin-only draft previews of unpublished content — must never be cached/served publicly.
  '/blog/preview',
  '/collections/preview',
];

/**
 * Long edge TTL for pages whose primary content has no live-inventory dependency:
 * blog posts, designer/style/garment hubs, care guides, and static/legal pages.
 * Everything else (home, /shop listings, /product, /collections) stays on the
 * short TTL because those reflect the live catalogue (an add/sale changes them).
 * Once purge-on-inventory-change lands, those can move here too.
 */
const SSR_STATIC_CACHE_TTL = 3600;
/**
 * Product detail pages: 30 min. Safe without purge-on-change because a SOLD item
 * is 301'd by the /product/ redirect layer that runs on every request BEFORE the
 * cache — so the only staleness is a rare admin edit to a still-live one-of-one.
 * Listing pages (home/shop/collections) stay short so new/sold items surface fast.
 */
const SSR_PRODUCT_CACHE_TTL = 1800;
const STATIC_PATH_PREFIXES = [
  '/blog',
  '/designers',
  '/style',
  '/dresses',
  '/care',
  '/about',
  '/contact',
  '/privacy-policy',
  '/returns-policy',
  '/security',
  '/terms-conditions',
  '/cookie-policy',
  '/accessibility-report',
  '/compliance-report',
  '/modern-slavery-policy',
  '/supply-chain-policy',
];

/** Edge TTL (seconds) for a cacheable page: long for static content, short otherwise. */
function edgeCacheTtl(url: URL): number {
  const path = url.pathname;
  const isStatic = STATIC_PATH_PREFIXES.some(
    (prefix) => path === prefix || path.startsWith(`${prefix}/`),
  );
  if (isStatic) {
    return SSR_STATIC_CACHE_TTL;
  }
  if (path.startsWith('/product/')) {
    return SSR_PRODUCT_CACHE_TTL;
  }
  return SSR_CACHE_TTL;
}

/**
 * True for anonymous GET navigations whose SSR HTML is identical for every
 * visitor. Auth is a JWT in localStorage attached client-side, so the server
 * render never sees a user — every render of a given URL is the same, which is
 * what makes edge-caching it safe (no per-user leak).
 */
function isCacheablePageRequest(request: Request, url: URL): boolean {
  if (request.method !== 'GET') {
    return false;
  }
  if (request.headers.has('Authorization')) {
    return false;
  }
  return !NO_CACHE_PREFIXES.some(
    (prefix) => url.pathname === prefix || url.pathname.startsWith(`${prefix}/`),
  );
}

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
    const resp = await fetchWithTimeout(`${apiBase}/api/products/resolve/${encodeURIComponent(slug)}`);
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
        const apiRes = await fetchWithTimeout('https://api.edenrelics.co.uk/api/sitemap.xml');
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
        const apiRes = await fetchWithTimeout('https://api.edenrelics.co.uk/api/merchant-feed.xml');
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

    // Anonymous page renders are identical for every visitor (auth lives in
    // localStorage, unavailable during SSR), so we edge-cache them via the Cache
    // API. A cache hit skips the Angular render AND its API fan-out entirely —
    // this is what stops a crawl burst from stacking full renders onto the
    // shared-CPU API and shedding 503s. Merely setting s-maxage would NOT do this:
    // the Worker re-runs every request, so we must read/write the cache ourselves.
    // `caches.default` is Cloudflare's per-colo cache; cast because the ambient
    // DOM `CacheStorage` type doesn't declare it.
    const cache = (caches as unknown as { default: Cache }).default;
    const cacheable = isCacheablePageRequest(request, url);
    if (cacheable) {
      const cached = await cache.match(request);
      if (cached) {
        // Still count the view; only the expensive render was skipped.
        sendPageViewBeacon(request, env, ctx, url.pathname);
        return withSecurityHeaders(cached);
      }
    }

    // Try Angular SSR for all routes. We fully BUFFER the rendered body inside
    // this try/catch (via arrayBuffer) rather than piping `response.body`
    // straight through. @angular/ssr can resolve the Response *before* the render
    // has finished and then throw while the body streams — a failure that a bare
    // `new Response(response.body, …)` lets escape as an uncaught 500. Observed in
    // prod: an intermittent NG0200 during render → `undefined.fetch` → hard 500 on
    // ~8% of cache-miss renders, which is exactly what Googlebot hits crawling
    // unique URLs (cache misses) and what showed up as GSC "server connectivity"
    // failures. Awaiting the buffer here pulls any such error into the catch so
    // every render degrades to the CSR shell instead of a 500.
    try {
      const response = await angularApp.handle(request);

      if (response) {
        if (response.status >= 200 && response.status < 300) {
          const body = await response.arrayBuffer();
          const propagated = new Response(body, response);
          const ttl = edgeCacheTtl(url);
          propagated.headers.set(
            'Cache-Control',
            `public, max-age=60, s-maxage=${ttl}, stale-while-revalidate=600`,
          );
          // Count this render in our first-party analytics (cookieless, non-blocking).
          sendPageViewBeacon(request, env, ctx, url.pathname);
          if (cacheable) {
            // Store a copy at the edge for SSR_CACHE_TTL seconds (non-blocking).
            ctx.waitUntil(cache.put(request, propagated.clone()));
          }
          return withSecurityHeaders(propagated);
        }
        if (response.status < 500) {
          // Redirects and genuine 404s are legitimate — propagate as-is, uncached.
          const body = await response.arrayBuffer();
          const propagated = new Response(body, response);
          propagated.headers.set('Cache-Control', 'no-store');
          return withSecurityHeaders(propagated);
        }
        // 5xx: a transient API/SSR failure. Fall through to the CSR shell below so
        // the visitor gets a working client-rendered page instead of a hard error.
      }
    } catch {
      // SSR failed — including a mid-render throw surfaced by buffering above.
      // Fall through to the CSR shell.
    }

    // Fallback: serve the CSR shell for client-rendered, failed, or 5xx routes.
    // Build a FRESH GET request for the asset rather than cloning the original
    // `request` — by this point it has already been passed to
    // `angularApp.handle()`, which consumes/locks it, so `new Request(url,
    // request)` throws intermittently (this was surfacing as spurious 503s on the
    // SSR-failure path). A clean request depends on nothing but the shell URL.
    const shellUrl = new URL('/index.csr.html', request.url);
    try {
      const fallback = await env.ASSETS.fetch(new Request(shellUrl, { method: 'GET' }));
      if (fallback.ok) {
        // Serve the real CSR shell (a 200 HTML doc that boots the client app),
        // uncached so a transient failure isn't pinned at the edge.
        const propagated = new Response(fallback.body, fallback);
        propagated.headers.set('Cache-Control', 'no-store');
        return withSecurityHeaders(propagated);
      }
      // Asset unexpectedly not ok — fall through to the last-resort response.
    } catch {
      // env.ASSETS.fetch threw — fall through to the last-resort response.
    }
    // Last resort: a retryable 503 (never a hard 500) if even the shell is
    // unavailable. Googlebot treats 503 as "try again", not a broken page.
    return withSecurityHeaders(
      new Response('Service temporarily unavailable — please retry.', {
        status: 503,
        headers: { 'Content-Type': 'text/plain; charset=utf-8', 'Cache-Control': 'no-store' },
      }),
    );
  },
};

/**
 * Request handler used by the Angular CLI (dev-server and build).
 */
export const reqHandler = createRequestHandler(async (req) => {
  return angularApp.handle(req) ?? new Response('Not Found', { status: 404 });
});
