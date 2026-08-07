/**
 * Which user agents are search/answer-engine crawlers, for the SSR-failure path
 * in worker.ts.
 *
 * When a render fails the worker falls back to the client-side shell, which is
 * a 200 carrying index.html's placeholder <title> and meta description —
 * identical on every URL. That is the right answer for a person: the page
 * client-renders and works. It is the wrong answer for a crawler, which indexes
 * a content-free page titled "Eden Relics — Vintage Clothing".
 *
 * It compounds: a poisoned isolate fails EVERY render until the two-hourly
 * recycle, so a single crawl session can collect dozens of identical shells.
 * Bing Webmaster Tools reporting "too many pages with identical titles" is
 * exactly the shape that leaves behind.
 *
 * Lives here rather than in worker.ts so it can be tested without booting the
 * Angular SSR engine.
 */

/**
 * Deliberately an explicit list rather than a loose /bot|crawler/ match,
 * because the costs are asymmetric: missing a crawler means one blank page
 * indexed until it next visits, but misclassifying a person means showing them
 * an error instead of a page that would have worked.
 */
export const INDEXING_CRAWLERS: readonly string[] = [
  'googlebot',
  'bingbot',
  'slurp',
  'duckduckbot',
  'baiduspider',
  'yandexbot',
  'applebot',
  'seznambot',
  'naver',
  'petalbot',
  // Answer engines cite pages, so a blank one is just as damaging there.
  'oai-searchbot',
  'chatgpt-user',
  'perplexitybot',
  'claude-searchbot',
  'claudebot',
  'gptbot',
];

export function isIndexingCrawlerUa(userAgent: string | null | undefined): boolean {
  if (!userAgent) {
    return false;
  }
  const ua = userAgent.toLowerCase();
  return INDEXING_CRAWLERS.some((bot) => ua.includes(bot));
}
