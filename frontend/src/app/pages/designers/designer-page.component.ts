import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { SeoService } from '../../services/seo.service';
import { ProductStore } from '../../store/product.store';
import { Product } from '../../models/product.model';
import { DesignerProfile, findDesignerBySlug, matchProductsToDesigner } from './designers.data';

@Component({
  selector: 'app-designer-page',
  imports: [RouterLink, CurrencyPipe],
  templateUrl: './designer-page.component.html',
  styleUrl: './designers.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DesignerPageComponent {
  private readonly seo = inject(SeoService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly productStore = inject(ProductStore);

  private readonly slug = toSignal(
    this.route.paramMap,
    { initialValue: this.route.snapshot.paramMap },
  );

  readonly designer = computed<DesignerProfile | undefined>(() => {
    const s = this.slug().get('slug');
    return s ? findDesignerBySlug(s) : undefined;
  });

  readonly products = computed<Product[]>(() => {
    const d = this.designer();
    if (!d) {
      return [];
    }
    return matchProductsToDesigner(this.productStore.products(), d);
  });

  private readonly seoApplied = signal('');

  constructor() {
    effect(() => {
      const d = this.designer();
      if (!d) {
        // Unknown slug — bounce to /designers.
        if (this.slug().get('slug')) {
          this.router.navigate(['/designers']);
        }
        return;
      }
      // Avoid re-emitting tags for the same designer on every CD cycle.
      if (this.seoApplied() === d.slug) {
        return;
      }
      this.seoApplied.set(d.slug);
      this.applySeo(d);
    });
  }

  private applySeo(d: DesignerProfile): void {
    this.seo.updateTags({
      title: d.metaTitle,
      description: d.metaDescription,
      url: `/designers/${d.slug}`,
    });

    const products = matchProductsToDesigner(this.productStore.products(), d);
    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@type': 'CollectionPage',
      name: `Vintage ${d.name}`,
      description: d.intro,
      url: `https://edenrelics.co.uk/designers/${d.slug}`,
      isPartOf: {
        '@type': 'WebSite',
        '@id': 'https://edenrelics.co.uk/#website',
        name: 'Eden Relics',
        url: 'https://edenrelics.co.uk',
      },
      mainEntity: {
        '@type': 'ItemList',
        numberOfItems: products.length,
        itemListElement: products.map((p, idx) => ({
          '@type': 'ListItem',
          position: idx + 1,
          url: `https://edenrelics.co.uk/product/${p.slug || p.id}`,
          name: p.name,
          image: p.imageUrl,
        })),
      },
    });
  }
}
