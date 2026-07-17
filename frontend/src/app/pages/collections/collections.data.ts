import { Product } from '../../models/product.model';

/** One curated piece: its stable SKU plus the clean slug it should live at. */
export interface CollectionItem {
  sku: string;
  slug: string;
}

export interface CollectionProfile {
  slug: string;
  /** Display title, shown in Eden Relics branding at the top of the page. */
  name: string;
  metaTitle: string;
  metaDescription: string;
  /** Lead paragraph describing the edit. */
  intro: string;
  /**
   * The pieces in this collection, in display order. Membership is explicit (by
   * SKU) — not name-matched like designers — so a curated edit can't accidentally
   * pull in other products that share a designer or keyword. Each item also
   * carries the clean slug it should live at (used by the approve/publish flow to
   * fix the GUID-slugged pieces at go-live).
   */
  items: CollectionItem[];
  /** SKUs to feature on the homepage strip, in display order (subset of items). */
  featuredSkus: string[];
  /**
   * When true, this collection's membership is not the static `items` above but is curated at
   * runtime from the database (admin Top Picks tab) and served — gated by its own switch — via
   * TopPicksService. Only the profile text (name/meta/intro) comes from here. See
   * collection-page.component + home.component, which resolve the live SKUs by SKU, not slug.
   */
  dynamicMembership?: boolean;
}

export const COLLECTIONS: CollectionProfile[] = [
  {
    slug: 'wildflower-edit',
    name: 'The Wildflower Edit',
    metaTitle: 'The Wildflower Edit — Vintage Floral Dresses | Eden Relics',
    metaDescription:
      'Hand-picked vintage cottagecore and prairie dresses — soft florals, graceful silhouettes and true originals from Laura Ashley, Angela Gore, Van Allan and more.',
    intro:
      'The Wildflower Edit is a curated collection of vintage cottagecore and prairie dresses — romantic originals chosen for timelessness rather than trend. Each piece is united by soft florals, graceful silhouettes and thoughtful craftsmanship: dresses with their own stories that nonetheless speak the same language. Soft structure, considered detail, and the quiet confidence of clothes made to be loved for decades, not seasons. Every dress here has been hand-sourced, individually dated from its labels and construction, and measured properly — so what arrives is exactly what you fell for.',
    items: [
      { sku: 'ER-00121', slug: '1980s-90s-laura-ashley-floral-dress-pastel-lightweight-cotton-v-waist' },
      { sku: 'ER-00120', slug: '1980s-laura-ashley-red-floral-dress-wool-cotton-self-covered-buttons' },
      { sku: 'ER-00119', slug: '1970s-angela-gore-maxi-dress-paisley-lace-teal-velvet-waist' },
      { sku: 'ER-00118', slug: '1982-epcot-center-dress-raspberry-lace-collar-cuffs' },
      { sku: 'ER-00117', slug: '1970s-handmade-folk-floral-maxi-dress-navy-with-contrast-bib-lace-trim' },
      { sku: 'ER-00116', slug: '1970s-van-allan-maxi-dress-lilac-flutter-sleeves-lace-applique' },
      { sku: 'ER-00115', slug: '1970s-two-tone-maxi-dress-white-chocolate-with-velvet-trim-paisley-border-print' },
      { sku: 'ER-00114', slug: '1970s-handmade-floral-maxi-dress-white-with-bell-sleeves-lace-bow-detail' },
      { sku: 'ER-00103', slug: '1970s-empire-waist-maxi-dress-powder-blue-ditsy-floral' },
      { sku: 'ER-00102', slug: '1970s-striped-maxi-dress-crochet-lace-bib-sheer-sleeves' },
      { sku: 'ER-00101', slug: '1970s-handmade-pinafore-maxi-dress-blue-green-plaid-eyelet-bib' },
      { sku: 'ER-00100', slug: '1980s-90s-laura-ashley-floral-maxi-dress-navy-open-back-oversized-bow' },
      { sku: 'ER-00099', slug: '1980s-laura-ashley-rose-print-dress-burgundy-wool-cotton-twill' },
    ],
    featuredSkus: ['ER-00119', 'ER-00118', 'ER-00100', 'ER-00114', 'ER-00120'],
  },
  {
    slug: 'vintage-tartan',
    name: 'Vintage Tartan',
    metaTitle: 'Vintage Tartan Dresses & Skirts | Eden Relics',
    metaDescription:
      'Original vintage tartan and plaid — green tartan shirt dresses, plaid midis, wool tartan skirts and Viyella checks. Each a genuine one-of-a-kind piece, hand-inspected.',
    intro:
      'A gathering of original tartan and plaid — the checks that never really leave, from a forest-green shirt dress to a wool pencil skirt in jewel tones. Some are dresses, some are separates, and all are genuine vintage: one-of-a-kind pieces chosen for their cloth and their colour. Tartan is a small, fast-moving corner of the rail, so what you see here is what remains.',
    items: [
      { sku: 'ER-00039', slug: '1990s-canda-tartan-shirt-dress-forest-green-navy-plaid-versatile-fit' },
      { sku: 'ER-00037', slug: 'late-1990s-plaid-bodycon-midi-dress-burgundy-black-cream-check-front-slit' },
      { sku: 'ER-00101', slug: '1970s-handmade-pinafore-maxi-dress-blue-green-plaid-eyelet-bib' },
      { sku: 'ER-00050', slug: '1970s-viyella-tartan-two-piece-set-double-breasted-blouse-skirt' },
      { sku: 'ER-00008', slug: '1980s-90s-st-michael-tartan-wool-pencil-skirt-jewel-tones' },
    ],
    featuredSkus: ['ER-00039', 'ER-00037', 'ER-00101'],
  },
  {
    // Membership is curated in the admin (Top Picks tab) and served from the DB, gated by its own
    // TopPicks:Enabled switch — independent of the marketplace. Only the profile text below is
    // static; `items`/`featuredSkus` stay empty here and are supplied at runtime by TopPicksService.
    slug: 'top-picks',
    dynamicMembership: true,
    name: 'Our Top Picks',
    metaTitle: 'Our Top Picks — Curated Vintage Highlights | Eden Relics',
    metaDescription:
      "Our Top Picks — a rotating, hand-chosen selection of standout vintage pieces from across the shop. Each one inspected, dated and measured.",
    intro:
      "Our Top Picks are the pieces we can't stop looking at — a small, rotating selection of standout vintage chosen by hand from across the shop. Whatever the label or the decade, these are the ones we'd happily keep for ourselves: chosen for character, quality and that bit of something extra. Every piece is inspected, dated from its labels and construction, and measured properly, so what arrives is exactly what you fell for.",
    items: [],
    featuredSkus: [],
  },
];

