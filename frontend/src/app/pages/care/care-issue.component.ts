import { Component, OnInit, RESPONSE_INIT, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { SeoService } from '../../services/seo.service';
import { environment } from '../../../environments/environment';

interface CareIssue {
  slug: string;
  name: string;
  causes: string;
  generalMethod: string;
  whatNotToDo: string;
  whenToSeeAPro: string;
  metaTitle: string;
  metaDescription: string;
  reviewedBy: string | null;
  lastReviewedUtc: string | null;
}

@Component({
  selector: 'app-care-issue',
  imports: [RouterLink],
  templateUrl: './care-issue.component.html',
  styleUrl: './care-fabric.component.scss',
})
export class CareIssueComponent implements OnInit {
  readonly slug = input.required<string>();
  private readonly http = inject(HttpClient);
  private readonly seo = inject(SeoService);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });

  readonly issue = signal<CareIssue | null>(null);
  readonly error = signal(false);

  ngOnInit(): void {
    this.http.get<CareIssue>(`${environment.apiUrl}/api/care/problem/${this.slug()}`).subscribe({
      next: (issue) => {
        this.issue.set(issue);
        const title = issue.metaTitle || issue.name;
        const description = issue.metaDescription || this.snippet(issue.causes || issue.generalMethod);
        const url = `/care/problem/${issue.slug}`;
        this.seo.updateTags({ title, description, url, type: 'article', hreflang: true });
        this.seo.setJsonLd({
          '@context': 'https://schema.org',
          '@graph': [
            {
              '@type': 'Article',
              headline: title,
              description,
              author: { '@type': 'Organization', name: 'Eden Relics' },
              publisher: {
                '@type': 'Organization',
                name: 'Eden Relics',
                logo: { '@type': 'ImageObject', url: 'https://edenrelics.co.uk/logo.png' },
              },
              ...(issue.lastReviewedUtc ? { dateModified: issue.lastReviewedUtc } : {}),
              mainEntityOfPage: { '@type': 'WebPage', '@id': `https://edenrelics.co.uk${url}` },
            },
            {
              '@type': 'BreadcrumbList',
              itemListElement: [
                { '@type': 'ListItem', position: 1, name: 'Home', item: 'https://edenrelics.co.uk' },
                { '@type': 'ListItem', position: 2, name: 'Vintage Care', item: 'https://edenrelics.co.uk/care' },
                { '@type': 'ListItem', position: 3, name: title, item: `https://edenrelics.co.uk${url}` },
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
