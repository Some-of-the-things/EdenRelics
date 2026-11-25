import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  RESPONSE_INIT,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { SeoService } from '../../services/seo.service';
import { HomeSectionsService, SectionPage } from '../../services/home-sections.service';
import { ProductStore } from '../../store/product.store';
import { Product } from '../../models/product.model';
import { orderedProductsById } from '../collections/collections.data';
import { routes } from '../../app.routes';
import { normaliseSectionPath, reservedFirstSegments, sectionPathState } from '../../utils/section-page-path';

/**
 * The catch-all route. Before saying "Page not found" it asks whether a custom homepage section
 * created a page at this address (the "Create this page" tick on the admin Home Sections tab), and
 * if so shows that section's title, description and pieces instead, with a 200 status.
 */
@Component({
  selector: 'app-not-found',
  imports: [RouterLink, CurrencyPipe],
  templateUrl: './not-found.component.html',
  styleUrls: ['../collections/collections.scss', './not-found.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotFoundComponent implements OnInit {
  private readonly seo = inject(SeoService);
  private readonly router = inject(Router);
  private readonly homeSections = inject(HomeSectionsService);
  private readonly productStore = inject(ProductStore);
  // Present only during server render; null in the browser.
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });

  /** undefined while checking, null when there's no created page here. */
  readonly page = signal<SectionPage | null | undefined>(undefined);

  /** The section's pieces that are still live, in the order she arranged them. */
  readonly products = computed<Product[]>(() => {
    const p = this.page();
    return p ? orderedProductsById(this.productStore.liveProducts(), p.productIds) : [];
  });

  constructor() {
    effect(() => {
      const p = this.page();
      if (p) {
        this.applySeo(p, this.products());
      }
    });
  }

  ngOnInit(): void {
    const path = normaliseSectionPath(this.router.url.split(/[?#]/)[0]);
    // Only ask the API about addresses a section could have created; anything else is a plain 404.
    if (sectionPathState(path, reservedFirstSegments(routes)) !== 'creatable') {
      this.showNotFound();
      return;
    }
    this.homeSections.getPage(path).subscribe({
      next: (page) => {
        if (this.responseInit) {
          this.responseInit.status = 200;
        }
        this.page.set(page);
      },
      error: () => this.showNotFound(),
    });
  }

  private showNotFound(): void {
    if (this.responseInit) {
      this.responseInit.status = 404;
    }
    this.page.set(null);
    this.seo.updateTags({
      title: 'Page not found',
      description: 'The page you are looking for could not be found.',
      noIndex: true,
    });
  }

  private applySeo(page: SectionPage, products: readonly Product[]): void {
    this.seo.updateTags({
      title: page.title,
      description: page.description || `${page.title}: hand-picked vintage from Eden Relics.`,
      url: page.path,
      image: products[0]?.imageUrl,
      // A page whose pieces have all sold is kept (links to it still work) but not indexed.
      noIndex: products.length === 0,
    });
  }
}
