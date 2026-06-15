import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { SeoService } from '../../services/seo.service';
import { environment } from '../../../environments/environment';

interface CareIndexItem {
  name: string;
  slug: string;
  summary: string;
}

interface CareIndex {
  fabrics: CareIndexItem[];
  issues: CareIndexItem[];
}

@Component({
  selector: 'app-care-hub',
  imports: [RouterLink],
  templateUrl: './care-hub.component.html',
  styleUrl: './care-hub.component.scss',
})
export class CareHubComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly seo = inject(SeoService);

  readonly index = signal<CareIndex>({ fabrics: [], issues: [] });

  ngOnInit(): void {
    const title = 'Vintage Clothing Care Guides';
    const description =
      'How to wash, restore and store vintage clothing — fabric-by-fabric care and fixes for ' +
      'common problems like age yellowing, musty smells and moth holes, from the team at Eden Relics.';
    this.seo.updateTags({ title, description, url: '/care', type: 'website', hreflang: true });
    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@graph': [
        {
          '@type': 'CollectionPage',
          name: title,
          description,
          url: 'https://edenrelics.co.uk/care',
        },
        {
          '@type': 'BreadcrumbList',
          itemListElement: [
            { '@type': 'ListItem', position: 1, name: 'Home', item: 'https://edenrelics.co.uk' },
            { '@type': 'ListItem', position: 2, name: 'Vintage Care', item: 'https://edenrelics.co.uk/care' },
          ],
        },
      ],
    });

    this.http.get<CareIndex>(`${environment.apiUrl}/api/care`).subscribe({
      next: (index) => this.index.set(index),
      error: () => {},
    });
  }
}
