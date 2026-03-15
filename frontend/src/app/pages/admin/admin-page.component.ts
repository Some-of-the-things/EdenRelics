import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CurrencyPipe, DatePipe, TitleCasePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ProductStore } from '../../store/product.store';
import { Product } from '../../models/product.model';
import { AuthService } from '../../services/auth.service';
import { ProductService } from '../../services/product.service';
import {
  OrderAdminService,
  AdminOrder,
} from '../../services/order-admin.service';
import { environment } from '../../../environments/environment';
import { BrandingService, Branding } from '../../services/branding.service';
import { ContentService } from '../../services/content.service';

interface AccountsSummary {
  totalRevenue: number;
  totalCost: number;
  totalProfit: number;
  marginPercent: number;
  totalOrders: number;
  totalItemsSold: number;
  averageOrderValue: number;
  revenueByMonth: { month: string; revenue: number; cost: number; profit: number; orders: number; itemsSold: number }[];
  revenueByCategory: { category: string; revenue: number; cost: number; profit: number; itemsSold: number }[];
  revenueByEra: { era: string; revenue: number; cost: number; profit: number; itemsSold: number }[];
  inventory: { inStock: number; outOfStock: number; retailValue: number; costValue: number };
  ordersByStatus: { status: string; count: number }[];
}

interface SeoHeading {
  level: number;
  text: string;
}

interface SeoOpenGraph {
  title: string | null;
  description: string | null;
  image: string | null;
}

interface KeywordSuggestion {
  keyword: string;
  score: number;
  frequency: number;
}

interface TrackedKeyword {
  id: string;
  keyword: string;
  pageUrl: string;
  lastPosition: number | null;
  lastCheckedUtc: string | null;
  notes: string | null;
}

interface SeoResult {
  url: string;
  title: string | null;
  metaDescription: string | null;
  metaKeywords: string | null;
  canonicalUrl: string | null;
  openGraph: SeoOpenGraph;
  headings: SeoHeading[];
  wordCount: number;
  imageCount: number;
  imagesMissingAlt: number;
  internalLinks: number;
  externalLinks: number;
  issues: string[];
  warnings: string[];
  passed: string[];
  suggestedKeywords: KeywordSuggestion[];
}

