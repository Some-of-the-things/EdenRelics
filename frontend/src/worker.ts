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

/**
 * Where the client-rendered shell lives, in the order we ask for it. Both forms
 * resolve through the ASSETS binding (verified against the real edge binding
 * with `wrangler dev --remote`); the extensionless one is listed first because
 * that is what a public request to `/index.csr.html` is 307'd to, so it is the
 * form least dependent on `html_handling` staying at its default.
 *
 * NB the reason this fallback never fired in production was not the path — it
 * was that `[assets]` in wrangler.toml declared no `binding`, so `env.ASSETS`
 * was `undefined` and every call here threw. See the note in wrangler.toml.
 */
const CSR_SHELL_PATHS = ['/index.csr', '/index.csr.html'];

/**
 * Record why a render failed.
 *
 * This Worker deliberately degrades instead of throwing, which until now meant
 * every SSR failure was invisible: the catch blocks below swallowed the error, so
 * Cloudflare's retained logs held only its own `GET <url>` request line with no
 * exception attached. Reading those logs for the 2026-07-30 05:30Z incident
 * showed the isolate-poisoning signature (one expensive failure, then a stuck
 * isolate failing in a flat 10 ms of CPU) but could not name the first error,
 * because our own code had already discarded it.
 *
 * Kept cheap and total on purpose. A poisoned isolate is killed within ~10 ms of
 * CPU, so this must not do real work; and it must never throw, or the diagnostic
 * would replace the very failure it exists to explain.
 */
function logRenderFailure(stage: string, url: string, error: unknown): void {
  try {
    const err = error as { name?: string; message?: string; stack?: string } | undefined;
    console.error(
      JSON.stringify({
        ssrFailure: stage,
        url,
        name: err?.name ?? typeof error,
        message: err?.message ?? String(error),
        stack: err?.stack?.slice(0, 2000),
      }),
    );
  } catch {
    // Diagnostics must never mask the original failure.
  }
}

/** Fetch the CSR shell asset, trying each candidate path. Null if none served one. */
async function fetchCsrShell(request: Request, env: WorkerEnv): Promise<Response | null> {
  for (const path of CSR_SHELL_PATHS) {
    // Build a FRESH GET request rather than reusing `request` — by the time we
    // get here it has already been passed to `angularApp.handle()`, which
    // consumes/locks it, so `new Request(url, request)` throws intermittently.
    const shellUrl = new URL(path, request.url);
    try {
      const shell = await env.ASSETS.fetch(new Request(shellUrl, { method: 'GET' }));
      if (shell.ok) {
        return shell;
      }
    } catch (error) {
      logRenderFailure('csr-shell', shellUrl.href, error);
      // Try the next candidate.
    }
  }
  return null;
}

/**
 * Run one Angular SSR pass and buffer the result. Returns null when the render
 * failed — it threw, produced nothing, or produced a 5xx — so the caller can
 * retry or fall back. Redirects and genuine 404s come back as-is; they are
 * legitimate answers, not failures.
 *
 * The body is fully BUFFERED inside the try/catch (via arrayBuffer) rather than
 * piping `response.body` straight through. @angular/ssr can resolve the Response
 * *before* the render has finished and then throw while the body streams — a
 * failure that a bare `new Response(response.body, …)` lets escape as an
 * uncaught 500. Awaiting the buffer here pulls any such error into the catch.
 */
/**
 * Start times of SSR renders that have not settled, one entry per in-flight
 * render. Module scope, so it is per-isolate and survives between requests —
 * which is the whole point.
 */
const inFlightRenders = new Set<number>();

/**
 * Longer than any legitimate render can be. The Workers runtime caps wall time
 * well below this, so an entry older than it cannot be a slow render — it is a
 * render whose invocation was destroyed before it could finish.
 */
const ABANDONED_RENDER_MS = 30_000;

/** Set once this isolate is known to have lost a render mid-flight. */
let isolatePoisoned = false;

/**
 * True when this isolate has had a render killed mid-flight, and should stop
 * attempting SSR.
 *
 * WHY THIS SHAPE. Angular guards module-global state with try/finally. When the
 * runtime destroys an invocation mid-render the finally never runs, and whatever
 * it was going to restore stays corrupted for the life of the isolate — which is
 * shared by every later request. Three such globals are known:
 *
 *   - `activeConsumer` (signals)            leaks -> NG0600 on the next signal write
 *   - `inNotificationPhase` (signals)       no exported setter
 *   - `NodeInjectorFactory.resolving`       lives on `tView.blueprint`, cached on
 *                                           the ComponentDef, so it leaks -> NG0200
 *                                           on every later render of that directive
 *
 * Resetting them one by one is whack-a-mole: only the first has a public setter,
 * and there is no reason to believe the list is complete. So this detects the
 * CAUSE they share — a render that started and never settled — instead of any
 * particular symptom. It stays correct if Angular adds a fourth.
 *
 * Detection has to happen at request start, because that is the only code we
 * know still runs: prod logs show our own render-failure handler never fires
 * once during poisoning, so the invocation dies before any catch of ours.
 */
