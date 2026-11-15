/**
 * Customer reviews shown around the site: the homepage section, the product-page line, the
 * basket line and the /reviews page. Adding a review is one entry in SHOP_REVIEWS.
 *
 * Rules (from the reviews brief, 2026-11-10):
 * - Text is used exactly as written on the original. Only a pleasantry at the very start or end
 *   may be trimmed; never change or remove words in the middle.
 * - Names as shown on the original, usernames included. No dates on the cards.
 * - Google reviews link to the original, so their text must match Google word for word.
 * - Buyer reviews carry no link and no mention of where they were left.
 * - Every new review goes on the reviews page, including ones below 5 stars (UK rules don't
 *   allow hiding negative reviews).
 * - No Review / AggregateRating structured data for these.
 */

export type ReviewSource = 'google' | 'buyer';

export interface ShopReview {
  /** Name exactly as shown on the original review. */
  name: string;
  source: ReviewSource;
  /** Whole stars, 1 to 5. */
  rating: number;
  text: string;
  /** The original review on Google. Google reviews only. */
  url?: string;
  showOnHome: boolean;
  showOnProduct: boolean;
}

/** The Google Business Profile. The "5.0 on Google" button links here. */
export const GOOGLE_PROFILE_URL = 'https://maps.app.goo.gl/jG5ku2piUUzWn32w8';

/** In display order within each source; Google reviews always show first. */
export const SHOP_REVIEWS: readonly ShopReview[] = [
  {
    name: 'Adi Raad',
    source: 'google',
    rating: 5,
    text: 'You can truly feel the care and passion the seller puts into this business the moment you receive your item. The attention to detail is exceptional, and the quality speaks for itself. Highly recommended!',
    url: 'https://maps.app.goo.gl/eAqHRLixwwoPDvN26',
    showOnHome: true,
    showOnProduct: false,
  },
  {
    name: 'Elizabeth Archer',
    source: 'google',
    rating: 5,
    text: 'Bought an incredible frock from here online. Reasonably priced, beautiful condition and packaging and posted so quickly. Hard recommend for vintage lovers',
    url: 'https://maps.app.goo.gl/H43CDaZKFxGac7ai7',
    showOnHome: true,
    showOnProduct: false,
  },
  {
    name: 'amhegedus',
    source: 'buyer',
    rating: 5,
    text: 'Fantastic communication and timely shipping, item exactly as described.',
    showOnHome: true,
    showOnProduct: true,
  },
  {
    name: 'caitlodonnell',
    source: 'buyer',
    rating: 5,
    text: 'Super quick postage, lovely seller - love my new dress',
    showOnHome: true,
    showOnProduct: true,
  },
  {
    name: 'vintage4eva',
    source: 'buyer',
    rating: 5,
    text: 'gorgeous and arrived so quickly - hard recommend',
    showOnHome: true,
    showOnProduct: true,
  },
  {
    name: 'emmalemon81',
    source: 'buyer',
    rating: 5,
    text: 'Beautiful dress, quick delivery!',
    showOnHome: true,
    showOnProduct: true,
  },
  {
    name: 'southseajamboree_99',
    source: 'buyer',
    rating: 5,
    text: 'Fast delivery, accurately described, item is exactly what I wanted and expected!',
    showOnHome: false,
    showOnProduct: false,
  },
  {
    name: 'edinburgh170389',
    source: 'buyer',
    rating: 5,
    text: 'Such a beautiful dress!',
    showOnHome: false,
    showOnProduct: false,
  },
  {
    name: 'yetifever',
    source: 'buyer',
    rating: 5,
    text: 'lovely item',
    showOnHome: false,
    showOnProduct: false,
  },
  {
    name: 'vtgemum78',
    source: 'buyer',
    rating: 5,
    text: 'Fantastic, many thanks',
    showOnHome: false,
    showOnProduct: false,
  },
];

/** Every review, Google first, each source in its listed order. */
export function allReviews(): ShopReview[] {
  return [...SHOP_REVIEWS.filter((r) => r.source === 'google'), ...SHOP_REVIEWS.filter((r) => r.source !== 'google')];
}

/**
 * The Google rating, to one decimal place ("5.0"), counted from Google reviews only. It moves
 * with the entries above, so a lower Google review changes the number shown everywhere.
 */
export function googleRating(): string {
  const google = SHOP_REVIEWS.filter((r) => r.source === 'google');
  if (google.length === 0) {
    return '';
  }
  const mean = google.reduce((sum, r) => sum + r.rating, 0) / google.length;
  return mean.toFixed(1);
}

/**
 * One short review for a product page, picked from the product-page set by the product's ID, so
 * pages rotate through them while each page stays the same between the server render and the
 * browser (no hydration flicker).
 */
export function productPageReview(key: string): ShopReview | undefined {
  const pool = SHOP_REVIEWS.filter((r) => r.showOnProduct);
  if (pool.length === 0) {
    return undefined;
  }
  let hash = 0;
  for (let i = 0; i < key.length; i++) {
    hash = (hash * 31 + key.charCodeAt(i)) >>> 0;
  }
  return pool[hash % pool.length];
}
