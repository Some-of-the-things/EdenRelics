/**
 * Flatten authored HTML to plain text, for places that need words rather than markup: meta
 * descriptions, JSON-LD, share text.
 *
 * This existed as two identical copies (product-detail and seo.service) and one different one
 * (admin, using DOMParser). The DOMParser version is the better implementation and is *not* used
 * here on purpose: this runs during server-side rendering, where DOMParser does not exist.
 *
 * The output is plain text and is bound as text, never as innerHTML. That matters, because step 3
 * strips tags and the steps after it decode entities — so `&lt;script&gt;` becomes `<script>` after
 * the stripping has already run. As text that is merely correct; rendered as HTML it would be an
 * injection. If you ever need this for innerHTML, use DOMParser instead.
 */

/** Entities we decode, in the order they must be applied. See the note on `&amp;` below. */
const ENTITIES: readonly (readonly [string, string])[] = [
  ['&nbsp;', ' '],
  ['&lt;', '<'],
  ['&gt;', '>'],
  ['&quot;', '"'],
  ['&#39;', "'"],
  // `&amp;` LAST, always. Decoding it first turns `&amp;lt;` into `&lt;`, which the next step then
  // decodes again into `<` — so text that was meant to show the characters "&lt;" silently becomes
  // a bracket. Ampersand is the escape character, so it has to be unescaped after everything that
  // could produce one.
  ['&amp;', '&'],
];

export function htmlToText(html: string): string {
  let text = html
    // Genuine patterns, so they stay regexes: replaceAll with a string cannot express "any tag",
    // and these two are case-insensitive.
    .replace(/<br\s*\/?>/gi, ' ')
    .replace(/<\/p>/gi, ' ')
    .replace(/<[^>]+>/g, '');

  for (const [entity, character] of ENTITIES) {
    // Literal strings, so replaceAll does a substring scan rather than compiling a regex.
    text = text.replaceAll(entity, character);
  }

  return text.replace(/\s+/g, ' ').trim();
}
