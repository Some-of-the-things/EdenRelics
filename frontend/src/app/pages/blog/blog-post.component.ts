import { Component, computed, inject, input, signal, OnInit, RESPONSE_INIT } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { SeoService } from '../../services/seo.service';
import { environment } from '../../../environments/environment';
import { imageSrcAt, imageSrcset } from '../../utils/image-variant-loader';
import { ShareButtonsComponent } from '../../components/share-buttons/share-buttons.component';
import { VintageSizeConverterComponent } from '../../components/vintage-size-converter/vintage-size-converter.component';

interface BlogPost {
  id: string;
  title: string;
  slug: string;
  content: string;
  excerpt: string | null;
  featuredImageUrl: string | null;
  author: string | null;
  publishedAtUtc: string | null;
}

interface BlogPostSummary {
  id: string;
  title: string;
  slug: string;
  excerpt: string | null;
  featuredImageUrl: string | null;
  author: string | null;
  publishedAtUtc: string | null;
}

@Component({
  selector: 'app-blog-post',
  imports: [RouterLink, DatePipe, ShareButtonsComponent, VintageSizeConverterComponent],
  templateUrl: './blog-post.component.html',
  styleUrl: './blog.component.scss',
})
export class BlogPostComponent implements OnInit {
  /** Placeholder authors drop into post HTML to embed the size converter. */
  private static readonly CONVERTER_TOKEN = '<!--SIZE_CONVERTER-->';

  readonly slug = input.required<string>();
  /** Bound from the /blog/preview/:slug route — fetches drafts via the admin-only preview endpoint and marks the page noindex. */
  readonly preview = input(false);
  private readonly http = inject(HttpClient);
  private readonly seo = inject(SeoService);
  // Present only during server render; null in the browser.
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });
  readonly post = signal<BlogPost | null>(null);
  readonly error = signal(false);
  readonly olderPost = signal<BlogPostSummary | null>(null);
  readonly newerPost = signal<BlogPostSummary | null>(null);
  readonly srcset = imageSrcset;
  readonly srcAt = imageSrcAt;

  /** Splits post content around the size-converter placeholder, if present. */
  readonly hasConverter = computed(
    () => this.post()?.content.includes(BlogPostComponent.CONVERTER_TOKEN) ?? false,
  );
  readonly contentBefore = computed(() => {
    const content = this.post()?.content ?? '';
    const idx = content.indexOf(BlogPostComponent.CONVERTER_TOKEN);
    return idx >= 0 ? content.slice(0, idx) : content;
  });
  readonly contentAfter = computed(() => {
    const content = this.post()?.content ?? '';
    const idx = content.indexOf(BlogPostComponent.CONVERTER_TOKEN);
    return idx >= 0 ? content.slice(idx + BlogPostComponent.CONVERTER_TOKEN.length) : '';
  });

  ngOnInit(): void {
    // Older/newer navigation only makes sense for published posts; a draft
    // preview isn't in the public list, so skip it in preview mode.
    if (!this.preview()) {
      this.http.get<BlogPostSummary[]>(`${environment.apiUrl}/api/blog`).subscribe({
        next: (posts) => {
          // API returns posts newest-first by publishedAtUtc.
          const idx = posts.findIndex((p) => p.slug === this.slug());
          if (idx >= 0) {
            this.newerPost.set(idx > 0 ? posts[idx - 1] : null);
            this.olderPost.set(idx < posts.length - 1 ? posts[idx + 1] : null);
          }
        },
        error: () => {},
      });
    }
    const postEndpoint = this.preview()
      ? `${environment.apiUrl}/api/blog/preview/${this.slug()}`
      : `${environment.apiUrl}/api/blog/${this.slug()}`;
    this.http.get<BlogPost>(postEndpoint).subscribe({
      next: (post) => {
        this.post.set(post);
        const postUrl = `https://edenrelics.co.uk/blog/${post.slug}`;
        this.seo.updateTags({
          title: this.preview() ? `[Preview] ${post.title}` : post.title,
          description: post.excerpt ?? undefined,
          url: `/blog/${post.slug}`,
          image: post.featuredImageUrl ?? undefined,
          type: 'article',
          hreflang: !this.preview(),
          noIndex: this.preview(),
        });

        const blogPosting: Record<string, unknown> = {
          '@type': 'BlogPosting',
          headline: post.title,
          mainEntityOfPage: {
            '@type': 'WebPage',
            '@id': postUrl,
          },
          author: {
            '@type': 'Person',
            name: post.author ?? 'Eden Relics',
          },
          publisher: {
            '@type': 'Organization',
            name: 'Eden Relics',
            logo: {
              '@type': 'ImageObject',
              url: 'https://edenrelics.co.uk/logo.png',
            },
          },
        };
        if (post.featuredImageUrl) {
          blogPosting['image'] = post.featuredImageUrl;
        }
        if (post.publishedAtUtc) {
          blogPosting['datePublished'] = post.publishedAtUtc;
        }
        if (post.excerpt) {
          blogPosting['description'] = post.excerpt;
        }

        this.seo.setJsonLd({
          '@context': 'https://schema.org',
          '@graph': [
            blogPosting,
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
                  name: 'Blog',
                  item: 'https://edenrelics.co.uk/blog',
                },
                {
                  '@type': 'ListItem',
                  position: 3,
                  name: post.title,
                  item: postUrl,
                },
              ],
            },
          ],
        });
      },
      error: () => {
        this.error.set(true);
        // Unknown blog slug — emit a real 404 for crawlers (server-only),
        // rather than a 200 soft-404. No-op in the browser.
        if (this.responseInit) {
          this.responseInit.status = 404;
        }
        this.seo.updateTags({ title: 'Post not found', noIndex: true });
      },
    });
  }
}
