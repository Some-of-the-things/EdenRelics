import { Router } from '@angular/router';
import { MissingSubCategory } from '../services/category.service';

/**
 * Sends an unknown sub-category URL to where it now lives, so old URLs never just break.
 *
 * On the server, @angular/ssr notices the router finished on a different URL and answers with a
 * real redirect carrying RESPONSE_INIT's status: 301 for a renamed or deleted page (passes its
 * search authority on), 302 for a temporarily hidden one. In the browser it's a plain navigation
 * that replaces the history entry. Returns false when there's nowhere to send it (a true 404).
 */
export function followMissingSubCategory(
  missing: MissingSubCategory,
  router: Router,
  responseInit: ResponseInit | null,
): boolean {
  if (!missing) {
    return false;
  }
  if (responseInit) {
    responseInit.status = missing.kind === 'moved' ? 301 : 302;
  }
  router.navigateByUrl(missing.to, { replaceUrl: true });
  return true;
}

/** Splits admin-entered page text into paragraphs on blank lines. */
export function paragraphs(text: string): string[] {
  return text
    .split(/\n\s*\n/)
    .map((p) => p.trim())
    .filter((p) => p.length > 0);
}
