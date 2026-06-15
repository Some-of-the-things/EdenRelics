import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
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

interface FinderResult {
  fabricName: string;
  fabricSlug: string;
  issueName: string;
  issueSlug: string;
  safety: string;
  shortAnswer: string;
  method: string;
  isGeneral: boolean;
}

interface IdentifyGuess {
  name: string;
  confidence: number;
  fabricSlug: string | null;
}

interface IdentifyResult {
  guesses: IdentifyGuess[];
  note: string;
}

@Component({
  selector: 'app-care-hub',
  imports: [RouterLink, FormsModule],
  templateUrl: './care-hub.component.html',
  styleUrl: './care-hub.component.scss',
})
export class CareHubComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly seo = inject(SeoService);

  readonly index = signal<CareIndex>({ fabrics: [], issues: [] });

  // Interactive finder (tool layer — not indexed)
  finderFabric = '';
  finderIssue = '';
  readonly finderResult = signal<FinderResult | null>(null);
  readonly finderLoading = signal(false);
  readonly finderError = signal('');

  runFinder(): void {
    if (!this.finderFabric || !this.finderIssue) {
      return;
    }
    this.finderLoading.set(true);
    this.finderError.set('');
    this.finderResult.set(null);
    const params = `fabric=${encodeURIComponent(this.finderFabric)}&issue=${encodeURIComponent(this.finderIssue)}`;
    this.http.get<FinderResult>(`${environment.apiUrl}/api/care/finder?${params}`).subscribe({
      next: (r) => {
        this.finderResult.set(r);
        this.finderLoading.set(false);
      },
      error: () => {
        this.finderError.set('Sorry — we couldn’t find guidance for that combination.');
        this.finderLoading.set(false);
      },
    });
  }

  // Image-based fabric identification (assistive)
  readonly identifyLoading = signal(false);
  readonly identifyError = signal('');
  readonly identifyResult = signal<IdentifyResult | null>(null);

  onPhotoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    if (file.size > 6 * 1024 * 1024) {
      this.identifyError.set('That image is too large (max ~5MB).');
      return;
    }
    this.identifyError.set('');
    this.identifyResult.set(null);
    this.identifyLoading.set(true);

    const reader = new FileReader();
    reader.onload = () => {
      const dataUrl = reader.result as string;
      const base64 = dataUrl.slice(dataUrl.indexOf(',') + 1);
      const mediaType = file.type || 'image/jpeg';
      this.http
        .post<IdentifyResult>(`${environment.apiUrl}/api/care/identify`, { imageBase64: base64, mediaType })
        .subscribe({
          next: (r) => {
            this.identifyResult.set(r);
            this.identifyLoading.set(false);
          },
          error: (e) => {
            this.identifyError.set(
              e.status === 429
                ? 'Too many tries just now — please wait a minute and try again.'
                : 'Sorry, we couldn’t identify that image.',
            );
            this.identifyLoading.set(false);
          },
        });
    };
    reader.readAsDataURL(file);
    input.value = '';
  }

  useGuess(slug: string): void {
    this.finderFabric = slug;
  }

  confidencePct(confidence: number): number {
    return Math.round(confidence * 100);
  }

  safetyLabel(safety: string): string {
    switch (safety) {
      case 'Safe':
        return 'Safe to try at home';
      case 'WithCaution':
        return 'Proceed with caution';
      case 'DoNotAttempt':
        return 'Don’t attempt at home';
      case 'SeeProfessional':
        return 'See a professional';
      default:
        return '';
    }
  }

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
