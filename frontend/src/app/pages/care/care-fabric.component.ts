import {
  Component,
  OnInit,
  RESPONSE_INIT,
  inject,
  input,
  signal,
  ChangeDetectionStrategy,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { SeoService } from '../../services/seo.service';
import { environment } from '../../../environments/environment';

interface CareProduct {
  id: string;
  name: string;
  slug: string;
  price: number;
  salePrice: number | null;
  imageUrl: string;
}

interface CareFabric {
  slug: string;
  name: string;
  intro: string;
  fiberContent: string;
  howToIdentify: string;
  washing: string;
  drying: string;
  ironing: string;
  storing: string;
  vintageCautions: string;
  dos: string[];
  donts: string[];
  metaTitle: string;
  metaDescription: string;
  reviewedBy: string | null;
  lastReviewedUtc: string | null;
}

@Component({
  selector: 'app-care-fabric',
  imports: [RouterLink],
  templateUrl: './care-fabric.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './care-fabric.component.scss',
})
export class CareFabricComponent implements OnInit {
  readonly slug = input.required<string>();
  private readonly http = inject(HttpClient);
  private readonly seo = inject(SeoService);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });

  readonly fabric = signal<CareFabric | null>(null);
  readonly products = signal<CareProduct[]>([]);
  readonly error = signal(false);

  ngOnInit(): void {
    this.http
      .get<CareProduct[]>(`${environment.apiUrl}/api/care/fabric/${this.slug()}/products`)
      .subscribe({
        next: (products) => this.products.set(products),
        error: () => {},
      });
    this.http.get<CareFabric>(`${environment.apiUrl}/api/care/fabric/${this.slug()}`).subscribe({
      next: (fabric) => {
        this.fabric.set(fabric);
        const title = fabric.metaTitle || `Caring for Vintage ${fabric.name}`;
        const description = fabric.metaDescription || this.snippet(fabric.intro);
        const url = `/care/fabric/${fabric.slug}`;
        this.seo.updateTags({
          title,
          description,
          url,
          type: 'article',
          hreflang: true,
        });
        this.seo.setJsonLd({
          '@context': 'https://schema.org',
          '@graph': [
            {
              '@type': 'Article',
              headline: title,
              description,
              about: `Vintage ${fabric.name} fabric care`,
              author: { '@type': 'Organization', name: 'Eden Relics' },
              publisher: {
                '@type': 'Organization',
                name: 'Eden Relics',
                logo: { '@type': 'ImageObject', url: 'https://edenrelics.co.uk/logo.png' },
              },
              ...(fabric.lastReviewedUtc ? { dateModified: fabric.lastReviewedUtc } : {}),
              mainEntityOfPage: { '@type': 'WebPage', '@id': `https://edenrelics.co.uk${url}` },
            },
            {
              '@type': 'BreadcrumbList',
              itemListElement: [
                {
                  '@type': 'ListItem',
                  position: 1,
                  name: 'Home',
                  item: 'https://edenrelics.co.uk',
                },
                {
                  '@type': 'ListItem',
                  position: 2,
                  name: 'Vintage Care',
                  item: 'https://edenrelics.co.uk/care',
                },
                {
                  '@type': 'ListItem',
                  position: 3,
                  name: title,
                  item: `https://edenrelics.co.uk${url}`,
                },
              ],
            },
          ],
        });
      },
      error: () => {
        this.error.set(true);
        if (this.responseInit) {
          this.responseInit.status = 404;
        }
        this.seo.updateTags({ title: 'Care guide not found', noIndex: true });
      },
    });
  }

  private snippet(text: string): string {
    const plain = text.trim();
    return plain.length > 160 ? plain.slice(0, 157).trimEnd() + '…' : plain;
  }
}
