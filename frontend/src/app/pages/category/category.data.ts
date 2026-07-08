import { Product } from '../../models/product.model';

/**
 * A permanent, indexable category landing page — an aesthetic ("Cottagecore &
 * Prairie") or a garment silhouette ("Maxi Dresses"). Unlike a curated
 * collection (explicit SKU membership), a hub AUTO-POPULATES by matching
 * keywords against product names, so it stays evergreen as one-of-one stock
 * rotates without any manual re-curation. This mirrors the designer-hub model
 * (matchers + editorial prose + graceful empty state), just keyed on style or
 * silhouette instead of a brand.
 */
export interface CategoryHub {
  /** Which taxonomy this hub belongs to — drives the URL prefix and index. */
  kind: 'style' | 'garment';
  /** URL slug, e.g. 'cottagecore' → /style/cottagecore, 'maxi' → /dresses/maxi. */
  slug: string;
  /** Display name / H1 stem, e.g. 'Cottagecore & Prairie', 'Maxi Dresses'. */
  name: string;
  /** SEO <title> (without the ' | Eden Relics' suffix). */
  metaTitle: string;
  /** SEO meta description. */
  metaDescription: string;
  /** Short phrase shown under the H1. */
  tagline: string;
  /** Lead paragraph. */
  intro: string;
  /** A few short editorial paragraphs — the unique content that lets the page rank. */
  body: string[];
  /** "What to look for" bullets — practical buying notes for this category. */
  lookFor: string[];
  /**
   * Lowercased keywords matched against a product's NAME (not description, to
   * stay precise). A product joins the hub if ANY include keyword appears and NO
   * exclude keyword does. Order specific-first.
   */
  include: string[];
  /** Keywords that disqualify a product even if an include matched. */
  exclude?: string[];
  /** Cross-links to related era / designer / other hub pages (internal linking). */
  relatedLinks?: { label: string; path: string }[];
}

