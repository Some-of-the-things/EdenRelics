import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { SeoService } from '../../services/seo.service';
import { CollectionProfile, findCollectionBySlug } from './collections.data';
import { environment } from '../../../environments/environment';

interface PreviewProduct {
  sku: string;
  name: string;
  slug: string;
  price: number;
  salePrice: number | null;
  imageUrl: string;
  isLive: boolean;
}

interface PublishResult {
  published: number;
  alreadyLive: number;
  notFound: string[];
}

/**
 * Admin-gated review page for a curated collection (/collections/preview/:slug).
 * It shows the pieces even while they're still held as Stock, and an
 * "Approve & publish" button flips them live via the admin-gated backend
 * endpoint. The route is behind adminGuard and the API requires the Admin role;
 * the admin JWT is attached automatically by the auth interceptor. Client-only
 * and noindex — never linked or in the sitemap.
 */
@Component({
  selector: 'app-collection-preview',
  imports: [RouterLink, CurrencyPipe],
  templateUrl: './collection-preview.component.html',
  styleUrl: './collections.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CollectionPreviewComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly http = inject(HttpClient);
  private readonly seo = inject(SeoService);

  readonly collection = signal<CollectionProfile | undefined>(undefined);
  readonly products = signal<PreviewProduct[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly publishing = signal(false);
  readonly published = signal<PublishResult | null>(null);

  /** True once every piece is live — either it already was, or we just published. */
  readonly allLive = computed(() => {
    const items = this.products();
    return items.length > 0 && items.every((p) => p.isLive);
  });

  ngOnInit(): void {
    this.seo.updateTags({ title: 'Collection preview', noIndex: true });

    const slug = this.route.snapshot.paramMap.get('slug') ?? '';
    const c = findCollectionBySlug(slug);
    this.collection.set(c);

    if (!c) {
      this.loading.set(false);
      this.error.set('Unknown collection.');
      return;
    }

    this.http.post<PreviewProduct[]>(
      `${environment.apiUrl}/api/collections/preview-products`,
      { skus: c.items.map((i) => i.sku) },
    ).subscribe({
      next: (products) => {
        this.products.set(products);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(this.explain(err.status));
      },
    });
  }

  approve(): void {
    const c = this.collection();
    if (!c || this.publishing() || this.allLive()) {
      return;
    }
    this.publishing.set(true);
    this.http.post<PublishResult>(
      `${environment.apiUrl}/api/collections/publish`,
      { items: c.items.map((i) => ({ sku: i.sku, slug: i.slug })) },
    ).subscribe({
      next: (result) => {
        this.publishing.set(false);
        this.published.set(result);
        // Reflect the new live state in the tiles without a reload.
        this.products.update((items) => items.map((p) => ({ ...p, isLive: true })));
      },
      error: (err) => {
        this.publishing.set(false);
        this.error.set(this.explain(err.status));
      },
    });
  }

  private explain(status: number): string {
    if (status === 401 || status === 403) {
      return 'Please sign in with an admin account to preview and publish this collection.';
    }
    return 'Something went wrong. Please try again in a moment.';
  }
}
