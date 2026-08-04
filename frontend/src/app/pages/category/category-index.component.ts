import { ChangeDetectionStrategy, Component, computed, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { SeoService } from '../../services/seo.service';
import { ProductStore } from '../../store/product.store';
import { CategoryHub, hubPath, hubsOfKind, matchProductsToHub } from './category.data';

interface IndexCopy {
  title: string;
  metaTitle: string;
  metaDescription: string;
  url: string;
  heading: string;
  lede: string;
  /**
   * Journal guides that explain what these pages sell. Both index pages were
   * bare card grids — /dresses was 300 words and sitting at position 54.9 on
   * 121 impressions — and neither passed any internal link to the Journal,
   * which is what actually ranks: the prairie guide alone is 720 of the site's
   * 2,332 monthly impressions and had four internal links pointing at it.
   * Anchor text is descriptive on purpose; it is a signal, not a "read more".
   */
  journal: { label: string; path: string; note: string }[];
}

const COPY: Record<CategoryHub['kind'], IndexCopy> = {
  style: {
    title: 'Shop by Style',
    metaTitle: 'Shop Vintage by Style & Aesthetic',
    metaDescription:
      'Browse authentic vintage by aesthetic — cottagecore and prairie, and more style edits to come. Hand-picked one-of-a-kind pieces from Eden Relics, with UK shipping.',
    url: '/style',
    heading: 'Shop by Style',
    lede: 'Vintage grouped by the looks it belongs to rather than the decade it came from. Each style page gathers the original pieces that share an aesthetic — the real garments the trend is built on — with a short note on what defines the look and what to watch for when buying.',
    journal: [
      {
        label: 'What is a prairie dress?',
        path: '/blog/the-complete-guide-to-vintage-prairie-dresses-1',
        note: 'Where the style came from, how to tell an original from a reproduction, and the labels worth knowing.',
      },
      {
        label: 'Vintage vs reproduction: how to spot the difference',
        path: '/blog/vintage-vs-reproduction-how-to-spot-the-difference',
        note: 'The construction details that separate a genuine period piece from a modern take on the look.',
      },
    ],
  },
  garment: {
    title: 'Dress Types',
    metaTitle: 'Shop Vintage Dresses by Type',
    metaDescription:
      'Browse authentic vintage dresses by silhouette — maxi, midi and more. Hand-picked one-of-a-kind pieces from Eden Relics, each measured and inspected, with UK shipping.',
    url: '/dresses',
    heading: 'Dress Types',
    lede: 'Vintage dresses grouped by silhouette, so you can start from the shape you want to wear. Each page gathers the originals of that cut across the decades, with buying notes on fit, fabric and the era tells that come with the shape.',
    journal: [
      {
        label: 'What is a prairie dress?',
        path: '/blog/the-complete-guide-to-vintage-prairie-dresses-1',
        note: 'The maxi silhouette that keeps coming back — its history, and how to identify a genuine 1970s example.',
      },
      {
        label: 'Why your modern dress size does not apply to vintage',
        path: '/blog/vintage-dress-sizing-uk-why-your-modern-size-doesnt-apply',
        note: 'Vintage sizing runs small and inconsistently. Measurements are the only reliable guide.',
      },
      {
        label: 'Buying vintage clothing online: what to look for',
        path: '/blog/buying-vintage-clothing-online-what-to-look-for',
        note: 'What to check before you buy — measurements, condition, construction, and the listing itself.',
      },
    ],
  },
};

@Component({
  selector: 'app-category-index',
  imports: [RouterLink],
  templateUrl: './category-index.component.html',
  styleUrl: './category.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CategoryIndexComponent implements OnInit {
  private readonly seo = inject(SeoService);
  private readonly route = inject(ActivatedRoute);
  private readonly productStore = inject(ProductStore);

  private readonly kind: CategoryHub['kind'] = this.route.snapshot.data['kind'];
  readonly copy = COPY[this.kind];

  readonly cards = computed(() => {
    const products = this.productStore.liveProducts();
    return hubsOfKind(this.kind).map((hub) => ({
      hub,
      path: hubPath(hub),
      productCount: matchProductsToHub(products, hub).length,
    }));
  });

  ngOnInit(): void {
    this.seo.updateTags({
      title: this.copy.metaTitle,
      description: this.copy.metaDescription,
      url: this.copy.url,
    });
    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@type': 'CollectionPage',
      name: this.copy.metaTitle,
      description: this.copy.metaDescription,
      url: `https://edenrelics.co.uk${this.copy.url}`,
      hasPart: hubsOfKind(this.kind).map((hub) => ({
        '@type': 'WebPage',
        name: `Vintage ${hub.name}`,
        url: `https://edenrelics.co.uk${hubPath(hub)}`,
      })),
    });
  }
}
