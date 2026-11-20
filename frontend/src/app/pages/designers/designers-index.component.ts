import { ChangeDetectionStrategy, Component, computed, effect, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SeoService } from '../../services/seo.service';
import { ProductStore } from '../../store/product.store';
import { CategoryService } from '../../services/category.service';

@Component({
  selector: 'app-designers-index',
  imports: [RouterLink],
  templateUrl: './designers-index.component.html',
  styleUrl: './designers.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DesignersIndexComponent implements OnInit {
  private readonly seo = inject(SeoService);
  private readonly productStore = inject(ProductStore);
  private readonly categories = inject(CategoryService);

  readonly cards = computed(() => {
    const products = this.productStore.liveProducts();
    return this.categories.subCategoriesOf('designers').map((d) => ({
      profile: d,
      productCount: this.categories.productsIn(d, products).length,
    }));
  });

  constructor() {
    this.categories.load();
    // The page list comes from the API, so the structured data follows it once it has answered.
    effect(() => {
      this.categories.loaded();
      this.seo.setJsonLd({
        '@context': 'https://schema.org',
        '@type': 'CollectionPage',
        name: 'Vintage Designers & Labels at Eden Relics',
        description: 'Curated index of vintage clothing labels stocked by Eden Relics.',
        url: 'https://edenrelics.co.uk/designers',
        hasPart: this.categories.subCategoriesOf('designers').map((d) => ({
          '@type': 'WebPage',
          name: `Vintage ${d.name}`,
          url: `https://edenrelics.co.uk/designers/${d.slug}`,
        })),
      });
    });
  }

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Vintage Designers & Labels — Authentic Pieces',
      description: 'Browse vintage clothing by designer and label: Leslie Fay, Carole Little, Caroline Wells, Laura Ashley, Rockmount Ranch Wear, St Michael and more. Hand-picked authentic pieces from Eden Relics.',
      url: '/designers',
    });
  }
}
