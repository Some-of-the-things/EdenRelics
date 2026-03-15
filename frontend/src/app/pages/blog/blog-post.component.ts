import { Component, inject, input, signal, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { SeoService } from '../../services/seo.service';
import { environment } from '../../../environments/environment';

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

  ngOnInit(): void {
    this.http.get<BlogPost>(`${environment.apiUrl}/api/blog/${this.slug()}`).subscribe({
      next: (post) => {
        this.post.set(post);
        this.seo.updateTags({
          title: post.title,
          description: post.excerpt ?? undefined,
          url: `/blog/${post.slug}`,
          image: post.featuredImageUrl ?? undefined,
        });
      },
      error: () => this.error.set(true),
    });
  }
}