export const CATEGORY_HUBS: CategoryHub[] = [
  {
    kind: 'style',
    slug: 'cottagecore',
    name: 'Cottagecore & Prairie',
    metaTitle: 'Vintage Cottagecore & Prairie Dresses',
    metaDescription:
      'Original vintage cottagecore and prairie dresses — floral smocks, folk prints, ditsy florals, pinafores and gingham. Genuine one-of-a-kind pieces, hand-inspected, with UK shipping.',
    tagline: 'Romantic, rural and softly nostalgic — the vintage roots of the cottagecore look.',
    intro:
      'Cottagecore borrows almost everything from the real vintage wardrobe: the prairie dresses of the 1970s revival, the folk prints and smocks, the ditsy florals and the soft, unhurried silhouettes. These are the originals rather than the reproductions — genuine period pieces chosen for the same qualities the look is built on: natural cloth, gentle shaping and prints that feel picked from a garden rather than a trend board.',
    body: [
      'The aesthetic leans on a handful of recurring signals. Prairie dresses — often with a yoked or bibbed bodice, a high neck and a tiered or gathered skirt — sit at its centre, a style that had its own strong revival in the mid-to-late 1970s before the current one. Around them gather folk and peasant influences, smock and pinafore shapes, gingham checks and the small repeating "ditsy" florals that read as cottage-garden rather than bold statement.',
      'Because cottagecore is a mood more than a maker, it cuts across eras and labels. A 1970s handmade folk maxi, an 1980s Laura Ashley prairie midi and a 1990s cotton smock can all belong to it. What ties them together is feeling: soft structure, natural or naturally-drawn fabrics, and a quiet, lived-in romance rather than polish.',
      'Everything here is one-of-a-kind vintage, so the rail moves — pieces that fit the look come and go as they are found, and what you see is what currently remains.',
    ],
    lookFor: [
      'Prairie and pinafore shaping — yoked or bibbed bodices, high or ruffled necklines, gathered and tiered skirts.',
      'Small, dense "ditsy" florals and folk or peasant prints rather than large graphic ones.',
      'Natural cloth — cotton, cotton-blend and soft lightweight weaves — and cottage details like eyelet, lace trim, smocking and self-covered buttons.',
      'Because these are one-of-one pieces in a single size, always read the measurements rather than the label size — the vintage size converter on our journal posts helps translate them.',
    ],
    include: [
      'prairie',
      'folk',
      'smock',
      'pinafore',
      'gingham',
      'ditsy',
      'dirndl',
      'patchwork',
      'eyelet',
    ],
    relatedLinks: [
      { label: '1970s Vintage Dresses', path: '/shop/1970s' },
      { label: 'Vintage Laura Ashley', path: '/designers/laura-ashley' },
      { label: 'Maxi Dresses', path: '/dresses/maxi' },
    ],
  },
  {
    kind: 'garment',
    slug: 'maxi',
    name: 'Maxi Dresses',
    metaTitle: 'Vintage Maxi Dresses',
    metaDescription:
      'Original vintage maxi dresses from the 1970s to the 1990s — floral, paisley, folk and boho full-length styles. Genuine one-of-a-kind pieces, hand-measured and inspected, with UK shipping.',
    tagline: 'Full-length vintage, from 1970s folk to 1990s florals.',
    intro:
      'The maxi is the vintage rail’s most enduring silhouette — floor-skimming, forgiving and easy to wear across decades of changing fashion. These are original full-length dresses from the 1970s through the 1990s, gathered here in one place: folk and prairie maxis, paisley and botanical prints, bias-cut florals and the occasional statement sleeve.',
    body: [
      'Maxi length arrived in force at the turn of the 1970s and never fully left. The earliest pieces here lean folk and romantic — bell sleeves, contrast bibs, lace and crochet trim — while the 1980s and 1990s examples run to fluid rayon florals, paisley robes and relaxed, draped shaping. The through-line is the full length and the ease that comes with it.',
      'Fit on a maxi is more forgiving than a fitted midi, but length itself matters: a maxi cut for one height can pool or ride short on another. The measurements on each listing include the full length for exactly this reason, and are the reliable guide — vintage sizing rarely maps cleanly onto a modern number.',
      'As with everything at Eden Relics, each maxi is a single one-of-a-kind piece in one size, so the selection shifts as pieces are found and sold.',
    ],
    lookFor: [
      'Full (floor-length) hem — check the stated length against your height, as a maxi cut for someone taller can sit long.',
      'Era tells in the shaping: 1970s pieces lean folk and romantic (bell sleeves, bibs, lace); 1980s–90s pieces run to fluid rayon and draped shapes.',
      'Natural drape fabrics — rayon, cotton and lightweight blends — hang best in a full-length cut.',
      'One-of-one in a single size: read the pit-to-pit, waist and length measurements rather than trusting a vintage label size.',
    ],
    include: ['maxi'],
    relatedLinks: [
      { label: '1970s Vintage Dresses', path: '/shop/1970s' },
      { label: 'Cottagecore & Prairie', path: '/style/cottagecore' },
      { label: 'Midi Dresses', path: '/dresses/midi' },
    ],
  },
  {
    kind: 'garment',
    slug: 'midi',
    name: 'Midi Dresses',
    metaTitle: 'Vintage Midi Dresses',
    metaDescription:
      'Original vintage midi dresses from the 1960s to the 1990s — floral, paisley and printed mid-length styles. Genuine one-of-a-kind pieces, hand-measured and inspected, with UK shipping.',
    tagline: 'The mid-calf cut that runs through every decade on the rail.',
    intro:
      'The midi — roughly mid-calf, between the mini and the maxi — is the length that keeps coming back, and vintage is full of it. These are original mid-length dresses from the 1960s through the 1990s: watercolour and folk prints, ditsy florals, paisley and block prints, in everything from structured 1980s shapes to fluid 1990s viscose.',
    body: [
      'Midi is less an era than a proportion, which is why it spans the whole catalogue. A 1960s wool block-print, an 1980s dropped-waist floral and a 1990s rust viscose all share the same flattering mid-calf line. The decade shows in the details — the shoulder, the waist, the fabric weight — rather than the length itself.',
      'Because the midi sits at a specific point on the leg, where the hem falls on you depends on your height, so the length is worth checking as carefully as the bust and waist. Every listing states it.',
      'All one-of-a-kind vintage in a single size, so the selection here shifts as pieces are found and sold.',
    ],
    lookFor: [
      'Hem around mid-calf — check the stated length against your height, as “midi” on a taller original can read closer to maxi on a shorter frame.',
      'Decade tells in the shaping: structured waists and shoulders lean 1980s; softer, fluid drape leans 1990s.',
      'A defined waist (belted, tie or seamed) is common on midis — read both bust and waist measurements, as they can suggest different sizes.',
      'One-of-one in a single size: trust the pit-to-pit, waist and length figures over any vintage label size.',
    ],
    include: ['midi'],
    relatedLinks: [
      { label: '1980s Vintage Dresses', path: '/shop/1980s' },
      { label: 'Maxi Dresses', path: '/dresses/maxi' },
      { label: 'Cottagecore & Prairie', path: '/style/cottagecore' },
    ],
  },
  {
    kind: 'style',
    slug: 'boho',
    name: 'Boho & Paisley',
    metaTitle: 'Vintage Boho & Paisley Dresses',
    metaDescription:
      'Original vintage boho and paisley pieces — swirling paisley prints, 1970s folk maxis and free-spirited dresses. Genuine one-of-a-kind vintage, hand-inspected, with UK shipping.',
    tagline: 'Seventies folk, swirling paisley and a free-spirited print.',
    intro:
      'Boho draws on the 1970s at its most romantic and well-travelled — paisley and folk prints, soft maxi shapes, lace and velvet trim, the odd bishop or bell sleeve. These are the originals: genuine vintage pieces, many of them paisley, that carry the easy, layered spirit the look is named for.',
    body: [
      'Paisley is the thread that runs through most of it — the teardrop motif turns up on Liberty prints, on fluid rayon robe-style maxis, on skirts and blouses, and it reads as boho almost wherever it lands. Around it sit folk and peasant influences, animal and botanical prints, and the relaxed drape that lets a piece be layered rather than structured.',
      'The look leans 1970s in origin but isn’t bound to it: a 1990s rayon paisley maxi belongs to it as readily as a 1970s folk one. What unites them is print and ease over polish and tailoring.',
      'Everything here is one-of-a-kind vintage, so the rail moves — paisley and boho pieces come and go as they are found.',
    ],
    lookFor: [
      'Paisley and folk prints — the swirling teardrop motif is the surest boho signal, on dresses, skirts and blouses alike.',
      'Soft, layerable shaping — fluid maxis, robe styles and relaxed waists rather than sharp tailoring.',
      'Natural drape fabrics, especially rayon and lightweight blends, and romantic trim like lace, velvet and bishop sleeves.',
      'One-of-one in a single size: read the measurements rather than the label, as relaxed vintage cuts vary widely.',
    ],
    include: ['paisley', 'boho'],
    relatedLinks: [
      { label: '1970s Vintage Dresses', path: '/shop/1970s' },
      { label: 'Maxi Dresses', path: '/dresses/maxi' },
      { label: 'Cottagecore & Prairie', path: '/style/cottagecore' },
    ],
  },
];

