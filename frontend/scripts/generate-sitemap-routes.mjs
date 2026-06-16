// Generates public/sitemap-routes.json — the single source of truth for the
// static URLs the backend SitemapController advertises.
//
// Designer routes are derived from designers.data.ts so the sitemap can never
// fall out of sync with the actual designer pages (add a designer there and it
// shows up here automatically on the next build). Runs as the `prebuild` hook.

import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const designersPath = resolve(here, '../src/app/pages/designers/designers.data.ts');
const outPath = resolve(here, '../public/sitemap-routes.json');

// Pull every designer slug, in file order, from the DESIGNERS data.
const designersSrc = readFileSync(designersPath, 'utf8');
const slugs = [...designersSrc.matchAll(/slug:\s*'([^']+)'/g)].map((m) => m[1]);
if (slugs.length === 0) {
  throw new Error('generate-sitemap-routes: no designer slugs found in designers.data.ts');
}

const before = [
  { path: '/', changefreq: 'daily', priority: '1.0' },
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

const routes = [...before, ...designerRoutes, ...after];

// Match the existing one-object-per-line formatting so diffs stay readable.
const body = routes
  .map((r) => `  { "path": ${JSON.stringify(r.path)}, "changefreq": ${JSON.stringify(r.changefreq)}, "priority": ${JSON.stringify(r.priority)} }`)
  .join(',\n');
writeFileSync(outPath, `[\n${body}\n]\n`, 'utf8');

console.log(`generate-sitemap-routes: wrote ${routes.length} routes (${slugs.length} designers) to public/sitemap-routes.json`);
