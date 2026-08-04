/**
 * Builds a schema.org FAQPage entity from question/answer pairs, for inclusion
 * in a page's JSON-LD @graph. Only emit FAQ schema for Q&A that is genuinely
 * visible on the page — Google requires the answer text to match the on-page
 * content — so callers should derive entries from the same fields they render.
 *
 * Blank questions/answers are dropped; returns null when nothing usable remains
 * so callers can omit the entity entirely rather than emit an empty FAQPage.
 */
export interface FaqEntry {
  question: string;
  answer: string;
}

export function buildFaqPage(entries: FaqEntry[]): Record<string, unknown> | null {
  const mainEntity = entries
    .filter((e) => e.question.trim() && e.answer.trim())
    .map((e) => ({
      '@type': 'Question',
      name: e.question.trim(),
      acceptedAnswer: {
        '@type': 'Answer',
        text: e.answer.trim(),
      },
    }));

  if (mainEntity.length === 0) {
    return null;
  }

  return {
    '@type': 'FAQPage',
    mainEntity,
  };
}

/* ────────────────────────────────────────────────────────────────────────────
 * Deriving entries from stored post HTML
 *
 * The designer hubs hand-author their FAQ entries. Blog posts cannot: the copy
 * lives in the database as a single HTML blob. But the posts already ask and
 * answer questions in their own headings, so the pairs can be read back out of
 * the markup — which keeps the schema answer identical to the rendered answer
 * by construction, exactly as Google requires.
 *
 * What this is for: Google retired FAQ *rich results* for most sites in 2023,
 * so this will not put an accordion in the SERP. The value is machine-readable
 * Q&A for answer engines, which is where the gap actually is. Measured
 * 2026-08-04, "what is a prairie dress" is 115 impressions at position 7.8 for
 * a single click, and the whole prairie question cluster is 295 impressions for
 * that same one click — the answers are written and ranking, something else is
 * winning the answer.
 *
 * Regex rather than DOM parsing so it behaves identically under SSR and in unit
 * tests, matching how scripts/generate-sitemap-routes.mjs reads the data files.
 * ──────────────────────────────────────────────────────────────────────────── */

/** Below this, the text after a heading is a stub rather than an answer. */
const MIN_ANSWER_CHARS = 40;

/** An answer is the answer, not the whole section that happens to follow it. */
const MAX_ANSWER_CHARS = 700;

/**
 * One Q&A is not an FAQ, and emitting FAQPage for it overstates the page.
 * Two is where the markup starts describing something real.
 */
export const MIN_DERIVED_FAQ_ENTRIES = 2;

const ENTITIES: Record<string, string> = {
  '&nbsp;': ' ', '&amp;': '&', '&lt;': '<', '&gt;': '>', '&quot;': '"',
  '&#39;': "'", '&rsquo;': '’', '&lsquo;': '‘',
  '&mdash;': '—', '&ndash;': '–',
};

function toText(html: string): string {
  return html
    .replace(/<[^>]+>/g, ' ')
    .replace(/&[a-z#0-9]+;/gi, (e) => ENTITIES[e.toLowerCase()] ?? ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

/**
 * Two posts are authored as email HTML: layout tables carrying product cards
 * and CTAs, sitting in among the prose. Those are not part of any answer.
 */
function stripNonProse(html: string): string {
  return html
    .replace(/<table[\s\S]*?<\/table>/gi, ' ')
    .replace(/<figure[\s\S]*?<\/figure>/gi, ' ')
    .replace(/<(script|style)[\s\S]*?<\/\1>/gi, ' ');
}

/**
 * Reads question-form headings and the prose beneath them out of post HTML.
 * A heading only counts if it is actually phrased as a question.
 */
export function extractFaqsFromHtml(html: string | null | undefined, max = 10): FaqEntry[] {
  if (!html) {
    return [];
  }

  // Each h2/h3 plus everything up to the next heading.
  const sections = [...html.matchAll(/<h([23])\b[^>]*>([\s\S]*?)<\/h\1>([\s\S]*?)(?=<h[23]\b|$)/gi)];

  const entries: FaqEntry[] = [];
  const seen = new Set<string>();

  for (const section of sections) {
    const question = toText(section[2]);
    if (!question.endsWith('?')) {
      continue;
    }

    // Leading paragraphs only. A section can run for a dozen of them; the
    // answer to the question in the heading is at the top.
    const prose = stripNonProse(section[3]);
    const paragraphs = [...prose.matchAll(/<p\b[^>]*>([\s\S]*?)<\/p>/gi)]
      .map((m) => toText(m[1]))
      .filter((t) => t.length > 0);

    let answer = '';
    for (const p of paragraphs) {
      if (answer && answer.length + p.length + 1 > MAX_ANSWER_CHARS) {
        break;
      }
      answer = answer ? `${answer} ${p}` : p;
    }
    // A section built from a bare list rather than paragraphs still has text.
    if (!answer) {
      answer = toText(prose);
    }
    if (answer.length > MAX_ANSWER_CHARS) {
      answer = `${answer.slice(0, MAX_ANSWER_CHARS - 1).trimEnd()}…`;
    }

    const key = question.toLowerCase();
    if (answer.length < MIN_ANSWER_CHARS || seen.has(key)) {
      continue;
    }
    seen.add(key);
    entries.push({ question, answer });
    if (entries.length >= max) {
      break;
    }
  }

  return entries;
}

/**
 * FAQPage for a stored post, or null when it does not carry enough genuine Q&A
 * to warrant one.
 */
export function buildFaqPageFromHtml(html: string | null | undefined): Record<string, unknown> | null {
  const entries = extractFaqsFromHtml(html);
  return entries.length >= MIN_DERIVED_FAQ_ENTRIES ? buildFaqPage(entries) : null;
}
