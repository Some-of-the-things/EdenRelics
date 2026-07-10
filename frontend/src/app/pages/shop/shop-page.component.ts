import { ChangeDetectionStrategy, Component, effect, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ProductListComponent } from '../../components/product-list/product-list.component';
import { ProductStore } from '../../store/product.store';
import { SeoService } from '../../services/seo.service';
import { Product } from '../../models/product.model';

interface ShopView {
  /** URL path relative to the site root, e.g. '/shop' or '/shop/1980s'. */
  path: string;
  /** Store category to filter by ('all' for the full catalogue). */
  category: Product['category'] | 'all';
  /** On-page H1. */
  heading: string;
  /** Intro sentence shown under the heading. */
  lede: string;
  /** SEO <title> (without the ' | Eden Relics' suffix). */
  title: string;
  /** SEO meta description. */
  description: string;
  /** Optional related-content CTA rendered as a second paragraph under the lede. */
  relatedLink?: { lead: string; text: string; routerLink: string; after?: string };
}

/**
 * Decade shop views. Keyed by the URL slug used in /shop/:decade. Each maps a
 * decade to the ProductStore category ('50s'…'90s') and carries its own
 * indexable title/description so the pages can rank for decade search terms.
 */
const DECADE_VIEWS: Record<string, ShopView> = {
  '1950s': {
    path: '/shop/1950s',
    category: '50s',
    heading: '1950s Vintage Dresses',
    lede: 'Full-skirted silhouettes and mid-century elegance — authentic 1950s pieces, hand-picked and inspected.',
    title: '1950s Vintage Dresses',
    description: 'Authentic 1950s vintage dresses, hand-picked and inspected. Full-skirted silhouettes and mid-century style, with UK shipping.',
  },
  '1960s': {
    path: '/shop/1960s',
    category: '60s',
    heading: '1960s Vintage Dresses',
    lede: 'Authentic 1960s vintage dresses from a decade when shapes simplified and colour did the work ornament used to do — shifts, A-lines, clean necklines, during one of the last decades when large-scale British dressmaking was still commonplace. Because “60s style” has been revived endlessly, we date every piece from physical evidence rather than appearance: label typefaces, the wording of fibre content, zip types, seam finishes. Stock in this category rotates as one-of-a-kind pieces come and go, but the standard doesn’t — each dress is inspected, honestly described, and sized from real measurements as a “best fits UK” size, never from the label, since sixties sizing runs roughly two modern sizes small.',
    title: '1960s Vintage Dresses',
    description: 'Authentic 1960s vintage dresses, hand-picked and inspected. Mod shifts, shift dresses and sixties style, with UK shipping.',
  },
  '1970s': {
    path: '/shop/1970s',
    category: '70s',
    heading: '1970s Vintage Dresses',
    lede: 'The 1970s is the heartland of our collection — the decade of the prairie dress, the folk maxi, and romantic dressmaking at its most inventive. Expect high necks, bell sleeves, lace and crochet bibs, paisley and ditsy florals, and skirts cut to move. This is the era of Laura Ashley at her height and of the small British boutique labels we hunt for, alongside beautifully made handmade pieces whose only label is the quality of their finishing. Every dress is dated from its label generation and construction — not guesswork — and measured properly, with sizing given as a “best fits UK” size.',
    relatedLink: {
      lead: 'For the story behind the silhouette, read our ',
      text: 'The Complete Guide to Vintage Prairie Dresses',
      routerLink: '/blog/the-complete-guide-to-vintage-prairie-dresses-1',
    },
    title: '1970s Vintage Dresses',
    description: 'Authentic 1970s vintage dresses, hand-picked and inspected. Boho prairie, maxi and prints from the seventies, with UK shipping.',
  },
  '1980s': {
    path: '/shop/1980s',
    category: '80s',
    heading: '1980s Vintage Dresses',
    lede: 'Authentic 1980s vintage dresses, better made than the decade’s reputation suggests. This is prime territory for the great British high-street labels — Laura Ashley’s wool-cotton florals, and St Michael by Marks & Spencer, quietly one of Britain’s most recognised vintage high-street labels and one of the most precisely datable, since M&S label designs changed in documented ways across the decade. We date every piece from that kind of evidence, disclose condition plainly, and size from the tape measure, not the label.',
    relatedLink: {
      lead: 'If there’s an old M&S dress in your own loft, our ',
      text: 'St Michael dating guide',
      routerLink: '/blog/st-michael-by-ms-how-to-identify-and-date-vintage-marks-spencer-clothing',
      after: ' will tell you exactly what you’re holding.',
    },
    title: '1980s Vintage Dresses',
    description: 'Authentic 1980s vintage dresses, hand-picked and inspected. Peplums, power shoulders and eighties colour, with UK shipping.',
  },
  '1990s': {
    path: '/shop/1990s',
    category: '90s',
    heading: '1990s Vintage Dresses',
    lede: 'Understated slips and minimalist florals — authentic 1990s pieces, hand-picked and inspected.',
    title: '1990s Vintage Dresses',
    description: 'Authentic 1990s vintage dresses, hand-picked and inspected. Slip dresses, florals and nineties minimalism, with UK shipping.',
  },
};