export function hubsOfKind(kind: CategoryHub['kind']): CategoryHub[] {
  return CATEGORY_HUBS.filter((h) => h.kind === kind);
}

export function findHub(kind: CategoryHub['kind'], slug: string): CategoryHub | undefined {
  return CATEGORY_HUBS.find((h) => h.kind === kind && h.slug === slug);
}

/** The permanent URL for a hub, e.g. '/style/cottagecore' or '/dresses/maxi'. */
export function hubPath(hub: CategoryHub): string {
  return hub.kind === 'style' ? `/style/${hub.slug}` : `/dresses/${hub.slug}`;
}

/** True when a product's name qualifies it for a hub (include hit, no exclude hit). */
function productMatchesHub(name: string, hub: CategoryHub): boolean {
  if (hub.exclude?.some((x) => name.includes(x.toLowerCase()))) {
    return false;
  }
  return hub.include.some((k) => name.includes(k.toLowerCase()));
}

/**
 * Products belonging to a hub: name contains an include keyword and no exclude
 * keyword. Matching on the name (not description) keeps membership precise and
 * predictable. Order is preserved from the caller (typically newest-first).
 */
export function matchProductsToHub(products: readonly Product[], hub: CategoryHub): Product[] {
  return products.filter((p) => productMatchesHub(p.name.toLowerCase(), hub));
}

/**
 * The hubs (aesthetic + garment) a single product belongs to — used to cross-link
 * a product page back to its category hubs, feeding internal link authority into
 * the hubs so they aren't orphaned on the footer alone.
 */
export function findHubsForProduct(product: Product): CategoryHub[] {
  const name = product.name.toLowerCase();
  return CATEGORY_HUBS.filter((hub) => productMatchesHub(name, hub));
}
