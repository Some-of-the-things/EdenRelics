import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { Route } from '@angular/router';
import { routes } from './app.routes';

interface SitemapEntry {
  path: string;
  changefreq: string;
  priority: string;
}

/**
 * Routes intentionally excluded from the sitemap. Adding a route here is a
 * deliberate decision — auth flows, transactional pages, and admin shouldn't
 * be in Google's index. Keep this list narrow.
 */
const SITEMAP_EXCLUDED_PATHS: ReadonlySet<string> = new Set([
  // Auth / account flows — not for public indexing
  'cart',
  'login',
  'register',
  'account',
  'settings',
  'forgot-password',
  'reset-password',
  'verify-email',
  // Admin
  'admin',
  'admin/login',
  // Dynamic landing pages handled separately (orders, reviews are per-user)
  // — they're matched by `:` filter below rather than listed here.
]);

function isDynamic(path: string): boolean {
  return path.includes(':');
}

function isWildcard(path: string): boolean {
  return path === '**';
}

function loadSitemapPaths(): Set<string> {
  const json = readFileSync(
    join(process.cwd(), 'public', 'sitemap-routes.json'),
    'utf-8',
  );
  const entries = JSON.parse(json) as SitemapEntry[];
  return new Set(entries.map((e) => e.path));
}

describe('sitemap-routes.json vs app.routes.ts', () => {
  const sitemapPaths = loadSitemapPaths();

  it('every public, static route is either in the sitemap or explicitly excluded', () => {
    const violations: string[] = [];
    for (const route of routes as Route[]) {
      const path = route.path ?? '';
      if (isWildcard(path) || isDynamic(path)) { continue; }
      if (SITEMAP_EXCLUDED_PATHS.has(path)) { continue; }

      // app.routes.ts paths are without a leading slash; sitemap-routes.json uses leading slash.
      const sitemapKey = `/${path}`.replace(/\/+$/, '') || '/';
      if (!sitemapPaths.has(sitemapKey)) {
        violations.push(
          `Route "${path || '/'}" exists in app.routes.ts but is not in public/sitemap-routes.json — add it to the JSON or to SITEMAP_EXCLUDED_PATHS in this spec.`,
        );
      }
    }
    expect(violations).toEqual([]);
  });

  it('every sitemap path corresponds to a real route in app.routes.ts', () => {
    // Build the universe of paths that app.routes.ts can serve:
    //   - exact static paths (e.g. "about", "designers")
    //   - dynamic-route prefixes (e.g. "designers/:slug" allows "designers/leslie-fay")
    const staticRoutePaths = new Set<string>();
    const dynamicRoutePrefixes: string[] = [];
    for (const route of routes as Route[]) {
      const path = route.path ?? '';
      if (isWildcard(path)) { continue; }
      if (isDynamic(path)) {
        // e.g. "designers/:slug" → prefix "designers/"
        const prefix = path.split('/:')[0] + '/';
        dynamicRoutePrefixes.push(prefix);
      } else {
        const key = `/${path}`.replace(/\/+$/, '') || '/';
        staticRoutePaths.add(key);
      }
    }

    const orphans: string[] = [];
    for (const sitemapPath of sitemapPaths) {
      if (staticRoutePaths.has(sitemapPath)) { continue; }
      // Allow paths that match a dynamic route, e.g. /designers/leslie-fay → designers/:slug
      const matchesDynamic = dynamicRoutePrefixes.some((prefix) =>
        sitemapPath.startsWith(`/${prefix}`),
      );
      if (matchesDynamic) { continue; }
      orphans.push(
        `Sitemap path "${sitemapPath}" doesn't match any route in app.routes.ts — remove it from sitemap-routes.json or add the matching route.`,
      );
    }
    expect(orphans).toEqual([]);
  });
});
