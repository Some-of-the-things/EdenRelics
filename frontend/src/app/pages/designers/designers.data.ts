import { Product } from '../../models/product.model';

/** A Journal post relevant to a designer — used for two-way internal linking. */
export interface RelatedPost {
  /** Blog post slug (the DB-driven post served at /blog/:slug). */
  slug: string;
  /** Exact published title, shown as the link text. */
  title: string;
}

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
   * Optional override for the identification section heading. Defaults to
   * "How to identify authentic {name}". Category C brands (well-surviving but
   * thinly documented, with no real counterfeit market) use a "How to recognise
   * vintage {name}" framing instead — recognition, not authentication.
   */
  identificationHeading?: string;
  /**
   * Patterns to match against a product Name (lowercased) — if ANY match, the
   * product belongs to this designer. Use the most distinctive substring for
   * each, ordered specific-first.
   */
  productMatchers: string[];
  /**
   * Journal posts to cross-link from this designer hub (and which link back to
   * it). Builds a topic cluster so the hub and the post reinforce each other in
   * search rather than competing. Keep titles in sync with the published post.
   */
  relatedPosts?: RelatedPost[];
}

// Shared post references so the slug/title stay consistent across designers.
const ST_MICHAEL_POST: RelatedPost = {
  slug: 'st-michael-by-ms-how-to-identify-and-date-vintage-marks-spencer-clothing',
  title: 'St Michael by M&S: How to Identify and Date Vintage Marks & Spencer Clothing',
};
const RAYON_POST: RelatedPost = {
  slug: 'the-rise-and-fall-of-rayon',
  title: 'The Rise and Fall of Rayon',
};

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
    relatedPosts: [RAYON_POST],
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
    relatedPosts: [RAYON_POST],
  },
  {
    slug: 'caroline-wells',
    name: 'Caroline Wells',
    metaTitle: 'Caroline Wells Collection — Vintage 90s Dresses',
    metaDescription: 'Shop rare vintage Caroline Wells Collection dresses — 1990s rayon florals and maxis, each a genuine one-of-a-kind piece. Hand-inspected, UK shipping.',
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
    relatedPosts: [RAYON_POST],
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
    intro: 'Rockmount Ranch Wear is widely credited with popularising the snap-front western shirt and helping establish many of the design features associated with modern westernwear. Founded in Denver in 1946, the brand keeps many of its signature sawtooth-pocket, snap-front features in production today, and its classic shirts align closely with contemporary western-inspired fashion.',
    history: [
      '"Papa" Jack A. Weil founded Rockmount Ranch Wear in Denver, Colorado in 1946. He is widely credited with popularising the snap-front western shirt and helping establish many of the design features — including the sawtooth chest pocket — now associated with modern westernwear.',
      'The brand is still family-owned and operated from the same downtown Denver building. Genuine vintage pieces — particularly from the 1980s and 90s — are recognised by their distinctive label evolution and increasingly vivid embroidery work.',
      'Rockmount\'s electric-coloured solid shirts, "Diamond Pioneer" snap details, and overstated yokes align closely with the contemporary western-inspired fashion trends that have brought renewed visibility to vintage westernwear in recent years.',
    ],
    identification: [
      'Look for Rockmount Ranch Wear labels referencing Denver, Colorado and the company\'s westernwear heritage — wording varies by decade and is itself useful for dating.',
      'Authentic pieces feature pearl-snap (or pearl-imitation snap) closures, sawtooth chest pockets, and either embroidery or a contrast yoke.',
      'Fabric is typically cotton, cotton-blend, or — on dressier pieces — rayon. Polyester-blend Rockmounts also exist.',
      'Many vintage examples carry "Made in USA" labels.',
    ],
    productMatchers: ['rockmount'],
  },
  {
    slug: 'st-michael',
    name: 'St Michael (Marks & Spencer)',
    metaTitle: 'Vintage St Michael Clothing — M&S Dresses & Knitwear',
    metaDescription: 'Vintage St Michael clothing — Marks & Spencer\'s original house label from the 1970s-90s. Dresses, knits & blouses, plus how to identify and date the M&S label.',
    origin: 'United Kingdom — Marks & Spencer house label, 1928–2000',
    intro: 'St Michael was Marks & Spencer\'s house label for the better part of the 20th century, and authentic vintage St Michael clothing is prized by collectors for its construction quality and era-defining prints. The dresses, knitwear, blouses and tailored skirts that filled British wardrobes from the 1970s to the 90s came from a period when M&S was closely tied to British manufacturing and clothes built to last.',
    history: [
      'The St Michael name was introduced by Simon Marks in 1928, in honour of his father and M&S co-founder, Michael Marks. (The name first appeared in 1927 and was registered as a trademark in 1928.) By 1950, virtually all of Marks & Spencer\'s general merchandise carried the St Michael label.',
      'For vintage collectors, the strongest eras are the 1970s through early-1990s: tartan wool skirts, knitted floral cotton tops, smart blouses, and the printed dresses that filled British wardrobes during those decades.',
      'The St Michael label was retired in 2000 in favour of M&S sub-brands, and briefly returned as a limited heritage emblem on clothing in 2021. Original pieces from the historic brand run therefore date from before 2000, and are now vintage by most collectors\' standards.',
    ],
    identification: [
      'Look for the woven "St Michael" label, often with a country of manufacture. Earlier examples are typically marked "Made in England" or "Made in Great Britain"; later pieces may have been made in Portugal, Morocco, Malta, Sri Lanka, or other overseas production centres M&S used.',
      'A second, smaller label inside often gives the M&S size and care instructions, sometimes with a quality or style number — useful for cross-referencing.',
      'Label design changed significantly over the decades. Script logos, typography, sizing formats, and fibre-content and care labels can often narrow a garment\'s production date to a particular decade.',
      'British wool, viscose, and cotton feature heavily across the era — check the composition label for the fabric story.',
      'Higher-quality pieces often show details such as bound seams, waist stays, reinforced stitching, and heavier buttons (shell, wood, or solid plastic) than are common on modern fast-fashion garments.',
    ],
    productMatchers: ['st michael'],
    relatedPosts: [ST_MICHAEL_POST],
  },
  {
    slug: 'viyella',
    name: 'Viyella',
    metaTitle: 'Vintage Viyella — British Wool & Cotton Heritage',
    metaDescription: 'Authentic vintage Viyella — the British wool-cotton cloth first branded in 1890s Nottingham. Soft tartan shirts, blouses and tailored pieces, hand-picked.',
    origin: 'Nottingham, England — fabric brand registered in the 1890s',
    intro: 'Viyella is one of the oldest names in British cloth: a soft twill of merino wool and cotton, first branded by William Hollins & Co. of Nottingham in the 1890s and often cited as one of the world\'s earliest registered fabric trademarks. Vintage Viyella — tartan shirts, tailored blouses, and checked separates — is prized for a warmth and softness that modern blends rarely match.',
    history: [
      'Viyella was developed by William Hollins & Co. at their mills near Nottingham, and the name was registered as a fabric trademark in the 1890s — frequently described as the first branded fabric of its kind. The signature cloth is a twill blend of merino wool and cotton.',
      'Through the 20th century Viyella became a byword for quality British tailoring and easy warmth — used for shirts, blouses, dresses and childrenswear, and sold both as cloth by the yard and as finished garments under the Viyella name.',
      'Genuine vintage Viyella is collected today for its soft handle, its tartans and checks, and a build quality that has kept pieces wearable for decades. As with most heritage British labels, the woven label and the fabric itself are the surest signs of authenticity.',
    ],
    identification: [
      'Look for the woven "Viyella" label, often paired with "Made in England" or "Made in Great Britain" on older pieces.',
      'The cloth is a soft, lightly brushed wool-cotton twill — check the composition label for a wool/cotton blend rather than pure cotton or synthetic.',
      'Tartans, tattersall checks, and fine prints are characteristic.',
      'Quality construction: neat seams, substantial buttons, and a soft but substantial drape.',
    ],
    productMatchers: ['viyella'],
  },
  {
    slug: 'robbie-bee',
    name: 'Robbie Bee',
    metaTitle: 'Vintage Robbie Bee Dresses — 1980s & 90s American',
    metaDescription: 'Authentic vintage Robbie Bee dresses — the 1980s-90s American label of floral rayon and silky shift styles. Hand-picked, inspected, UK shipping.',
    origin: 'USA — active 1980s-1990s',
    intro: 'Robbie Bee was a popular American dress label of the 1980s and 90s, producing the floral rayons, silky shifts, and easy department-store silhouettes now sought after on the vintage market — part of the same cohort as Carole Little, Caroline Wells, and R&K Originals.',
    history: [
      'Robbie Bee was one of the mid-priced American dress labels that filled US department stores through the 1980s and 90s, alongside names like Carole Little, R&K Originals, and Caroline Wells.',
      'Its pieces favour the era\'s wearable silhouettes — florals, shifts, and midi dresses in rayon and silky fabrics — the kind of relaxed, printed dressing that has come back strongly in 2020s vintage.',
      'As with most of this cohort, genuine pieces are now firmly vintage and increasingly collected for their prints and drape rather than for a single signature design.',
    ],
    identification: [
      'Look for the woven "Robbie Bee" label.',
      'Fabrics are typically rayon or silky synthetics with a fluid drape.',
      'All-over florals and simple shift or midi silhouettes are characteristic of the era.',
    ],
    productMatchers: ['robbie bee'],
    relatedPosts: [RAYON_POST],
  },
  {
    slug: 'rk-originals',
    name: 'R&K Originals',
    metaTitle: 'Vintage R&K Originals Dresses — Printed Florals & Day Dresses',
    metaDescription: 'Authentic vintage R&K Originals dresses — the American label of printed day dresses and florals, found across several decades of 20th-century fashion. Hand-picked and inspected, UK shipping.',
    origin: 'USA — 20th-century American dress label',
    intro: 'R&K Originals was an American dress label whose printed day dresses and florals turn up across several decades of 20th-century fashion, sold at accessible prices. Surviving pieces are collected for their period prints and easy, wearable silhouettes.',
    history: [
      'Much of what we can say about R&K Originals comes from the garments themselves rather than detailed company records. The number and range of surviving pieces point to an American label that produced printed dresses in some volume across several decades.',
      'In look and price point it falls into the same accessible, printed-dress category as Carole Little and Caroline Wells — though that grouping is a useful way to place the label for readers rather than a documented connection between the companies.',
      'Genuine vintage R&K Originals is collected for its period prints and easy silhouettes. As with similar labels, the best way to place a piece is to read the woven label alongside the fabric and construction — there is no real counterfeit market, so this is about recognition rather than authentication.',
    ],
    identificationHeading: 'How to recognise vintage R&K Originals',
    identification: [
      'Look for the original "R&K Originals" label on the garment.',
      'Printed fabrics are common, including florals and other repeating patterns.',
      'Dresses appear frequently among surviving examples, including day dresses and longer-length styles.',
      'Fabrics vary by era and garment type, but rayon, rayon blends, polyester blends, and other lightweight dress fabrics are commonly encountered.',
      'Consider the label, fibre content, garment construction, and overall styling together when identifying a piece.',
    ],
    productMatchers: ['r&k originals'],
    relatedPosts: [RAYON_POST],
  },
  {
    slug: 'mondi',
    name: 'Mondi',
    metaTitle: 'Vintage Mondi — German Print & Colour Womenswear',
    metaDescription: 'Authentic vintage Mondi — the German womenswear label whose surviving pieces often feature bold colour and pattern across dresses, separates and jackets. Hand-picked and inspected, UK shipping.',
    origin: 'Munich, Germany — founded 1967',
    intro: 'Mondi is a recognised German womenswear label of the later twentieth century. Its surviving pieces — dresses, separates, jackets, and coordinated outfits — are often recognisable for their strong use of colour and pattern, though styles varied considerably across the brand\'s lifespan.',
    history: [
      'Mondi was founded in Munich in 1967 by Herwig Zahm and Otto Brüstle, beginning as a knitwear business before expanding into womenswear.',
      'Mondi was part of the late twentieth-century European ready-to-wear tradition, producing dresses, separates, jackets, and coordinated outfits that became widely distributed across parts of Europe.',
      'Most surviving Mondi garments date from the 1970s, 1980s, and 1990s. Bold colour and pattern are common across them, but the brand\'s output varied widely, so styling alone is a weaker guide than the label and country-of-origin markings.',
    ],
    identificationHeading: 'How to recognise vintage Mondi',
    identification: [
      'Look for original Mondi labels on the garment.',
      'Labels marked "Made in West Germany" indicate production before German reunification in 1990; later pieces are marked "Made in Germany".',
      'Many surviving examples feature bold colours, florals, abstract prints, or paisley patterns.',
      'Dresses, skirts, blouses, jackets, and coordinated separates are commonly encountered.',
      'Consider the label, country-of-origin information, fabric composition, and garment styling together when identifying pieces.',
    ],
    productMatchers: ['mondi'],
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

/** The designer (if any) whose matchers appear in a product's name. */
export function findDesignerForProduct(productName: string): DesignerProfile | undefined {
  const name = productName.toLowerCase();
  return DESIGNERS.find((d) => d.productMatchers.some((m) => name.includes(m.toLowerCase())));
}

/** Designers that cross-link to a given Journal post — the reverse of relatedPosts. */
export function findDesignersForPost(postSlug: string): DesignerProfile[] {
  return DESIGNERS.filter((d) => d.relatedPosts?.some((p) => p.slug === postSlug));
}
