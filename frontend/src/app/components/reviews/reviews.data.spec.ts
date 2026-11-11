import { SHOP_REVIEWS, allReviews, googleRating, productPageReview } from './reviews.data';

describe('reviews data', () => {
  it('lists Google reviews before buyer reviews', () => {
    const sources = allReviews().map((r) => r.source);
    expect(sources.lastIndexOf('google')).toBeLessThan(sources.indexOf('buyer'));
    expect(allReviews().length).toBe(SHOP_REVIEWS.length);
  });

  it('computes the Google rating from Google reviews only', () => {
    expect(googleRating()).toBe('5.0');
  });

  it('gives every Google review a link and no buyer review one', () => {
    for (const r of SHOP_REVIEWS) {
      if (r.source === 'google') {
        expect(r.url).toMatch(/^https:\/\//);
      } else {
        expect(r.url).toBeUndefined();
      }
    }
  });

  it('only quotes product-page reviews on product pages, the same one for the same product', () => {
    const quote = productPageReview('01a13571-aed1-7968-87bd-8833aa3c8c76');
    expect(quote?.showOnProduct).toBe(true);
    expect(productPageReview('01a13571-aed1-7968-87bd-8833aa3c8c76')).toBe(quote);
  });

  it('never names where buyer reviews were left', () => {
    expect(JSON.stringify(SHOP_REVIEWS).toLowerCase()).not.toContain('vint' + 'ed');
  });
});