@Component({
  selector: 'app-admin-page',
  imports: [FormsModule, CurrencyPipe, TitleCasePipe, DatePipe],
  templateUrl: './admin-page.component.html',
  styleUrl: './admin-page.component.scss',
})
export class AdminPageComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);
  private readonly productService = inject(ProductService);
  private readonly orderService = inject(OrderAdminService);
  readonly store = inject(ProductStore);

  private readonly brandingService = inject(BrandingService);
  private readonly contentService = inject(ContentService);
  readonly activeTab = signal<'products' | 'orders' | 'accounts' | 'seo' | 'instagram' | 'branding' | 'content' | 'marketplace' | 'blog'>('products');
  readonly showForm = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly imagePreview = signal<string | null>(null);
  readonly uploading = signal(false);
  readonly uploadError = signal('');

  // Orders
  readonly orders = signal<AdminOrder[]>([]);
  readonly ordersLoading = signal(false);
  readonly ordersError = signal('');
  readonly statusFilter = signal<string>('all');

  readonly statuses = ['Pending', 'Paid', 'Processing', 'Shipped', 'Delivered', 'Cancelled'];

  // SEO
  seoUrl = 'https://edenrelics.co.uk';
  readonly seoResult = signal<SeoResult | null>(null);
  readonly seoLoading = signal(false);
  readonly seoError = signal('');

  // Tracked keywords
  readonly trackedKeywords = signal<TrackedKeyword[]>([]);
  readonly trackedLoading = signal(false);
  newKeyword = '';
  newKeywordUrl = 'https://edenrelics.co.uk';
  newKeywordPosition: number | null = null;

  // Content
  contentForm: Record<string, string> = {};
  readonly contentSaving = signal(false);
  readonly contentSuccess = signal('');
  readonly contentError = signal('');

  readonly contentSections = [
    {
      title: 'Hero Section',
      fields: [
        { key: 'home.hero.eyebrow', label: 'Eyebrow Text', type: 'text' },
        { key: 'home.hero.title', label: 'Title', type: 'text' },
        { key: 'home.hero.subtitle', label: 'Subtitle', type: 'text' },
      ],
    },
    {
      title: 'About Section',
      fields: [
        { key: 'home.about.title', label: 'Section Title', type: 'text' },
        { key: 'home.about.card1.title', label: 'Card 1 Title', type: 'text' },
        { key: 'home.about.card1.text', label: 'Card 1 Text', type: 'textarea' },
        { key: 'home.about.card2.title', label: 'Card 2 Title', type: 'text' },
        { key: 'home.about.card2.text', label: 'Card 2 Text', type: 'textarea' },
        { key: 'home.about.card3.title', label: 'Card 3 Title', type: 'text' },
        { key: 'home.about.card3.text', label: 'Card 3 Text', type: 'textarea' },
        { key: 'home.about.card4.title', label: 'Card 4 Title', type: 'text' },
        { key: 'home.about.card4.text', label: 'Card 4 Text', type: 'textarea' },
      ],
    },
    {
      title: 'Footer',
      fields: [
        { key: 'footer.tagline', label: 'Tagline', type: 'text' },
        { key: 'footer.company.line1', label: 'Company Line 1', type: 'text' },
        { key: 'footer.company.line2', label: 'Company Line 2', type: 'text' },
        { key: 'footer.company.line3', label: 'Company Line 3', type: 'text' },
        { key: 'footer.contact.email', label: 'Email', type: 'text' },
        { key: 'footer.contact.phone', label: 'Phone', type: 'text' },
        { key: 'footer.contact.address', label: 'Address', type: 'textarea' },
      ],
    },
    {
      title: 'Contact Page',
      fields: [
        { key: 'contact.title', label: 'Title', type: 'text' },
        { key: 'contact.subtitle', label: 'Subtitle', type: 'text' },
      ],
    },
  ];

  // Branding
  brandingForm: Branding = {
    logoUrl: null, bgPrimary: '#FAF9F7', bgSecondary: '#F3F1EE', bgCard: '#FFFFFF',
    bgDark: '#2E2E2E', textPrimary: '#2E2E2E', textSecondary: '#5A5858',
    textMuted: '#706E6C', textInverse: '#FAF9F7', accent: '#8F1D31',
    accentHover: '#6E1526', fontDisplay: 'Playfair Display', fontBody: 'Work Sans',
  };
  readonly brandingSaving = signal(false);
  readonly brandingSuccess = signal('');
  readonly brandingError = signal('');
  readonly logoUploading = signal(false);
  readonly logoPreview = signal<string | null>(null);

  readonly fontOptions = [
    'Playfair Display', 'Work Sans', 'Inter', 'Lora', 'Merriweather',
    'Montserrat', 'Open Sans', 'Poppins', 'Raleway', 'Roboto',
    'Source Sans 3', 'Nunito', 'PT Serif', 'Crimson Text', 'Libre Baskerville',
    'DM Sans', 'Outfit', 'Cormorant Garamond', 'Josefin Sans', 'EB Garamond',
  ];

  // Instagram
  igCaption = '';
  igImageUrl = '';
  readonly igPosting = signal(false);
  readonly igResult = signal<string | null>(null);
  readonly igError = signal('');
  readonly igConfigured = signal<boolean | null>(null);
  // Blog
  readonly blogPosts = signal<{ id: string; title: string; slug: string; published: boolean; publishedAtUtc: string | null; createdAtUtc: string }[]>([]);
  readonly blogLoading = signal(false);
  readonly showBlogForm = signal(false);
  readonly editingBlogId = signal<string | null>(null);
  readonly blogUploading = signal(false);
  readonly blogError = signal('');
  readonly blogSuccess = signal('');
  blogForm = { title: '', content: '', excerpt: '', featuredImageUrl: '', author: 'Teodora Carter', published: false };

  // Marketplace
  readonly pendingRemovals = signal<{ listingId: string; productId: string; productName: string; platform: string; externalUrl: string | null }[]>([]);
  readonly marketplaceProducts = signal<{ product: Product; listings: { id: string; platform: string; status: string; externalUrl: string | null }[] }[]>([]);
  readonly marketplaceLoading = signal(false);
  readonly generatedListing = signal<{ title: string; description: string; price: number; imageUrl: string } | null>(null);
  readonly etsyConfigured = signal<boolean | null>(null);
  readonly marketplaceError = signal('');
  readonly marketplaceSuccess = signal('');

  // Accounts
  readonly accountsSummary = signal<AccountsSummary | null>(null);
  readonly accountsLoading = signal(false);
  readonly accountsError = signal('');

  postToInstagram = false;

  form: Omit<Product, 'id'> = this.emptyForm();

  switchTab(tab: 'products' | 'orders' | 'accounts' | 'seo' | 'instagram' | 'branding' | 'content' | 'marketplace' | 'blog'): void {
    this.activeTab.set(tab);
    if (tab === 'orders' && this.orders().length === 0) {
      this.loadOrders();
    }
    if (tab === 'seo' && this.trackedKeywords().length === 0) {
      this.loadTrackedKeywords();
    }
    if (tab === 'instagram' && this.igConfigured() === null) {
      this.checkInstagramStatus();
    }
    if (tab === 'branding') {
      this.loadBranding();
    }
    if (tab === 'accounts') {
      this.loadAccounts();
    }
    if (tab === 'content') {
      this.loadContent();
    }
    if (tab === 'marketplace') {
      this.loadMarketplace();
    }
    if (tab === 'blog') {
      this.loadBlogPosts();
    }
  }

  loadBlogPosts(): void {
    this.blogLoading.set(true);
    this.http.get<any[]>(`${environment.apiUrl}/api/blog/admin/all`).subscribe({
      next: (posts) => {
        this.blogPosts.set(posts);
        this.blogLoading.set(false);
      },
      error: () => this.blogLoading.set(false),
    });
  }

  openBlogForm(): void {
    this.editingBlogId.set(null);
    this.blogForm = { title: '', content: '', excerpt: '', featuredImageUrl: '', author: 'Teodora Carter', published: false };
    this.blogError.set('');
    this.blogSuccess.set('');
    this.showBlogForm.set(true);
  }

  editBlogPost(id: string): void {
    this.http.get<any>(`${environment.apiUrl}/api/blog/admin/${id}`).subscribe({
      next: (post) => {
        this.editingBlogId.set(id);
        this.blogForm = {
          title: post.title,
          content: post.content,
          excerpt: post.excerpt ?? '',
          featuredImageUrl: post.featuredImageUrl ?? '',
          author: post.author ?? '',
          published: post.published,
        };
        this.blogError.set('');
        this.blogSuccess.set('');
        this.showBlogForm.set(true);
      },
    });
  }

  saveBlogPost(): void {
    this.blogError.set('');
    const id = this.editingBlogId();
    const req = id
      ? this.http.put(`${environment.apiUrl}/api/blog/${id}`, this.blogForm)
      : this.http.post(`${environment.apiUrl}/api/blog`, this.blogForm);

    req.subscribe({
      next: () => {
        this.blogSuccess.set(id ? 'Post updated.' : 'Post created.');
        this.showBlogForm.set(false);
        this.loadBlogPosts();
      },
      error: (err) => this.blogError.set(err.error?.message ?? 'Failed to save post.'),
    });
  }

  deleteBlogPost(id: string): void {
    if (!confirm('Delete this post?')) return;
    this.http.delete(`${environment.apiUrl}/api/blog/${id}`).subscribe({
      next: () => this.loadBlogPosts(),
    });
  }

  onBlogImageUpload(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.blogUploading.set(true);
    const formData = new FormData();
    formData.append('file', file);
    this.http.post<{ imageUrl: string }>(`${environment.apiUrl}/api/blog/upload-image`, formData).subscribe({
      next: (res) => {
        this.blogForm.content += `\n<img src="${res.imageUrl}" alt="" />\n`;
        this.blogUploading.set(false);
      },
      error: () => this.blogUploading.set(false),
    });
  }

  onFeaturedImageUpload(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.blogUploading.set(true);
    const formData = new FormData();
    formData.append('file', file);
    this.http.post<{ imageUrl: string }>(`${environment.apiUrl}/api/blog/upload-image`, formData).subscribe({
      next: (res) => {
        this.blogForm.featuredImageUrl = res.imageUrl;
        this.blogUploading.set(false);
      },
      error: () => this.blogUploading.set(false),
    });
  }

  insertHeading(level: 2 | 3): void {
    this.blogForm.content += `\n<h${level}>Heading</h${level}>\n`;
  }

  loadMarketplace(): void {
    this.marketplaceLoading.set(true);
    this.marketplaceError.set('');

    // Load pending removals, product listings, and Etsy status in parallel
    this.http.get<any[]>(`${environment.apiUrl}/api/marketplace/pending-removals`).subscribe({
      next: (r) => this.pendingRemovals.set(r),
    });

    this.http.get<{ configured: boolean }>(`${environment.apiUrl}/api/marketplace/etsy/status`).subscribe({
      next: (r) => this.etsyConfigured.set(r.configured),
    });

    // Load listings for all products
    const products = this.store.products();
    if (products.length === 0) {
      this.marketplaceLoading.set(false);
      return;
    }

    let loaded = 0;
    const result: { product: Product; listings: any[] }[] = [];
    for (const product of products) {
      this.http.get<any[]>(`${environment.apiUrl}/api/marketplace/listings/${product.id}`).subscribe({
        next: (listings) => {
          result.push({ product, listings });
          loaded++;
          if (loaded === products.length) {
            this.marketplaceProducts.set(result.sort((a, b) => a.product.name.localeCompare(b.product.name)));
            this.marketplaceLoading.set(false);
          }
        },
        error: () => {
          loaded++;
          if (loaded === products.length) {
            this.marketplaceProducts.set(result);
            this.marketplaceLoading.set(false);
          }
        },
      });
    }
  }

  listOnPlatform(productId: string, platform: string): void {
    if (platform === 'Etsy') {
      this.http.post<any>(`${environment.apiUrl}/api/marketplace/etsy/create-listing`, { productId }).subscribe({
        next: () => {
          this.marketplaceSuccess.set(`Listed on Etsy successfully.`);
          this.loadMarketplace();
        },
        error: (err) => this.marketplaceError.set(err.error?.message ?? 'Failed to list on Etsy.'),
      });
    } else {
      // For Depop/Vinted, mark as listed manually and show copy text
      this.http.post<any>(`${environment.apiUrl}/api/marketplace/listings`, {
        productId, platform, externalListingId: null, externalUrl: null,
      }).subscribe({
        next: () => this.loadMarketplace(),
      });
    }
  }

  generateListing(productId: string, platform: string): void {
    this.generatedListing.set(null);
    this.http.get<any>(`${environment.apiUrl}/api/marketplace/generate-listing/${productId}?platform=${platform}`).subscribe({
      next: (r) => this.generatedListing.set(r),
    });
  }

  markSoldOn(productId: string, platform: string): void {
    this.http.post<any>(`${environment.apiUrl}/api/marketplace/mark-sold/${productId}`, { soldOn: platform }).subscribe({
      next: (r) => {
        this.marketplaceSuccess.set(r.message);
        this.loadMarketplace();
      },
      error: (err) => this.marketplaceError.set(err.error?.message ?? 'Failed to mark as sold.'),
    });
  }

  acknowledgeRemoval(listingId: string): void {
    this.http.post(`${environment.apiUrl}/api/marketplace/acknowledge-removal/${listingId}`, {}).subscribe({
      next: () => {
        this.pendingRemovals.update(list => list.filter(r => r.listingId !== listingId));
      },
    });
  }

  copyToClipboard(text: string): void {
    navigator.clipboard.writeText(text);
  }

  loadAccounts(): void {
    this.accountsLoading.set(true);
    this.accountsError.set('');
    this.http.get<AccountsSummary>(`${environment.apiUrl}/api/accounts/summary`).subscribe({
      next: (data) => {
        this.accountsSummary.set(data);
        this.accountsLoading.set(false);
      },
      error: () => {
        this.accountsError.set('Failed to load accounts data.');
        this.accountsLoading.set(false);
      },
    });
  }

  loadContent(): void {
    this.http.get<Record<string, string>>(`${environment.apiUrl}/api/content`).subscribe({
      next: (c) => {
        this.contentForm = { ...c };
      },
    });
  }

  saveContent(): void {
    this.contentSaving.set(true);
    this.contentError.set('');
    this.contentSuccess.set('');

    this.http.put<Record<string, string>>(`${environment.apiUrl}/api/content`, this.contentForm).subscribe({
      next: (c) => {
        this.contentSaving.set(false);
        this.contentSuccess.set('Content saved. Changes are live.');
        this.contentService.setAll(c);
      },
      error: (err) => {
        this.contentSaving.set(false);
        this.contentError.set(err.error?.message ?? 'Failed to save content.');
      },
    });
  }

  loadBranding(): void {
    this.http.get<Branding>(`${environment.apiUrl}/api/branding`).subscribe({
      next: (b) => {
        this.brandingForm = { ...b };
        this.logoPreview.set(b.logoUrl);
      },
    });
  }

  saveBranding(): void {
    this.brandingSaving.set(true);
    this.brandingError.set('');
    this.brandingSuccess.set('');

    this.http.put<Branding>(`${environment.apiUrl}/api/branding`, this.brandingForm).subscribe({
      next: (b) => {
        this.brandingSaving.set(false);
        this.brandingSuccess.set('Branding saved. Changes are live.');
        this.brandingService.apply(b);
      },
      error: (err) => {
        this.brandingSaving.set(false);
        this.brandingError.set(err.error?.message ?? 'Failed to save branding.');
      },
    });
  }

  onLogoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.logoUploading.set(true);
    this.logoPreview.set(URL.createObjectURL(file));

    const formData = new FormData();
    formData.append('file', file);

    this.http.post<{ logoUrl: string; faviconUrl: string | null }>(
      `${environment.apiUrl}/api/branding/upload-logo`, formData
    ).subscribe({
      next: (res) => {
        this.brandingForm.logoUrl = res.logoUrl;
        this.logoUploading.set(false);
      },
      error: () => {
        this.logoUploading.set(false);
        this.logoPreview.set(this.brandingForm.logoUrl);
      },
    });
  }

  previewBranding(): void {
    this.brandingService.apply(this.brandingForm);
  }

  checkInstagramStatus(): void {
    this.http.get<{ configured: boolean }>(`${environment.apiUrl}/api/instagram/status`).subscribe({
      next: (res) => this.igConfigured.set(res.configured),
      error: () => this.igConfigured.set(false),
    });
  }

  postInstagram(imageUrl?: string, caption?: string): void {
    const url = imageUrl ?? this.igImageUrl;
    const cap = caption ?? this.igCaption;
    if (!url || !cap) return;

    this.igPosting.set(true);
    this.igError.set('');
    this.igResult.set(null);

    this.http
      .post<{ mediaId: string; message: string }>(`${environment.apiUrl}/api/instagram/post`, {
        imageUrl: url,
        caption: cap,
      })
      .subscribe({
        next: (res) => {
          this.igResult.set(res.message);
          this.igPosting.set(false);
          this.igCaption = '';
          this.igImageUrl = '';
        },
        error: (err) => {
          this.igError.set(err.error?.message ?? 'Failed to post to Instagram.');
          this.igPosting.set(false);
        },
      });
  }

  loadTrackedKeywords(): void {
    this.trackedLoading.set(true);
    this.http.get<TrackedKeyword[]>(`${environment.apiUrl}/api/seo/keywords`).subscribe({
      next: (keywords) => {
        this.trackedKeywords.set(keywords);
        this.trackedLoading.set(false);
      },
      error: () => this.trackedLoading.set(false),
    });
  }

  trackKeyword(keyword?: string): void {
    const kw = keyword ?? this.newKeyword;
    if (!kw.trim()) return;
    this.http
      .post<TrackedKeyword>(`${environment.apiUrl}/api/seo/keywords`, {
        keyword: kw,
        pageUrl: this.newKeywordUrl,
        position: this.newKeywordPosition,
      })
      .subscribe({
        next: (created) => {
          this.trackedKeywords.update((list) => [created, ...list]);
          this.newKeyword = '';
          this.newKeywordPosition = null;
        },
      });
  }

  updateKeywordPosition(kw: TrackedKeyword, position: number): void {
    this.http
      .put<TrackedKeyword>(`${environment.apiUrl}/api/seo/keywords/${kw.id}`, { position })
      .subscribe({
        next: (updated) => {
          this.trackedKeywords.update((list) =>
            list.map((k) => (k.id === updated.id ? updated : k))
          );
        },
      });
  }

  removeTrackedKeyword(id: string): void {
    this.http.delete(`${environment.apiUrl}/api/seo/keywords/${id}`).subscribe({
      next: () => {
        this.trackedKeywords.update((list) => list.filter((k) => k.id !== id));
      },
    });
  }

  readonly checkingAll = signal(false);

  checkAllKeywords(): void {
    this.checkingAll.set(true);
    this.http
      .post<TrackedKeyword[]>(`${environment.apiUrl}/api/seo/keywords/check-all`, {})
      .subscribe({
        next: (keywords) => {
          this.trackedKeywords.set(keywords);
          this.checkingAll.set(false);
        },
        error: () => this.checkingAll.set(false),
      });
  }

  analyseSeo(): void {
    this.seoLoading.set(true);
    this.seoError.set('');
    this.seoResult.set(null);

    this.http
      .post<SeoResult>(`${environment.apiUrl}/api/seo/analyse`, {
        url: this.seoUrl,
      })
      .subscribe({
        next: (result) => {
          this.seoResult.set(result);
          this.seoLoading.set(false);
        },
        error: (err) => {
          this.seoError.set(
            err.error?.message ?? 'Failed to analyse. Check the URL.'
          );
          this.seoLoading.set(false);
        },
      });
  }

  loadOrders(): void {
    this.ordersLoading.set(true);
    this.ordersError.set('');
    this.orderService.getAll().subscribe({
      next: (orders) => {
        this.orders.set(orders);
        this.ordersLoading.set(false);
      },
      error: () => {
        this.ordersError.set('Failed to load orders.');
        this.ordersLoading.set(false);
      },
    });
  }

  get filteredOrders(): AdminOrder[] {
    const filter = this.statusFilter();
    if (filter === 'all') return this.orders();
    return this.orders().filter((o) => o.status === filter);
  }

  updateOrderStatus(order: AdminOrder, status: string): void {
    this.orderService.updateStatus(order.id, status).subscribe({
      next: (updated) => {
        this.orders.update((orders) =>
          orders.map((o) => (o.id === updated.id ? updated : o))
        );
      },
      error: () => this.ordersError.set('Failed to update order status.'),
    });
  }

  openForm(): void {
    this.editingId.set(null);
    this.imagePreview.set(null);
    this.uploadError.set('');
    this.form = this.emptyForm();
    this.showForm.set(true);
  }

  edit(product: Product): void {
    this.editingId.set(product.id);
    this.imagePreview.set(null);
    this.uploadError.set('');
    this.form = {
      name: product.name,
      description: product.description,
      price: product.price,
      costPrice: product.costPrice ?? 0,
      supplier: product.supplier ?? '',
      era: product.era,
      category: product.category,
      size: product.size,
      condition: product.condition,
      imageUrl: product.imageUrl,
      additionalImageUrls: [...(product.additionalImageUrls ?? [])],
      inStock: product.inStock,
    };
    this.showForm.set(true);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.uploadError.set('');
    this.imagePreview.set(URL.createObjectURL(file));
    this.uploading.set(true);

    this.productService.uploadImage(file).subscribe({
      next: (res) => {
        this.form.imageUrl = res.imageUrl;
        this.uploading.set(false);
      },
      error: (err) => {
        this.uploading.set(false);
        this.imagePreview.set(null);
        this.uploadError.set(
          err.error?.error ?? 'Upload failed. Please try again.'
        );
      },
    });
  }

  save(): void {
    const id = this.editingId();
    if (id) {
      this.store.updateProduct(id, this.form);
    } else {
      this.store.addProduct(this.form);
      if (this.postToInstagram && this.form.imageUrl) {
        const caption = `✨ New arrival: ${this.form.name}\n\n${this.form.description}\n\n💷 £${this.form.price}\n📏 Size ${this.form.size} | ${this.form.era}\n\nShop now at edenrelics.co.uk\n\n#vintagefashion #vintagedress #${this.form.category} #sustainablefashion #edenrelics`;
        this.postInstagram(this.form.imageUrl, caption);
      }
    }
    this.postToInstagram = false;
    this.closeForm();
  }

  remove(id: string): void {
    if (confirm('Delete this product?')) {
      this.store.removeProduct(id);
    }
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }

  closeForm(): void {
    this.showForm.set(false);
  }

  private emptyForm(): Omit<Product, 'id'> {
    return {
      name: '',
      description: '',
      price: 0,
      costPrice: 0,
      supplier: '',
      era: '',
      category: '70s',
      size: '10',
      condition: 'good',
      imageUrl: '',
      additionalImageUrls: [] as string[],
      inStock: true,
    };
  }

  onAdditionalImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.uploading.set(true);
    this.productService.uploadImage(file).subscribe({
      next: (res) => {
        this.form.additionalImageUrls = [...(this.form.additionalImageUrls ?? []), res.imageUrl];
        this.uploading.set(false);
      },
      error: () => this.uploading.set(false),
    });
  }

  removeAdditionalImage(index: number): void {
    this.form.additionalImageUrls = this.form.additionalImageUrls?.filter((_, i) => i !== index) ?? [];
  }
}
