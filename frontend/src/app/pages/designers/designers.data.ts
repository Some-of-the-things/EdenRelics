import { Product } from '../../models/product.model';

export interface DesignerProfile {
  slug: string;
  name: string;
  metaTitle: string;
  metaDescription: string;
  /** Country of origin, era active — short phrase shown under H1. */
  origin: string;
  /** Lead paragraph (300-400 chars). */
  intro: string;
  /** A few short paragraphs covering history + signature look. */
  history: string[];
  /** Bulleted authenticity / identification tips. */
  identification: string[];
  /**
   * Patterns to match against a product Name (lowercased) — if ANY match, the
   * product belongs to this designer. Use the most distinctive substring for
   * each, ordered specific-first.
   */
  productMatchers: string[];
}

export const DESIGNERS: DesignerProfile[] = [
  {
    slug: 'leslie-fay',
    name: 'Leslie Fay',
    metaTitle: 'Vintage Leslie Fay Dresses — Authentic 1980s & 1990s',
    metaDescription: 'Authentic vintage Leslie Fay dresses from the 1980s and 90s. Floral midis, polka dot peplums, and patchwork prints — hand-picked and inspected.',
    origin: 'New York, USA — founded 1947',
    intro: 'Leslie Fay built one of America\'s most recognised mid-priced dress labels, defining the polished, polka-dotted, peplum-waist look that ran from late-1970s offices through to the early-1990s. Original pieces are increasingly sought after for their tailored fit and printed rayons.',
    history: [
      'Leslie Fay Inc. was founded by Fred Pomerantz in 1947, named after his daughter. Through the 1970s and 80s the brand became a wardrobe staple in American department stores — Macy\'s, Dillard\'s, Hess\'s — sitting in the bracket between budget and designer.',
      'Its strongest era for collectors is the mid-1980s through early-90s: structured floral midi dresses, polka dot peplum two-pieces, and the patchwork-print rayon dresses that have become a signature on the resale market.',
      'After a well-publicised 1993 accounting scandal the label changed hands several times. Modern revivals exist, but vintage collectors prize the pre-1995 originals — recognisable by their fabric weight, label design, and construction details.',
    ],
    identification: [
      'Genuine 1980s/early-90s labels read "Leslie Fay" in a slim serif on a white woven tag — later licensed reissues use a heavier block font.',
      'Most authentic pieces from this era are 100% rayon or rayon-blend with a soft drape; modern reproductions tend to substitute polyester.',
      'Look for fabric-covered shoulder pads sewn into the lining, an interior waist tape, and "Made in USA" or "Made in Hong Kong" union labels.',
      'Patchwork-print midi dresses with crimson, teal, and floral panels are a particularly collected silhouette.',
    ],
    productMatchers: ['leslie fay'],
  },
  {
    slug: 'carole-little',
    name: 'Carole Little',
    metaTitle: 'Vintage Carole Little Dresses — 1980s & 1990s Rayon Florals',
    metaDescription: 'Authentic vintage Carole Little dresses — the rayon floral maxis and open-back styles that defined American 90s boho. UK shipping, hand-curated.',
    origin: 'Los Angeles, USA — founded 1975',
    intro: 'Carole Little\'s Los Angeles label became synonymous with the rayon floral maxi dress — relaxed, drapey, hand-painted-look prints with the kind of California-meets-bohemian elegance that has come back hard in 2020s vintage circles.',
    history: [
      'Carole Little and her husband Leonard Goldstein founded the label in Los Angeles in 1975, and through the 1980s and 90s it became one of America\'s most successful contemporary brands — known for relaxed silhouettes, artisanal prints, and saturated colours.',
      'The brand\'s signature was the rayon floral midi and maxi dress: open backs, soft shoulders, side pockets, and prints that ranged from inky florals to abstract botanicals. These are now highly collected on the resale market for their drape and unique prints.',
      'Carole Little Inc. ceased operations in 2003, which means every authentic piece is at least two decades old. The brand has not been revived under the same ownership, so all genuine Carole Little is vintage.',
    ],
    identification: [
      'Look for a white woven label reading "Carole Little" in a flowing italic script, often with "Saint-Tropez West" or "for Saint-Tropez West" underneath on earlier 80s pieces.',
      'Fabric is almost always 100% rayon or rayon/silk blend — soft, slightly slubbed, and unmistakably draped.',
      'Many dresses feature an interior waist-tie, a button-front bodice, and either an open back or a low-cut shoulder.',
      'Prints are typically all-over florals, abstract brushstrokes, or hand-painted-look botanicals — not geometric or ditsy.',
    ],
    productMatchers: ['carole little'],
  },
  {
    slug: 'caroline-wells',
    name: 'Caroline Wells',
    metaTitle: 'Vintage Caroline Wells Collection — 90s Rayon Florals',
    metaDescription: 'Authentic Caroline Wells Collection vintage dresses from the 1990s. Amber floral rayon maxis and similar pieces — UK-curated and inspected.',
    origin: 'USA — active 1990s',
    intro: 'Caroline Wells Collection was a 1990s American label producing the kind of soft-rayon floral maxi dress now intensely sought after on the secondhand market. Surviving pieces are relatively scarce, which makes them an interesting collector entry point.',
    history: [
      'Caroline Wells Collection is one of a cohort of mid-priced American dress labels that flourished in the 1990s alongside Carole Little, Robbie Bee, and R&K Originals — producing rayon florals, midi dresses, and easy-wear silhouettes for the department-store market.',
      'The brand operated primarily through US department stores in the early-to-mid 1990s and didn\'t survive the late-90s shift to fast fashion. Pieces are now firmly vintage and increasingly hard to source.',
      'Surviving pieces are recognised for the same qualities buyers prize in Carole Little: rayon drape, all-over floral or botanical prints, and relaxed silhouettes that fit a wide range of modern sizes.',
    ],
    identification: [
      'Labels typically read "Caroline Wells Collection" in a small serif on a white woven tag.',
      'Fabric is almost always 100% rayon — check the care label for the rayon marker as a quick authenticity test.',
      'Look for button-front bodices, square or scoop necklines, and side pockets — common construction details for the era.',
    ],
    productMatchers: ['caroline wells'],
  },
  {
    slug: 'laura-ashley',
    name: 'Laura Ashley',
    metaTitle: 'Vintage Laura Ashley Dresses — Authentic 1970s & 80s Prairie',
    metaDescription: 'Authentic vintage Laura Ashley prairie and floral dresses from the 1970s and 80s. The original cottagecore brand — hand-picked, UK shipping.',
    origin: 'Wales, UK — founded 1953',
    intro: 'Laura Ashley\'s prairie dresses essentially invented the cottagecore aesthetic decades before the term existed. Authentic 1970s and 80s pieces — with their woven labels, smocked bodices, and signature floral prints — are now among the most consistently collected British vintage labels.',
    history: [
      'Laura and Bernard Ashley founded the company in Wales in 1953, initially printing fabric. By the mid-1960s the brand had moved into ready-to-wear, and by the 1970s the Laura Ashley prairie dress was a defining piece of British fashion.',
      'The peak collector era is roughly 1975-1989: high-necked, smock-bodice, full-skirted dresses in small-scale floral cotton prints, often with white piping, lace trim, or pin-tucks. The brand pioneered what we now call cottagecore.',
      'The original company entered administration in 2020. Today the Laura Ashley name has been licensed to various retailers, but the original UK-manufactured pieces from the 1970s and 80s remain distinct in quality and construction.',
    ],
    identification: [
      'Genuine 1970s/80s labels are woven (not printed) and read "Laura Ashley" in a slim serif, with "Made in Wales", "Made in Great Britain", or "Made in Carno" on a second line.',
      'Original-era prints are small-scale florals on lightweight cotton or cotton-poly — never large abstract patterns or polyester.',
      'Construction details: smocked bodices, pin-tucks, lace or eyelet trim, covered buttons, and either a tie back or a high-neck collar.',
      'Be wary of post-2010 reissues which use heavier modern cotton and printed (not woven) labels.',
    ],
    productMatchers: ['laura ashley'],
  },
  {
    slug: 'rockmount-ranch-wear',
    name: 'Rockmount Ranch Wear',
    metaTitle: 'Vintage Rockmount Ranch Wear — Authentic Western Shirts',
    metaDescription: 'Authentic vintage Rockmount Ranch Wear western shirts. The Denver-original sawtooth-pocket label — hand-picked, UK shipping.',
    origin: 'Denver, Colorado — founded 1946',
    intro: 'Rockmount Ranch Wear invented the snap-button western shirt in 1946 and has been making the same iconic sawtooth-pocket, snap-front shirts ever since. Authentic vintage Rockmount is increasingly sought after as the western/cowboycore trend continues to grow.',
    history: [
      '"Papa" Jack A. Weil founded Rockmount Ranch Wear in Denver, Colorado in 1946. Weil is credited with putting snap fasteners on western shirts and pioneering the sawtooth chest pocket — both now genre-defining details.',
      'The brand is still family-owned and operated from the same downtown Denver building. Genuine vintage pieces — particularly from the 1980s and 90s — are recognised by their distinctive label evolution and increasingly vivid embroidery work.',
      'Rockmount\'s electric-coloured solid shirts, "Diamond Pioneer" snap details, and overstated yokes are now central to the cowboycore aesthetic that has driven huge growth in vintage western demand since the early 2020s.',
    ],
    identification: [
      'Look for the "Rockmount Ranch Wear / Tradition Since 1946 / Denver, Colorado" woven label — design varies by decade and is itself useful for dating.',
      'Authentic pieces feature pearl-snap (or pearl-imitation snap) closures, sawtooth chest pockets, and either embroidery or a contrast yoke.',
      'Fabric is typically cotton, cotton-blend, or — on dressier pieces — rayon. Polyester-blend Rockmounts exist but are usually 1990s onwards.',
      '"Made in USA" labelling is consistent on pre-2000s production.',
    ],
    productMatchers: ['rockmount'],
  },
  {
    slug: 'st-michael',
    name: 'St Michael (Marks & Spencer)',
    metaTitle: 'Vintage St Michael Clothing — M&S 1970s, 80s & 90s Originals',
    metaDescription: 'Authentic vintage St Michael clothing — Marks & Spencer\'s original house label from the 1970s, 80s and 90s. UK-curated and inspected.',
    origin: 'United Kingdom — Marks & Spencer house label, 1928-2000',
    intro: 'St Michael was Marks & Spencer\'s house label for the better part of the 20th century — and authentic vintage St Michael pieces are now collected for their construction quality, era-defining prints, and the fact that British wool, viscose, and cotton standards of the time consistently outlast modern equivalents.',
    history: [
      'Marks & Spencer introduced St Michael as its own-brand label in 1928, named after company chairman Michael Marks. By the 1970s, almost everything M&S sold carried the St Michael name.',
      'For vintage collectors, the strongest eras are the 1970s through early-1990s: tartan wool skirts, knitted floral cotton tops, smart blouses, and the printed dresses that filled British wardrobes during those decades.',
      'The St Michael label was retired in 2000 in favour of M&S sub-brands. Every authentic St Michael garment is therefore at least a quarter-century old — and many are considerably older.',
    ],
    identification: [
      'Look for the woven "St Michael" label, often with a quality-grade indicator and country of manufacture ("Made in England", "Made in Great Britain", or for later pieces "Made in Portugal").',
      'A second smaller label inside often gives the M&S size and care instructions, sometimes with a date code.',
      'British wool and viscose dominate the era — check the composition label for the fabric story.',
      'Construction details typical of UK manufacturing: bound seams, interior waist tape on skirts, and substantial buttons (often shell, wood, or solid plastic, rarely thin modern plastic).',
    ],
    productMatchers: ['st michael'],
  },
];

export function findDesignerBySlug(slug: string): DesignerProfile | undefined {
  return DESIGNERS.find((d) => d.slug === slug);
}

export function matchProductsToDesigner(
  products: readonly Product[],
  designer: DesignerProfile,
): Product[] {
  return products.filter((p) => {
    const name = p.name.toLowerCase();
    return designer.productMatchers.some((m) => name.includes(m.toLowerCase()));
  });
}