function isolateIsPoisoned(): boolean {
  if (isolatePoisoned) {
    return true;
  }
  const now = Date.now();
  for (const startedAt of inFlightRenders) {
    if (now - startedAt > ABANDONED_RENDER_MS) {
      isolatePoisoned = true;
      inFlightRenders.clear();
      try {
        console.error(
          JSON.stringify({
            ssrFailure: 'isolate-poisoned',
            detail: 'a render was destroyed mid-flight; serving the CSR shell from now on',
            abandonedForMs: now - startedAt,
          }),
        );
      } catch {
        // Diagnostics must never mask the mitigation.
      }
      return true;
    }
  }
  return false;
}

async function renderOnce(request: Request): Promise<Response | null> {
  const startedAt = Date.now();
  inFlightRenders.add(startedAt);
  try {
    const response = await angularApp.handle(request);
    if (!response || response.status >= 500) {
      const outcome = response ? `HTTP ${response.status}` : 'nothing';
      logRenderFailure('render-status', request.url, `angularApp.handle() returned ${outcome}`);
      return null;
    }
    return new Response(await response.arrayBuffer(), response);
  } catch (error) {
    logRenderFailure('render-throw', request.url, error);
    return null;
  } finally {
    // A render that reaches here settled, however it ended. Only one destroyed
    // mid-flight leaves its entry behind, which is exactly the signal we want.
    inFlightRenders.delete(startedAt);
  }
}

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

    // Try Angular SSR for all routes, retrying a failed render ONCE. This covers
    // a genuinely transient failure — notably the mid-render throw that surfaces
    // only once the body is buffered (see renderOnce). It does NOT rescue a
    // POISONED isolate: measured in prod over a single keep-alive connection,
    // 20/20 renders fail on a poisoned isolate and 20/20 succeed on a healthy
    // one, so failure is a property of the isolate, not of the attempt. The
    // retry is kept because it is cheap — a poisoned isolate rejects the second
    // attempt in a few ms, and healthy renders never reach this path at all.
    // It needs a FRESH request because `angularApp.handle()` consumes the one it
    // is given.
    // On a poisoned isolate every SSR attempt fails in a few ms and the runtime
    // then kills the invocation, so the visitor gets a 503 instead of our
    // fallback. Skipping straight to the CSR shell turns that into a working
    // client-rendered page: worse for SEO on this isolate, but a page rather
    // than an error, and it holds until the recycle replaces the isolate.
    let rendered = isolateIsPoisoned() ? null : await renderOnce(request);
    if (!rendered && request.method === 'GET' && !isolatePoisoned) {
      rendered = await renderOnce(
        new Request(request.url, { method: 'GET', headers: new Headers(request.headers) }),
      );
    }

    if (rendered) {
      if (rendered.status >= 200 && rendered.status < 300) {
        const ttl = edgeCacheTtl(url);
        rendered.headers.set(
          'Cache-Control',
          `public, max-age=60, s-maxage=${ttl}, stale-while-revalidate=600`,
        );
        // Count this render in our first-party analytics (cookieless, non-blocking).
        sendPageViewBeacon(request, env, ctx, url.pathname);
        if (cacheable) {
          // Store a copy at the edge for SSR_CACHE_TTL seconds (non-blocking).
          ctx.waitUntil(cache.put(request, rendered.clone()));
        }
        return withSecurityHeaders(rendered);
      }
      // Redirects and genuine 404s are legitimate — propagate as-is, uncached.
      rendered.headers.set('Cache-Control', 'no-store');
      return withSecurityHeaders(rendered);
    }
    // Both render attempts failed — fall through to the CSR shell below so the
    // visitor gets a working client-rendered page instead of a hard error.

    // Fallback: serve the CSR shell for client-rendered, failed, or 5xx routes.
    const shell = await fetchCsrShell(request, env);
    if (shell) {
      // Serve the real CSR shell (a 200 HTML doc that boots the client app),
      // uncached so a transient failure isn't pinned at the edge.
      const propagated = new Response(shell.body, shell);
      propagated.headers.set('Cache-Control', 'no-store');
      return withSecurityHeaders(propagated);
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