const ALL_VIEW: ShopView = {
  path: '/shop',
  category: 'all',
  heading: 'All Dresses',
  lede: 'The full Eden Relics collection — authentic vintage dresses spanning the 1950s to the 1990s, each hand-picked, measured and inspected.',
  title: 'Shop All Vintage Dresses',
  description: 'Browse the full Eden Relics collection of authentic vintage dresses from the 1950s to the 1990s. Each piece is hand-picked, measured and inspected, with UK shipping.',
};

@Component({
  selector: 'app-shop',
  imports: [ProductListComponent, RouterLink],
  templateUrl: './shop-page.component.html',
  styleUrl: './shop-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShopPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly seo = inject(SeoService);
  private readonly productStore = inject(ProductStore);

  readonly view = signal<ShopView>(ALL_VIEW);

  constructor() {
    effect(() => {
      this.emitJsonLd(this.view(), this.productStore.filteredProducts());
    });
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const decade = params.get('decade');
      // Unknown decade slugs shouldn't become thin, duplicate pages — send them
      // to the canonical /shop rather than rendering an empty filtered view.
      if (decade !== null && !DECADE_VIEWS[decade]) {
        this.router.navigate(['/shop'], { replaceUrl: true });
        return;
      }
      const view = decade !== null ? DECADE_VIEWS[decade] : ALL_VIEW;
      this.view.set(view);
      this.productStore.setCategory(view.category);
      this.seo.updateTags({
        title: view.title,
        description: view.description,
        url: view.path,
        hreflang: true,
      });
    });

    this.route.queryParamMap.subscribe((params) => {
      const q = params.get('q');
      this.productStore.setSearchQuery(q ?? '');
      const pageParam = params.get('page');
      const page = pageParam ? parseInt(pageParam, 10) : 1;
      this.productStore.setPage(Number.isFinite(page) && page > 0 ? page : 1);
    });
  }

  private emitJsonLd(view: ShopView, products: readonly Product[]): void {
    const url = `https://edenrelics.co.uk${view.path}`;
    const graph: object[] = [
      {
        '@type': 'CollectionPage',
        '@id': `${url}#page`,
        url,
        name: view.title,
        description: view.description,
        isPartOf: { '@id': 'https://edenrelics.co.uk/#website' },
      },
    ];

    if (products.length > 0) {
      graph.push({
        '@type': 'ItemList',
        '@id': `${url}#products`,
        name: view.heading,
        numberOfItems: products.length,
        itemListElement: products.slice(0, 30).map((p, idx) => ({
          '@type': 'ListItem',
          position: idx + 1,
          url: `https://edenrelics.co.uk/product/${p.slug || p.id}`,
          name: p.name,
          image: p.imageUrl,
        })),
      });
    }

    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@graph': graph,
    });
  }
}
