import { isIndexingCrawlerUa } from './indexing-crawlers';

describe('isIndexingCrawlerUa', () => {
  const CRAWLERS = [
    'Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)',
    'Mozilla/5.0 (compatible; bingbot/2.0; +http://www.bing.com/bingbot.htm)',
    'Mozilla/5.0 (compatible; DuckDuckBot/1.1; +http://duckduckgo.com/duckduckbot.html)',
    'Mozilla/5.0 (compatible; YandexBot/3.0; +http://yandex.com/bots)',
    'Mozilla/5.0 (compatible; Applebot/0.1; +http://www.apple.com/go/applebot)',
    'Mozilla/5.0 (compatible; OAI-SearchBot/1.0; +https://openai.com/searchbot)',
    'Mozilla/5.0 (compatible; PerplexityBot/1.0; +https://perplexity.ai/perplexitybot)',
    'Mozilla/5.0 (compatible; ClaudeBot/1.0; +claudebot@anthropic.com)',
  ];

  const PEOPLE = [
    // Real browsers, including the mobile ones most likely to trip a loose match.
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36',
    'Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1',
    'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36',
    'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36',
  ];

  it('recognises the search and answer engines we care about', () => {
    for (const ua of CRAWLERS) {
      expect(isIndexingCrawlerUa(ua)).toBe(true);
    }
  });

  /**
   * The expensive direction. A crawler misread as a person costs one blank page
   * indexed; a person misread as a crawler is shown an error instead of a page
   * that would have client-rendered perfectly well.
   */
  it('never mistakes a real browser for a crawler', () => {
    for (const ua of PEOPLE) {
      expect(isIndexingCrawlerUa(ua)).toBe(false);
    }
  });

  it('treats a missing or empty user agent as not a crawler', () => {
    expect(isIndexingCrawlerUa(null)).toBe(false);
    expect(isIndexingCrawlerUa(undefined)).toBe(false);
    expect(isIndexingCrawlerUa('')).toBe(false);
  });

  it('matches regardless of case', () => {
    expect(isIndexingCrawlerUa('GOOGLEBOT/2.1')).toBe(true);
    expect(isIndexingCrawlerUa('bingBot')).toBe(true);
  });
});
