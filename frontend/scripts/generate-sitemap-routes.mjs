// Generates public/sitemap-routes.json — the single source of truth for the
// static URLs the backend SitemapController advertises.
//
// Designer, collection and category-hub routes are derived from their data
// files so the sitemap can never fall out of sync with the actual pages (add a
// designer/collection/hub there and it shows up here automatically on the next
// build). Runs as the `prebuild` hook.
//
// The category hubs were missing from this generator until 2026-07-30, so
// /style, /dresses and every hub beneath them had never been submitted to
// Google despite existing to rank. app.routes.spec.ts guards against that
// recurring.

import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const excludedPath = resolve(here, '../src/app/sitemap-excluded-paths.json');
const designersPath = resolve(here, '../src/app/pages/designers/designers.data.ts');
const collectionsPath = resolve(here, '../src/app/pages/collections/collections.data.ts');
const categoriesPath = resolve(here, '../src/app/pages/category/category.data.ts');
const outPath = resolve(here, '../public/sitemap-routes.json');

// Pull every designer slug, in file order, from the DESIGNERS data. Match only
// a `slug:` immediately followed by `name:` — that pairing is unique to a
// DesignerProfile, so unrelated `slug:` keys (e.g. RelatedPost link targets)
// don't leak in as bogus /designers/* sitemap routes.
const designersSrc = readFileSync(designersPath, 'utf8');
const slugs = [...designersSrc.matchAll(/slug:\s*'([^']+)',\s*name:/g)].map((m) => m[1]);
if (slugs.length === 0) {
  throw new Error('generate-sitemap-routes: no designer slugs found in designers.data.ts');
}

// Collection slugs, same `slug:` → `name:` pairing (unique to a CollectionProfile).
const collectionsSrc = readFileSync(collectionsPath, 'utf8');
const collectionSlugs = [...collectionsSrc.matchAll(/slug:\s*'([^']+)',\s*name:/g)].map((m) => m[1]);
if (collectionSlugs.length === 0) {
  throw new Error('generate-sitemap-routes: no collection slugs found in collections.data.ts');
}

// Category hubs. `kind` decides the URL prefix — 'style' hubs live at /style/:slug
// and 'garment' hubs at /dresses/:slug — so capture the pair, again anchored on a
// following `name:` so only real CategoryHub entries match.
const categoriesSrc = readFileSync(categoriesPath, 'utf8');
const hubs = [...categoriesSrc.matchAll(/kind:\s*'([^']+)',\s*slug:\s*'([^']+)',\s*name:/g)].map(
  (m) => ({ kind: m[1], slug: m[2] }),
);
if (hubs.length === 0) {
  throw new Error('generate-sitemap-routes: no category hubs found in category.data.ts');
}
// The pattern above needs kind/slug/name adjacent, so a comment inserted between
// them makes a hub vanish from the sitemap with no error — which is how
// /style/cottagecore silently dropped out on 2026-08-04. Count the `kind:` keys
// independently and insist every one of them produced a hub.
// Trailing comma required, so the CategoryHub interface's own
// `kind: 'style' | 'garment';` union is not counted as a hub.
const declaredHubs = (categoriesSrc.match(/^\s*kind:\s*'[^']+',/gm) ?? []).length;
if (declaredHubs !== hubs.length) {
  throw new Error(
    `generate-sitemap-routes: category.data.ts declares ${declaredHubs} hubs but only ${hubs.length} matched ` +
      `(${hubs.map((h) => h.slug).join(', ')}). The matcher needs kind/slug/name on consecutive lines — ` +
      'check for a comment or reordered key in the entries that are missing.',
  );
}
const unknownKind = hubs.find((h) => h.kind !== 'style' && h.kind !== 'garment');
if (unknownKind) {
  throw new Error(
    `generate-sitemap-routes: unknown hub kind '${unknownKind.kind}' for slug '${unknownKind.slug}'`,
  );
}

const before = [
  { path: '/', changefreq: 'daily', priority: '1.0' },
  { path: '/shop', changefreq: 'daily', priority: '0.9' },
  { path: '/shop/1950s', changefreq: 'weekly', priority: '0.7' },
  { path: '/shop/1960s', changefreq: 'weekly', priority: '0.7' },
  { path: '/shop/1970s', changefreq: 'weekly', priority: '0.7' },
  { path: '/shop/1980s', changefreq: 'weekly', priority: '0.7' },
  { path: '/shop/1990s', changefreq: 'weekly', priority: '0.7' },
  { path: '/about', changefreq: 'monthly', priority: '0.7' },
  { path: '/contact', changefreq: 'monthly', priority: '0.6' },
  { path: '/blog', changefreq: 'weekly', priority: '0.7' },
  { path: '/care', changefreq: 'weekly', priority: '0.7' },
  { path: '/designers', changefreq: 'weekly', priority: '0.8' },
];
const designerRoutes = slugs.map((slug) => ({
  path: `/designers/${slug}`,
  changefreq: 'weekly',
  priority: '0.7',
}));
const collectionRoutes = collectionSlugs.map((slug) => ({
  path: `/collections/${slug}`,
  changefreq: 'weekly',
  priority: '0.7',
}));
// Hub indexes plus one entry per hub. Same weighting as /designers and its pages.
const categoryRoutes = [
  { path: '/style', changefreq: 'weekly', priority: '0.8' },
  { path: '/dresses', changefreq: 'weekly', priority: '0.8' },
  ...hubs.map((h) => ({
    path: `/${h.kind === 'style' ? 'style' : 'dresses'}/${h.slug}`,
    changefreq: 'weekly',
    priority: '0.7',
  })),
];
// Marketplace-facing static pages. Three are deliberately absent:
//
//   /top-picks   gated behind TopPicks:Enabled and currently 302s, so submitting
//                it would advertise a redirect.
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
  { path: '/terms-conditions', changefreq: 'yearly', priority: '0.3' },
  { path: '/cookie-policy', changefreq: 'yearly', priority: '0.3' },
  { path: '/accessibility-report', changefreq: 'yearly', priority: '0.3' },
  { path: '/security', changefreq: 'yearly', priority: '0.3' },
  { path: '/compliance-report', changefreq: 'yearly', priority: '0.3' },
];

const routes = [
  ...before,
  ...designerRoutes,
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

console.log(`generate-sitemap-routes: wrote ${routes.length} routes (${slugs.length} designers, ${collectionSlugs.length} collections, ${hubs.length} category hubs) to public/sitemap-routes.json`);
