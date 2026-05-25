import { Route } from '@angular/router';
import { routes } from './app.routes';
import sitemapRoutes from '../../public/sitemap-routes.json';

interface SitemapEntry {
  path: string;
  changefreq: string;
  priority: string;
}

/**
 * Routes intentionally excluded from the sitemap. Auth flows, transactional
 * pages, and admin shouldn't be in Google's index. Keep this list narrow.
 */
const SITEMAP_EXCLUDED_PATHS: ReadonlySet<string> = new Set([
  'cart',
  'login',
  'register',
  'account',
  'settings',
  'forgot-password',
  'reset-password',
  'verify-email',
  'admin',
  'admin/login',
]);

function isDynamic(path: string): boolean {
  return path.includes(':');
}

function isWildcard(path: string): boolean {
  return path === '**';
}

const sitemapEntries = sitemapRoutes as SitemapEntry[];
const sitemapPaths = new Set(sitemapEntries.map((e) => e.path));

describe('sitemap-routes.json vs app.routes.ts', () => {
  it('every public, static route is either in the sitemap or explicitly excluded', () => {
    const violations: string[] = [];
    for (const route of routes as Route[]) {
      const path = route.path ?? '';
      if (isWildcard(path) || isDynamic(path)) { continue; }
      if (SITEMAP_EXCLUDED_PATHS.has(path)) { continue; }

      // app.routes.ts paths are without a leading slash; sitemap-routes.json uses leading slash.
      const sitemapKey = path === '' ? '/' : `/${path}`;
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
        // "designers/:slug" → "designers/", "product/:id" → "product/"
        const prefix = path.split('/:')[0] + '/';
        dynamicRoutePrefixes.push(prefix);
      } else {
        const key = path === '' ? '/' : `/${path}`;
        staticRoutePaths.add(key);
      }
    }

    const orphans: string[] = [];
    for (const sitemapPath of sitemapPaths) {
      if (staticRoutePaths.has(sitemapPath)) { continue; }
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
