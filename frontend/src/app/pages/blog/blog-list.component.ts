import {
  Component,
  inject,
  signal,
  OnInit,
  PLATFORM_ID,
  ChangeDetectionStrategy,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe, isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { SeoService } from '../../services/seo.service';
import { environment } from '../../../environments/environment';
import { imageSrcAt, imageSrcset } from '../../utils/image-variant-loader';

interface BlogSummary {
  id: string;
  title: string;
  slug: string;
  excerpt: string | null;
  featuredImageUrl: string | null;
  author: string | null;
  publishedAtUtc: string | null;
}

@Component({
  selector: 'app-blog-list',
  imports: [RouterLink, DatePipe],
  templateUrl: './blog-list.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './blog.component.scss',
})
export class BlogListComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly seo = inject(SeoService);
  readonly posts = signal<BlogSummary[]>([]);
  readonly srcset = imageSrcset;
  readonly srcAt = imageSrcAt;

  private readonly platformId = inject(PLATFORM_ID);

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Blog',
      description: 'Vintage fashion tips, styling guides and stories from Eden Relics.',
      url: '/blog',
    });
    this.http.get<BlogSummary[]>(`${environment.apiUrl}/api/blog`).subscribe({
      next: (posts) => {
        this.posts.set(posts);
        this.setBlogJsonLd(posts);
      },
    });
  }

  /**
   * /blog was the only page type shipping no structured data at all, while the
   * posts it links to and every other hub had some. Blog + ItemList names the
   * page as a publication and hands crawlers the post list with its dates,
   * rather than leaving them to infer it from the markup.
   */
  private setBlogJsonLd(posts: BlogSummary[]): void {
    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@graph': [
        {
          '@type': 'Blog',
          '@id': 'https://edenrelics.co.uk/blog',
          name: 'The Eden Relics Journal',
          description:
            'Vintage fashion tips, styling guides and stories from Eden Relics.',
          url: 'https://edenrelics.co.uk/blog',
          publisher: {
            '@type': 'Organization',
            name: 'Eden Relics',
            url: 'https://edenrelics.co.uk',
          },
          blogPost: posts.map((p) => ({
            '@type': 'BlogPosting',
            headline: p.title,
            url: `https://edenrelics.co.uk/blog/${p.slug}`,
            datePublished: p.publishedAtUtc ?? undefined,
            author: { '@type': 'Person', name: p.author ?? 'Eden Relics' },
            ...(p.excerpt ? { description: p.excerpt } : {}),
            ...(p.featuredImageUrl ? { image: p.featuredImageUrl } : {}),
          })),
        },
        {
          '@type': 'ItemList',
          itemListOrder: 'https://schema.org/ItemListOrderDescending',
          numberOfItems: posts.length,
          itemListElement: posts.map((p, i) => ({
            '@type': 'ListItem',
            position: i + 1,
            url: `https://edenrelics.co.uk/blog/${p.slug}`,
            name: p.title,
          })),
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
              name: 'Journal',
              item: 'https://edenrelics.co.uk/blog',
            },
          ],
        },
      ],
    });
  }
}
