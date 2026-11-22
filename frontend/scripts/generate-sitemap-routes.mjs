// Generates public/sitemap-routes.json — the single source of truth for the
// static URLs the backend SitemapController advertises.
//
// Collection routes are derived from their data file so the sitemap can never
// fall out of sync with the actual pages. Runs as the `prebuild` hook.
//
// Sub-category pages under /designers, /style and /dresses are NOT listed here.
// Since 2026-11 they are managed from the admin and stored in the database, so
// the backend SitemapController adds them itself from that table (visible pages
// only). Listing them here as well would keep a renamed or hidden page in the
// sitemap until the next frontend deploy.
//
// The hub index pages (/designers, /style, /dresses) stay listed here;
// app.routes.spec.ts fails the build if a public route is missing.

import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const excludedPath = resolve(here, '../src/app/sitemap-excluded-paths.json');
const collectionsPath = resolve(here, '../src/app/pages/collections/collections.data.ts');
const outPath = resolve(here, '../public/sitemap-routes.json');

// Collection slugs: a `slug:` immediately followed by `name:` is unique to a CollectionProfile.
const collectionsSrc = readFileSync(collectionsPath, 'utf8');
const collectionSlugs = [...collectionsSrc.matchAll(/slug:\s*'([^']+)',\s*name:/g)].map((m) => m[1]);
if (collectionSlugs.length === 0) {
  throw new Error('generate-sitemap-routes: no collection slugs found in collections.data.ts');
}

const before = [
  { path: '/', changefreq: 'daily', priority: '1.0' },
  { path: '/shop', changefreq: 'daily', priority: '0.9' },
  { path: '/shop/1950s', changefreq: 'weekly', priority: '0.7' },
  { path: '/shop/1960s', changefreq: 'weekly', priority: '0.7' },
  { path: '/shop/1970s', changefreq: 'weekly', priority: '0.7' },
  { path: '/shop/1980s', changefreq: 'weekly', priority: '0.7' },
  { path: '/shop/1990s', changefreq: 'weekly', priority: '0.7' },
  { path: '/shop/sale', changefreq: 'daily', priority: '0.6' },
  { path: '/about', changefreq: 'monthly', priority: '0.7' },
  { path: '/reviews', changefreq: 'monthly', priority: '0.5' },
  { path: '/contact', changefreq: 'monthly', priority: '0.6' },
  { path: '/blog', changefreq: 'weekly', priority: '0.7' },
  { path: '/care', changefreq: 'weekly', priority: '0.7' },
  { path: '/designers', changefreq: 'weekly', priority: '0.8' },
];
const collectionRoutes = collectionSlugs.map((slug) => ({
  path: `/collections/${slug}`,
  changefreq: 'weekly',
  priority: '0.7',
}));
// Hub index pages. The sub-category pages beneath them come from the backend.
const categoryRoutes = [
  { path: '/style', changefreq: 'weekly', priority: '0.8' },
  { path: '/dresses', changefreq: 'weekly', priority: '0.8' },
  // /top-picks was listed 2026-08-27 to 2026-10-13. Top Picks was then switched off in the
  // admin UI (Noir took its place on the homepage), so the page 302s and is excluded again.
  // If it is switched back on for good, list it here and drop it from the excluded paths.
];
// Marketplace-facing static pages. Two are deliberately absent:
//
//   /seller      authGuard'd dashboard.
//   /seller-tool admin-only during the beta.
//
// The last two were listed here until 2026-08-03, which put them in sitemap.xml
// and meant the first IndexNow run asked Bing, Yandex, Seznam and Naver to crawl
// the gated beta tool. Both client-render behind a guard, so a crawler only ever
// got an empty shell — but they should not be discoverable at all, and removing
// them from the JSON by hand achieved nothing because this generator runs on
// `prebuild` and put them straight back.
//
// The PUBLIC /sellers/:slug profile is dynamic and unaffected.
//
// Anything added here must also come out of SITEMAP_EXCLUDED_PATHS in
// app.routes.spec.ts, which fails the build if the two lists disagree.
const sellerRoutes = [];
const after = [
  { path: '/privacy-policy', changefreq: 'yearly', priority: '0.3' },
  { path: '/modern-slavery-policy', changefreq: 'yearly', priority: '0.3' },
  { path: '/supply-chain-policy', changefreq: 'yearly', priority: '0.3' },
  { path: '/returns-policy', changefreq: 'yearly', priority: '0.3' },
  { path: '/review-policy', changefreq: 'yearly', priority: '0.3' },
  { path: '/terms-conditions', changefreq: 'yearly', priority: '0.3' },
  { path: '/cookie-policy', changefreq: 'yearly', priority: '0.3' },
  { path: '/accessibility-report', changefreq: 'yearly', priority: '0.3' },
  { path: '/security', changefreq: 'yearly', priority: '0.3' },
  { path: '/compliance-report', changefreq: 'yearly', priority: '0.3' },
];

const routes = [
  ...before,
  ...collectionRoutes,
  ...categoryRoutes,
  ...sellerRoutes,
  ...after,
];

// Fail the build rather than emit a gated page. app.routes.spec.ts checks the
// committed JSON, but this script overwrites it on every `npm run build`, so
// that spec cannot catch a regression here — which is exactly how /seller and
// /seller-tool survived being deleted from the JSON by hand on 2026-08-03 and
// went out in the first IndexNow submission. Same list both sides read.
const excluded = JSON.parse(readFileSync(excludedPath, 'utf8')).paths;
const leaked = routes.filter((r) => excluded.includes(r.path.replace(/^\//, '')));
if (leaked.length > 0) {
  throw new Error(
    `generate-sitemap-routes: refusing to emit ${leaked
      .map((r) => r.path)
      .join(', ')} — listed in src/app/sitemap-excluded-paths.json. Remove it from this generator, or from that list if it is genuinely meant to be indexed.`,
  );
}

// Match the existing one-object-per-line formatting so diffs stay readable.
const body = routes
  .map((r) => `  { "path": ${JSON.stringify(r.path)}, "changefreq": ${JSON.stringify(r.changefreq)}, "priority": ${JSON.stringify(r.priority)} }`)
  .join(',\n');
writeFileSync(outPath, `[\n${body}\n]\n`, 'utf8');

console.log(`generate-sitemap-routes: wrote ${routes.length} routes (${collectionSlugs.length} collections; sub-category pages come from the backend) to public/sitemap-routes.json`);
