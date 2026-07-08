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
