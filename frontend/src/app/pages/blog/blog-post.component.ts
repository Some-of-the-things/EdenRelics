import { Component, inject, input, signal, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { SeoService } from '../../services/seo.service';
import { environment } from '../../../environments/environment';
import { imageSrcAt, imageSrcset } from '../../utils/image-variant-loader';

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

@Component({
  selector: 'app-blog-post',
  imports: [RouterLink, DatePipe],
  templateUrl: './blog-post.component.html',
  styleUrl: './blog.component.scss',
})
export class BlogPostComponent implements OnInit {
  readonly slug = input.required<string>();
  private readonly http = inject(HttpClient);
  private readonly seo = inject(SeoService);
  readonly post = signal<BlogPost | null>(null);
  readonly error = signal(false);
  readonly srcset = imageSrcset;
  readonly srcAt = imageSrcAt;

  ngOnInit(): void {
    this.http.get<BlogPost>(`${environment.apiUrl}/api/blog/${this.slug()}`).subscribe({
      next: (post) => {
        this.post.set(post);
        const postUrl = `https://edenrelics.co.uk/blog/${post.slug}`;
        this.seo.updateTags({
          title: post.title,
          description: post.excerpt ?? undefined,
          url: `/blog/${post.slug}`,
          image: post.featuredImageUrl ?? undefined,
          type: 'article',
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
      error: () => this.error.set(true),
    });
  }
}
