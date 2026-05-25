import { ChangeDetectionStrategy, Component, computed, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SeoService } from '../../services/seo.service';
import { ProductStore } from '../../store/product.store';
import { DESIGNERS, matchProductsToDesigner } from './designers.data';

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

  readonly designers = DESIGNERS;

  readonly cards = computed(() => {
    const products = this.productStore.liveProducts();
    return DESIGNERS.map((d) => ({
      profile: d,
      productCount: matchProductsToDesigner(products, d).length,
    }));
  });

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Vintage Designers & Labels — Authentic Pieces',
      description: 'Browse vintage clothing by designer and label: Leslie Fay, Carole Little, Caroline Wells, Laura Ashley, Rockmount Ranch Wear, St Michael and more. Hand-picked authentic pieces from Eden Relics.',
      url: '/designers',
    });
    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@type': 'CollectionPage',
      name: 'Vintage Designers & Labels at Eden Relics',
      description: 'Curated index of vintage clothing labels stocked by Eden Relics.',
      url: 'https://edenrelics.co.uk/designers',
      hasPart: DESIGNERS.map((d) => ({
        '@type': 'WebPage',
        name: `Vintage ${d.name}`,
        url: `https://edenrelics.co.uk/designers/${d.slug}`,
      })),
    });
  }
}