export function findCollectionBySlug(slug: string): CollectionProfile | undefined {
  return COLLECTIONS.find((c) => c.slug === slug);
}

/** All product slugs in the collection, in display order. */
export function collectionProductSlugs(c: CollectionProfile): string[] {
  return c.items.map((i) => i.slug);
}

/** Featured product slugs (homepage strip), in display order. */
export function collectionFeaturedSlugs(c: CollectionProfile): string[] {
  const slugBySku = new Map(c.items.map((i) => [i.sku, i.slug]));
  return c.featuredSkus
    .map((sku) => slugBySku.get(sku))
    .filter((s): s is string => s !== undefined);
}

/**
 * Products belonging to a collection, in the collection's declared order.
 * Products not currently live (or not yet found in the store) are omitted, so
 * the page degrades gracefully before the pieces go live.
 */
export function orderedCollectionProducts(
  products: readonly Product[],
  slugs: readonly string[],
): Product[] {
  const bySlug = new Map(products.map((p) => [p.slug ?? p.id, p]));
  return slugs
    .map((s) => bySlug.get(s))
    .filter((p): p is Product => p !== undefined);
}

/**
 * Products for a dynamic (DB-curated) collection, resolved by product ID in the given order. Used by
 * Top Picks, whose membership is stored by the globally-unique product ID (unambiguous across sellers,
 * unlike SKU). Products not currently live/sold — or no longer in the store — are omitted, so a
 * sold-out or removed pick simply drops off.
 */
export function orderedProductsById(
  products: readonly Product[],
  ids: readonly string[],
): Product[] {
  const byId = new Map(products.map((p) => [p.id, p]));
  return ids
    .map((id) => byId.get(id))
    .filter((p): p is Product => p !== undefined);
}
