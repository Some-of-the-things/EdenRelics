import { Route } from '@angular/router';
import { routes } from './app.routes';
import sitemapRoutes from '../../public/sitemap-routes.json';
import excluded from './sitemap-excluded-paths.json';

interface SitemapEntry {
  path: string;
  changefreq: string;
  priority: string;
}

/**
 * Routes intentionally excluded from the sitemap. Auth flows, transactional
 * pages, and admin shouldn't be in any search index. Keep this list narrow.
 *
 * The list itself lives in sitemap-excluded-paths.json because the prebuild
 * generator has to read it too. This spec validates the *committed*
 * sitemap-routes.json, which the generator overwrites on every `npm run build` —
 * so the guard that matters for the gated pages is the one inside the generator.
 * Notable members:
 *
 *   seller       authGuard'd dashboard, client-rendered.
 *   seller-tool  admin-only during the beta, client-rendered.
 *                Both were in the sitemap until 2026-08-03, which meant the
 *                first IndexNow run submitted the beta tool to Bing, Yandex,
 *                Seznam and Naver. The PUBLIC 'sellers/:slug' profile is
 *                dynamic and unaffected.
 *   top-picks    Flag-gated behind TopPicks:Enabled and currently 302ing, so
 *                submitting it would hand Google a redirect. Move it into
 *                sitemap-routes.json when the flag goes on and it returns 200.
 */
const SITEMAP_EXCLUDED_PATHS: ReadonlySet<string> = new Set(excluded.paths);

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
