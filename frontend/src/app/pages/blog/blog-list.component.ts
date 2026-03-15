import { Component, inject, signal, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { SeoService } from '../../services/seo.service';
import { environment } from '../../../environments/environment';

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
  styleUrl: './blog.component.scss',
})
export class BlogListComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly seo = inject(SeoService);
  readonly posts = signal<BlogSummary[]>([]);

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Blog',
      description: 'Vintage fashion tips, styling guides and stories from Eden Relics.',
      url: '/blog',
    });
    this.http.get<BlogSummary[]>(`${environment.apiUrl}/api/blog`).subscribe({
      next: (posts) => this.posts.set(posts),
    });
  }
}
