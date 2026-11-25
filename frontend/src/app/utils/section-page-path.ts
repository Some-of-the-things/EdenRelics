import { Routes } from '@angular/router';

/**
 * Whether a homepage section's "Button goes to" address can have a page created at it. Mirrors
 * HomeSectionsService.IsCreatablePagePath on the API: one to three lowercase hyphenated parts, and
 * a first part the site doesn't already use. The API has the final say; this is for the admin hint.
 */
export type SectionPathState = 'empty' | 'existing' | 'invalid' | 'creatable';

const PAGE_PATH = /^\/[a-z0-9]+(?:-[a-z0-9]+)*(?:\/[a-z0-9]+(?:-[a-z0-9]+)*){0,2}$/;

/** First path segments the app's own routes use (plus the API), e.g. "shop" for shop/:decade. */
export function reservedFirstSegments(routes: Routes): Set<string> {
  const segments = new Set<string>(['api']);
  for (const route of routes) {
    const first = (route.path ?? '').split('/')[0];
    if (first && first !== '**' && !first.startsWith(':')) {
      segments.add(first);
    }
  }
  return segments;
}

/** The address as the page is served: trimmed, lowercased, no trailing slash. */
export function normaliseSectionPath(link: string): string {
  const trimmed = link.trim().toLowerCase();
  return trimmed.length > 1 ? trimmed.replace(/\/+$/, '') : trimmed;
}

export function sectionPathState(link: string, reserved: ReadonlySet<string>): SectionPathState {
  const path = normaliseSectionPath(link);
  if (path === '' || path === '/') {
    return path === '/' ? 'existing' : 'empty';
  }
  if (!path.startsWith('/') || path.startsWith('//')) {
    return 'invalid';
  }
  if (reserved.has(path.slice(1).split('/')[0])) {
    return 'existing';
  }
  return PAGE_PATH.test(path) ? 'creatable' : 'invalid';
}
