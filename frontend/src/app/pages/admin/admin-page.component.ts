import { Component, computed, ElementRef, inject, OnInit, signal, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CurrencyPipe, DatePipe, DecimalPipe, TitleCasePipe } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, forkJoin } from 'rxjs';
import { ProductStore } from '../../store/product.store';
import { Product, ProductStatus } from '../../models/product.model';
import { filterAdminProducts, productStatusLabel, resolveProductStatus, sortAdminProducts, ProductSort } from '../../utils/product-status';
import { SeoService } from '../../services/seo.service';
import { AuthService } from '../../services/auth.service';
import { ProductService } from '../../services/product.service';
import {
  OrderAdminService,
  AdminOrder,
} from '../../services/order-admin.service';
import { environment } from '../../../environments/environment';
import { BrandingService, Branding } from '../../services/branding.service';
import { ContentService } from '../../services/content.service';
import {
  OffsiteSaleService,
  OffsiteSale,
  CreateOffsiteSale,
} from '../../services/offsite-sale.service';
import { AdminReview, ReviewsService } from '../../services/reviews.service';
import { AdminCalendarComponent } from './admin-calendar.component';
import { AdminAccountingComponent } from './admin-accounting.component';
import { AdminCareComponent } from './admin-care.component';

interface AdminUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  emailVerified: boolean;
  mfaEnabled: boolean;
  externalProvider: string | null;
  createdAtUtc: string;
  orderCount: number;
  mailingList: boolean;
  favourites: string[];
}

interface ViewRecord {
  createdAtUtc: string;
  channel: string;
  country: string | null;
  referrer: string | null;
  deviceType: string | null;
  operatingSystem: string | null;
  screenResolution: string | null;
  utmSource: string | null;
  utmMedium: string | null;
  utmCampaign: string | null;
}

interface ViewAnalytics {
  totalViews: number;
  trackedViews: number;
  views: ViewRecord[];
  byChannel: { channel: string; count: number }[];
  byCountry: { country: string; count: number }[];
  topReferrers: { referrer: string; count: number }[];
  viewsByDate: { date: string; count: number }[];
  byDevice: { device: string; count: number }[];
  byOs: { os: string; count: number }[];
}

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

interface FinanceTransaction {
  id: string;
  date: string;
  description: string;
  amount: number;
  category: string;
  platform: string | null;
  reference: string | null;
  receiptUrl: string | null;
  notes: string | null;
  createdAtUtc: string;
}

interface FinanceSummary {
  totalIncome: number;
  totalExpenses: number;
  totalProfit: number;
  transactionCount: number;
  byMonth: {
    month: string;
    income: number;
    expenses: number;
    profit: number;
    count: number;
    byCategory: { category: string; total: number; count: number }[];
    byPlatform: { platform: string; total: number; count: number }[];
  }[];
}

interface MonzoTransaction {
  id: string;
  monzoId: string;
  date: string;
  description: string;
  amount: number;
  currency: string;
  category: string;
  merchantName: string | null;
  merchantLogo: string | null;
  notes: string | null;
  tags: string | null;
  isLoad: boolean;
  declineReason: string | null;
  settledAt: string | null;
  userCategory: string | null;
  platform: string | null;
  receiptUrl: string | null;
  createdAtUtc: string;
}

interface MonzoBalance {
  balance: number;
  totalBalance: number;
  currency: string;
  spendToday: number;
}

interface MonzoSummary {
  totalIncome: number;
  totalExpenses: number;
  totalProfit: number;
  transactionCount: number;
  taggedCount: number;
  untaggedCount: number;
  byMonth: {
    month: string;
    income: number;
    expenses: number;
    profit: number;
    count: number;
    byCategory: { category: string; total: number; count: number }[];
    byPlatform: { platform: string; total: number; count: number }[];
  }[];
}

interface MonzoPot {
  id: string;
  name: string;
  balance: number;
  currency: string;
}

interface MonzoAnnotatePayload {
  notes: string;
  tags: string;
  userCategory: string;
  platform: string;
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

interface SeoHealthSnapshot {
  id: string;
  takenAtUtc: string;
  totalProducts: number;
  liveProducts: number;
  stockProducts: number;
  soldProducts: number;
  productsMissingImage: number;
  productsMissingDescription: number;
  productsMissingSlug: number;
  productsMissingSku: number;
  productsWithVideo: number;
  productsWithAdditionalImages: number;
  avgProductDescriptionWords: number;
  totalBlogPosts: number;
  publishedBlogPosts: number;
  blogPostsMissingFeaturedImage: number;
  blogPostsMissingExcerpt: number;
  avgBlogPostWords: number;
  sitemapUrlCount: number;
  sitemapImageEntryCount: number;
  trackedKeywords: number;
  trackedKeywordsWithPosition: number;
  avgKeywordPosition: number;
  keywordsInTop10: number;
  keywordsInTop3: number;
}

interface TrafficStatus {
  gscConfigured: boolean;
  ga4Configured: boolean;
  gscLastDate: string | null;
  ga4LastDate: string | null;
  gscStale: boolean;
  ga4Stale: boolean;
}

interface TotalsSummary {
  gscClicks: number;
  gscImpressions: number;
  gscCtr: number;
  gscPosition: number;
  gaSessions: number;
  gaUsers: number;
  gaConversions: number;
}

interface GscDaily {
  date: string;
  clicks: number;
  impressions: number;
  ctr: number;
  position: number;
}

interface GaDaily {
  date: string;
  sessions: number;
  users: number;
  newUsers: number;
  engagedSessions: number;
  conversions: number;
  engagementRate: number;
  averageSessionDuration: number;
  screenPageViews: number;
}

interface TrafficOverview {
  gsc: GscDaily[];
  ga4: GaDaily[];
  totals: TotalsSummary;
}

interface QueryRollup {
  query: string;
  clicks: number;
  impressions: number;
  ctr: number;
  position: number;
}

interface PageRollup {
  page: string;
  clicks: number;
  impressions: number;
  ctr: number;
  position: number;
}

interface SourceRollup {
  source: string;
  medium: string;
  sessions: number;
  users: number;
  conversions: number;
}

interface LandingPageRollup {
  landingPage: string;
  sessions: number;
  engagedSessions: number;
  conversions: number;
  averageSessionDuration: number;
}

interface PageViewDailyPoint {
  date: string;
  humans: number;
  bots: number;
}

interface PageViewTopPage {
  path: string;
  humans: number;
  bots: number;
}

interface PageViewTopCountry {
  country: string;
  humans: number;
}

interface PageViewStats {
  days: number;
  humanViews: number;
  botViews: number;
  lastDataDate: string | null;
  daily: PageViewDailyPoint[];
  topPages: PageViewTopPage[];
  topCountries: PageViewTopCountry[];
}

@Component({
  selector: 'app-admin-page',
  imports: [FormsModule, CurrencyPipe, TitleCasePipe, DatePipe, DecimalPipe, AdminCalendarComponent, AdminAccountingComponent, AdminCareComponent],
  templateUrl: './admin-page.component.html',
  styleUrl: './admin-page.component.scss',
})
export class AdminPageComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);
  private readonly http = inject(HttpClient);
  private readonly productService = inject(ProductService);
  private readonly orderService = inject(OrderAdminService);
  readonly store = inject(ProductStore);

  private readonly brandingService = inject(BrandingService);
  private readonly contentService = inject(ContentService);
  private readonly offsiteSaleService = inject(OffsiteSaleService);
  private readonly reviewsService = inject(ReviewsService);
  private readonly sanitizer = inject(DomSanitizer);

  @ViewChild('descriptionEditor') descriptionEditor!: ElementRef<HTMLElement>;
  readonly activeTab = signal<'products' | 'orders' | 'users' | 'finance' | 'accounting' | 'calendar' | 'seo' | 'branding' | 'content' | 'marketplace' | 'blog' | 'offsite-sales' | 'reviews' | 'signature' | 'care'>('products');
  readonly mobileMenuOpen = signal(false);
  readonly showForm = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly imagePreview = signal<string | null>(null);
  readonly uploading = signal(false);
  readonly uploadError = signal('');

  // Email signature — brand HTML signature for copy/paste into a mail client.
  readonly emailSignatureHtml = `<table cellpadding="0" cellspacing="0" border="0" role="presentation" style="font-family: Georgia, 'Times New Roman', Times, serif; color:#2e1a0e; line-height:1.4;">
  <tr>
    <td valign="top" style="padding-right:16px;">
      <table cellpadding="0" cellspacing="0" border="0" role="presentation">
        <tr>
          <td bgcolor="#9b4a1e" width="60" height="60" align="center" valign="middle" style="background-color:#9b4a1e; width:60px; height:60px; color:#f5f0e8; font-family:Georgia,'Times New Roman',serif; font-size:24px; font-weight:bold; letter-spacing:1px; text-align:center;">ER</td>
        </tr>
      </table>
    </td>
    <td valign="top" style="border-left:2px solid #c9973a; padding-left:16px;">
      <div style="font-size:18px; font-weight:bold; color:#2e1a0e; letter-spacing:0.3px;">Teodora Carter</div>
      <div style="font-size:11px; color:#9b4a1e; text-transform:uppercase; letter-spacing:2px; margin-top:3px;">Founder &amp; Curator</div>
      <div style="font-size:15px; font-weight:bold; color:#9b4a1e; margin-top:10px; letter-spacing:0.5px;">Eden Relics</div>
      <div style="font-size:12px; font-style:italic; color:#7a5c3a; margin-top:2px;">Vintage clothing, thoughtfully sourced &amp; carefully assessed.</div>
      <div style="font-size:12px; color:#5c3d1e; margin-top:10px;">
        <a href="https://edenrelics.co.uk" style="color:#9b4a1e; text-decoration:none; font-weight:bold;">edenrelics.co.uk</a>
        &nbsp;|&nbsp;
        <a href="mailto:teodora@edenrelics.co.uk" style="color:#9b4a1e; text-decoration:none;">teodora@edenrelics.co.uk</a>
      </div>
      <div style="font-size:12px; color:#5c3d1e; margin-top:6px;">
        <a href="https://instagram.com/edenrelics" style="color:#9b4a1e; text-decoration:none;">Instagram</a>
        &nbsp;&middot;&nbsp;
        <a href="https://x.com/edenrelics" style="color:#9b4a1e; text-decoration:none;">X</a>
      </div>
    </td>
  </tr>
</table>`;
  readonly emailSignaturePreview: SafeHtml = this.sanitizer.bypassSecurityTrustHtml(this.emailSignatureHtml);
  readonly signatureCopied = signal(false);

  // Orders
  readonly orders = signal<AdminOrder[]>([]);
  readonly ordersLoading = signal(false);
  readonly ordersError = signal('');
  readonly invoiceSendingId = signal<string | null>(null);
  // Marketplace shown on a manually-sent invoice, tracked per order id.
  readonly invoicePlatforms = ['edenrelics.co.uk', 'Etsy', 'Depop', 'Vinted', 'eBay'];
  readonly invoicePlatform = signal<Record<string, string>>({});
  readonly statusFilter = signal<string>('all');

  readonly statuses = ['Pending', 'Paid', 'Processing', 'Shipped', 'Delivered', 'Cancelled'];

  // Offsite Sales (dresses sold elsewhere — captured for analytics)
  readonly offsiteSales = signal<OffsiteSale[]>([]);
  readonly offsiteLoading = signal(false);
  readonly offsiteError = signal('');
  readonly offsiteSuccess = signal('');
  readonly showOffsiteForm = signal(false);
  readonly editingOffsiteId = signal<string | null>(null);
  offsiteForm: CreateOffsiteSale = this.emptyOffsiteForm();

  readonly offsitePlatforms = ['Etsy', 'Depop', 'Vinted', 'Instagram', 'In Person', 'Other'];

  // Reviews moderation
  readonly reviews = signal<AdminReview[]>([]);
  readonly reviewsLoading = signal(false);
  readonly reviewsFilter = signal<'Pending' | 'Approved' | 'Rejected' | 'all'>('Pending');
  readonly reviewsModerating = signal<string | null>(null);

  loadReviews(): void {
    this.reviewsLoading.set(true);
    const filter = this.reviewsFilter();
    const status = filter === 'all' ? undefined : filter;
    this.reviewsService.getAdmin(status).subscribe({
      next: (r) => {
        this.reviews.set(r);
        this.reviewsLoading.set(false);
      },
      error: () => this.reviewsLoading.set(false),
    });
  }

  setReviewsFilter(filter: 'Pending' | 'Approved' | 'Rejected' | 'all'): void {
    this.reviewsFilter.set(filter);
    this.loadReviews();
  }

  approveReview(id: string): void {
    this.reviewsModerating.set(id);
    this.reviewsService.approve(id).subscribe({
      next: () => {
        this.reviews.update((rs) => rs.filter((r) => r.id !== id || this.reviewsFilter() === 'all'));
        this.reviewsModerating.set(null);
        if (this.reviewsFilter() !== 'all') {
          this.loadReviews();
        }
      },
      error: () => this.reviewsModerating.set(null),
    });
  }

  rejectReview(id: string): void {
    this.reviewsModerating.set(id);
    this.reviewsService.reject(id).subscribe({
      next: () => {
        this.reviewsModerating.set(null);
        this.loadReviews();
      },
      error: () => this.reviewsModerating.set(null),
    });
  }

  reviewOverall(r: AdminReview): number {
    return Math.round(((r.transactionRating + r.deliveryRating + r.productRating) / 3) * 10) / 10;
  }

  // Product stats
  readonly totalItems = computed(() => this.store.products().length);
  readonly itemsForSale = computed(() => this.store.products().filter((p) => this.resolveStatus(p) === 'live').length);
  readonly itemsSold = computed(() => this.store.products().filter((p) => this.resolveStatus(p) === 'sold').length);
  readonly itemsStock = computed(() => this.store.products().filter((p) => this.resolveStatus(p) === 'stock').length);

  // Product list filters
  readonly productSearch = signal('');
  readonly productStatusFilter = signal<'all' | ProductStatus>('all');
  readonly productSort = signal<ProductSort>('newest');
  readonly filteredProducts = computed(() =>
    sortAdminProducts(
      filterAdminProducts(this.store.products(), this.productSearch(), this.productStatusFilter()),
      this.productSort()
    )
  );

  resolveStatus(product: Product): ProductStatus {
    return resolveProductStatus(product);
  }

  statusLabel(product: Product): string {
    return productStatusLabel(resolveProductStatus(product));
  }

  // SEO
  seoUrl = 'https://edenrelics.co.uk';
  readonly seoResult = signal<SeoResult | null>(null);

  // SEO health snapshots (catalog quality + sitemap signals captured daily)
  readonly seoHealthSnapshots = signal<SeoHealthSnapshot[]>([]);
  readonly seoHealthLoading = signal(false);
  readonly seoHealthRunning = signal(false);
  readonly seoHealthError = signal('');
  readonly latestSeoHealth = computed(() => this.seoHealthSnapshots()[0] ?? null);
  readonly seoLoading = signal(false);
  readonly seoError = signal('');

  // SEO traffic & rankings (Google Search Console + GA4 daily ingest)
  readonly trafficStatus = signal<TrafficStatus | null>(null);
  readonly trafficOverview = signal<TrafficOverview | null>(null);
  readonly trafficTopQueries = signal<QueryRollup[]>([]);
  readonly trafficTopPages = signal<PageRollup[]>([]);
  readonly trafficOpportunities = signal<QueryRollup[]>([]);
  readonly trafficSources = signal<SourceRollup[]>([]);
  readonly trafficLandingPages = signal<LandingPageRollup[]>([]);
  readonly pageViews = signal<PageViewStats | null>(null);
  readonly trafficLoading = signal(false);
  readonly trafficRunning = signal(false);
  readonly trafficError = signal('');
  readonly trafficWindowDays = signal(28);

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

  readonly contentSections: {
    title: string;
    fields: { key: string; label: string; type: 'text' | 'textarea' | 'html'; fallback?: string }[];
  }[] = [
    {
      title: 'Header Banner',
      fields: [
        { key: 'header.banner', label: 'Announcement (leave blank to hide)', type: 'text', fallback: 'Free shipping on UK orders' },
      ],
    },
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
        { key: 'home.about.title', label: 'Section Title', type: 'text', fallback: 'Why Eden Relics?' },
        { key: 'home.about.card1.title', label: 'Card 1 Title', type: 'text', fallback: 'Authentically Vintage' },
        { key: 'home.about.card1.text', label: 'Card 1 Text', type: 'textarea' },
        { key: 'home.about.card2.title', label: 'Card 2 Title', type: 'text', fallback: 'Quality Assured' },
        { key: 'home.about.card2.text', label: 'Card 2 Text', type: 'textarea' },
        { key: 'home.about.card3.title', label: 'Card 3 Title', type: 'text', fallback: 'Sustainable Fashion' },
        { key: 'home.about.card3.text', label: 'Card 3 Text', type: 'textarea' },
        { key: 'home.about.card4.title', label: 'Card 4 Title', type: 'text', fallback: 'Spanning the Decades' },
        { key: 'home.about.card4.text', label: 'Card 4 Text', type: 'textarea' },
      ],
    },
    {
      title: 'Footer',
      fields: [
        { key: 'footer.tagline', label: 'Tagline', type: 'text', fallback: 'Carefully sourced & lovingly preserved vintage clothing.' },
        { key: 'footer.company.line1', label: 'Company Line 1', type: 'text', fallback: 'Company No. 17153907' },
        { key: 'footer.company.line2', label: 'Company Line 2', type: 'text', fallback: 'Registered in England and Wales' },
        { key: 'footer.company.line3', label: 'Company Line 3', type: 'text' },
        { key: 'footer.contact.email', label: 'Email', type: 'text', fallback: 'edenrelics@dcp-net.com' },
        { key: 'footer.contact.phone', label: 'Phone', type: 'text', fallback: '+44 (0) 7454 905173' },
        { key: 'footer.contact.address', label: 'Address', type: 'textarea', fallback: 'EDEN RELICS LTD\n30 Vane Close\nNorwich, NR7 0US\nUnited Kingdom' },
      ],
    },
    {
      title: 'Contact Page',
      fields: [
        { key: 'contact.title', label: 'Title', type: 'text', fallback: 'Get in Touch' },
        { key: 'contact.subtitle', label: 'Subtitle', type: 'text', fallback: 'Have a question or want to know more? Drop us a message.' },
      ],
    },
    {
      title: 'About Page',
      fields: [
        { key: 'page.about.meta.title', label: 'SEO Title (browser tab + search results)', type: 'text', fallback: 'About Eden Relics — Lovingly Handpicked Vintage' },
        { key: 'page.about.meta.description', label: 'SEO Meta Description (search results snippet)', type: 'textarea', fallback: 'Eden Relics is a curated vintage shop in Norwich, UK, specialising in 1970s, 80s, and 90s dresses — personally sourced, photographed, and chosen for their quality and character.' },
        { key: 'page.about.title', label: 'Page Title (H1)', type: 'text', fallback: 'Lovingly handpicked vintage' },
        { key: 'page.about.content', label: 'Body Content (HTML)', type: 'html', fallback: '<p>Eden Relics is a curated vintage shop based in Norwich, UK, specialising in dresses from the 1970s, 80s, and 90s — the kind that were made to last, cut with intention, and worn by someone who loved them first.</p>\n<p>Every piece is personally sourced and chosen for its quality, character, and the way it moves. Wherever possible the dresses are modelled — by one of us or a friend — so you can see how they actually fall on a real body. For pieces that don\'t suit a modelled shot, we photograph them carefully on a mannequin so nothing is left to guesswork.</p>\n<p>We started Eden Relics because we believe in buying less and buying better. Fast fashion has a cost the price tag doesn\'t show — in waste, in craft, in the stories we throw away. Vintage is the alternative: beautiful things that already exist, waiting to be worn again.</p>\n<p>Every purchase here is an act of intention. We hope you find something that feels like it was always meant to be yours.</p>' },
        { key: 'page.about.signature', label: 'Signature', type: 'text', fallback: 'Teodora Carter & Peter Carter' },
        { key: 'page.about.jsonld.description', label: 'Structured Data — Organisation Description (one sentence, used by Google for the business)', type: 'textarea', fallback: 'A curated vintage shop based in Norwich, UK, specialising in dresses from the 1970s, 80s, and 90s.' },
      ],
    },
    {
      title: 'Privacy Policy',
      fields: [
        { key: 'policy.privacy.updated', label: 'Last Updated', type: 'text', fallback: 'March 2026' },
        { key: 'policy.privacy.content', label: 'Content (HTML)', type: 'html' },
      ],
    },
    {
      title: 'Returns Policy',
      fields: [
        { key: 'policy.returns.updated', label: 'Last Updated', type: 'text', fallback: 'March 2026' },
        { key: 'policy.returns.content', label: 'Content (HTML)', type: 'html' },
      ],
    },
    {
      title: 'Supply Chain Policy',
      fields: [
        { key: 'policy.supply-chain.updated', label: 'Last Updated', type: 'text', fallback: 'March 2026' },
        { key: 'policy.supply-chain.content', label: 'Content (HTML)', type: 'html' },
      ],
    },
    {
      title: 'Modern Slavery Policy',
      fields: [
        { key: 'policy.modern-slavery.updated', label: 'Last Updated', type: 'text', fallback: 'March 2026' },
        { key: 'policy.modern-slavery.content', label: 'Content (HTML)', type: 'html' },
      ],
    },
    {
      title: 'Terms & Conditions',
      fields: [
        { key: 'policy.terms.updated', label: 'Last Updated', type: 'text', fallback: 'April 2026' },
        { key: 'policy.terms.content', label: 'Content (HTML)', type: 'html' },
      ],
    },
    {
      title: 'Cookie Policy',
      fields: [
        { key: 'policy.cookies.updated', label: 'Last Updated', type: 'text', fallback: 'April 2026' },
        { key: 'policy.cookies.content', label: 'Content (HTML)', type: 'html' },
      ],
    },
    {
      title: 'Latest Security Report',
      fields: [
        { key: 'report.security.updated', label: 'Last Updated', type: 'text', fallback: 'April 2026' },
        { key: 'report.security.content', label: 'Content (HTML)', type: 'html' },
      ],
    },
    {
      title: 'Accessibility Report',
      fields: [
        { key: 'report.accessibility.updated', label: 'Last Updated', type: 'text', fallback: 'April 2026' },
        { key: 'report.accessibility.content', label: 'Content (HTML)', type: 'html' },
      ],
    },
    {
      title: 'Compliance Report',
      fields: [
        { key: 'report.compliance.updated', label: 'Last Updated', type: 'text', fallback: 'April 2026' },
        { key: 'report.compliance.content', label: 'Content (HTML)', type: 'html' },
      ],
    },
  ];

  // Branding
  brandingForm: Branding = {
    logoUrl: null, bgPrimary: '#F5F0E6', bgSecondary: '#EAE0CC', bgCard: '#FBF8F1',
    bgDark: '#1C1510', textPrimary: '#2E1A0E', textSecondary: '#5C3D1E',
    textMuted: '#6E4A22', textInverse: '#F5F0E6', accent: '#9B4A1E',
    accentHover: '#7A3A16', fontDisplay: 'Playfair Display', fontBody: 'EB Garamond',
  };
  readonly brandingSaving = signal(false);
  readonly brandingSuccess = signal('');
  readonly brandingError = signal('');
  readonly logoUploading = signal(false);
  readonly logoPreview = signal<string | null>(null);
  readonly contrastCheckTrigger = signal(0);

  readonly contrastErrors = computed(() => {
    this.contrastCheckTrigger();
    const f = this.brandingForm;
    const pairs: { fg: string; bg: string; label: string }[] = [
      { fg: f.textPrimary, bg: f.bgPrimary, label: 'Text Primary on Background Primary' },
      { fg: f.textSecondary, bg: f.bgPrimary, label: 'Text Secondary on Background Primary' },
      { fg: f.textMuted, bg: f.bgPrimary, label: 'Text Muted on Background Primary' },
      { fg: f.textPrimary, bg: f.bgCard, label: 'Text Primary on Card Background' },
      { fg: f.textSecondary, bg: f.bgCard, label: 'Text Secondary on Card Background' },
      { fg: f.textMuted, bg: f.bgCard, label: 'Text Muted on Card Background' },
      { fg: f.textPrimary, bg: f.bgSecondary, label: 'Text Primary on Background Secondary' },
      { fg: f.textSecondary, bg: f.bgSecondary, label: 'Text Secondary on Background Secondary' },
      { fg: f.textInverse, bg: f.bgDark, label: 'Text Inverse on Dark Background' },
      { fg: f.accent, bg: f.bgPrimary, label: 'Accent on Background Primary' },
      { fg: f.accent, bg: f.bgCard, label: 'Accent on Card Background' },
    ];
    const errors: string[] = [];
    for (const { fg, bg, label } of pairs) {
      const ratio = this.contrastRatio(fg, bg);
      if (ratio < 4.5) {
        errors.push(`${label}: ${ratio.toFixed(1)}:1 (requires 4.5:1)`);
      }
    }
    return errors;
  });

  private luminance(hex: string): number {
    const rgb = [hex.slice(1, 3), hex.slice(3, 5), hex.slice(5, 7)]
      .map(c => {
        const v = parseInt(c, 16) / 255;
        return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
      });
    return 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2];
  }

  private contrastRatio(fg: string, bg: string): number {
    const l1 = this.luminance(fg);
    const l2 = this.luminance(bg);
    const lighter = Math.max(l1, l2);
    const darker = Math.min(l1, l2);
    return (lighter + 0.05) / (darker + 0.05);
  }

  private static readonly IMAGE_RESIZE_WARN_BYTES = 10 * 1024 * 1024;
  private static readonly LOGO_RESIZE_WARN_BYTES = 5 * 1024 * 1024;
  private static readonly MAX_UPLOAD_BYTES = 4 * 1024 * 1024 * 1024;
  private static readonly MAX_UPLOAD_DISPLAY = '4 GB';

  private confirmLargeImageUpload(file: File, warnBytes: number): boolean {
    if (file.size > AdminPageComponent.MAX_UPLOAD_BYTES) {
      alert(`"${file.name}" is over the ${AdminPageComponent.MAX_UPLOAD_DISPLAY} upload maximum and cannot be uploaded.`);
      return false;
    }
    if (file.size <= warnBytes) {
      return true;
    }
    const sizeMb = (file.size / (1024 * 1024)).toFixed(1);
    return confirm(
      `"${file.name}" is ${sizeMb} MB. It will be resized to fit our display dimensions, which may reduce image quality. For best results, we recommend resizing it manually before uploading.\n\nContinue with the upload?`
    );
  }

  readonly fontOptions = [
    'Playfair Display', 'EB Garamond', 'Cinzel Decorative', 'Work Sans', 'Inter',
    'Lora', 'Merriweather', 'Montserrat', 'Open Sans', 'Poppins', 'Raleway', 'Roboto',
    'Source Sans 3', 'Nunito', 'PT Serif', 'Crimson Text', 'Libre Baskerville',
    'DM Sans', 'Outfit', 'Cormorant Garamond', 'Josefin Sans',
  ];

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
  readonly etsyStatus = signal<{ apiKeyConfigured: boolean; connected: boolean; shopId: string | null } | null>(null);
  private etsyCodeVerifier = '';
  readonly marketplaceError = signal('');
  readonly marketplaceSuccess = signal('');

  // AI analysis
  readonly analysing = signal(false);
  readonly analyseError = signal('');

  // Users
  readonly adminUsers = signal<AdminUser[]>([]);
  readonly usersLoading = signal(false);
  readonly usersError = signal('');

  // Mailing list
  readonly mailingListSubs = signal<{ id: string; email: string; firstName: string | null; source: string; createdAtUtc: string }[]>([]);
  readonly mailingListLoading = signal(false);

  // Accounts
  readonly accountsSummary = signal<AccountsSummary | null>(null);
  readonly accountsLoading = signal(false);
  readonly accountsError = signal('');

  // Finance
  readonly financeTransactions = signal<FinanceTransaction[]>([]);
  readonly financeLoading = signal(false);
  readonly financeError = signal('');
  readonly financeSuccess = signal('');
  readonly financeSummary = signal<FinanceSummary | null>(null);
  readonly showFinanceForm = signal(false);
  readonly editingTransactionId = signal<string | null>(null);
  readonly financeReceiptUploading = signal(false);
  readonly financeMonthFilter = signal<string>('all');
  readonly financeSourceFilter = signal<'all' | 'site' | 'external'>('all');
  readonly backfillingSales = signal(false);
  financeForm = { date: '', description: '', amount: 0, category: 'Stock', platform: '', reference: '', notes: '' };
  financeReceiptUrl: string | null = null;

  readonly financeCategories = ['Stock', 'Shipping', 'Fees', 'Packaging', 'Owner Draw', 'Sales', 'Other'];
  readonly financePlatforms = ['Website', 'Etsy', 'Depop', 'Vinted', 'eBay', ''];

  // Monzo bank
  readonly financeSubTab = signal<'transactions' | 'bank'>('transactions');
  readonly monzoTransactions = signal<MonzoTransaction[]>([]);
  readonly monzoBalance = signal<MonzoBalance | null>(null);
  readonly monzoPots = signal<MonzoPot[]>([]);
  readonly monzoLoading = signal(false);
  readonly monzoSyncing = signal(false);
  readonly monzoError = signal('');
  readonly monzoConnected = signal(false);
  readonly monzoPendingApproval = signal(false);
  readonly monzoAnnotating = signal<string | null>(null);
  readonly monzoSummary = signal<MonzoSummary | null>(null);
  readonly monzoMonthFilter = signal<string>('all');
  readonly monzoReceiptUploading = signal<string | null>(null);

  // View analytics
  readonly showViewAnalytics = signal(false);
  readonly viewAnalyticsProduct = signal<Product | null>(null);
  readonly viewAnalyticsData = signal<ViewAnalytics | null>(null);
  readonly viewAnalyticsLoading = signal(false);
  readonly viewAnalyticsMode = signal<'list' | 'aggregate'>('list');

  form: Omit<Product, 'id'> = this.emptyForm();

  ngOnInit(): void {
    this.seo.updateTags({ title: 'Admin', url: '/admin', noIndex: true });
    this.route.queryParams.subscribe(params => {
      if (params['monzo'] === 'callback' && params['code']) {
        this.handleMonzoCallback(params['code'], params['state'] ?? '');
      }
      if (params['etsy'] === 'callback' && params['code']) {
        this.switchTab('marketplace');
        this.handleEtsyCallback(params['code']);
        this.router.navigate(['/admin'], { queryParams: {} });
      }
    });
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen.update(v => !v);
  }

  copySignature(): void {
    void navigator.clipboard?.writeText(this.emailSignatureHtml).then(() => {
      this.signatureCopied.set(true);
      setTimeout(() => this.signatureCopied.set(false), 2000);
    });
  }

  switchTab(tab: 'products' | 'orders' | 'users' | 'finance' | 'accounting' | 'calendar' | 'seo' | 'branding' | 'content' | 'marketplace' | 'blog' | 'offsite-sales' | 'reviews' | 'signature' | 'care'): void {
    this.mobileMenuOpen.set(false);
    this.activeTab.set(tab);
    if (tab === 'orders' && this.orders().length === 0) {
      this.loadOrders();
    }
    if (tab === 'seo') {
      if (this.trackedKeywords().length === 0) {
        this.loadTrackedKeywords();
      }
      this.loadSeoHealth();
      this.loadTraffic();
    }
    if (tab === 'branding') {
      this.loadBranding();
    }
    if (tab === 'users') {
      this.loadUsers();
    }
    if (tab === 'content') {
      this.loadContent();
    }
    if (tab === 'marketplace') {
      this.loadMarketplace();
    }
    if (tab === 'finance') {
      this.loadFinance();
      this.loadAccounts();
    }
    if (tab === 'blog') {
      this.loadBlogPosts();
    }
    if (tab === 'reviews') {
      this.loadReviews();
    }
    if (tab === 'offsite-sales' && this.offsiteSales().length === 0) {
      this.loadOffsiteSales();
    }
  }

  loadOffsiteSales(): void {
    this.offsiteLoading.set(true);
    this.offsiteError.set('');
    this.offsiteSaleService.getAll().subscribe({
      next: (sales) => {
        this.offsiteSales.set(sales);
        this.offsiteLoading.set(false);
      },
      error: () => {
        this.offsiteError.set('Failed to load offsite sales.');
        this.offsiteLoading.set(false);
      },
    });
  }

  openOffsiteForm(): void {
    this.editingOffsiteId.set(null);
    this.offsiteForm = this.emptyOffsiteForm();
    this.offsiteError.set('');
    this.offsiteSuccess.set('');
    this.showOffsiteForm.set(true);
  }

  editOffsiteSale(sale: OffsiteSale): void {
    this.editingOffsiteId.set(sale.id);
    this.offsiteForm = {
      dressName: sale.dressName,
      era: sale.era,
      category: sale.category,
      size: sale.size,
      condition: sale.condition,
      salePrice: sale.salePrice,
      costPrice: sale.costPrice,
      platform: sale.platform,
      saleDateUtc: sale.saleDateUtc.slice(0, 10),
      notes: sale.notes,
    };
    this.offsiteError.set('');
    this.offsiteSuccess.set('');
    this.showOffsiteForm.set(true);
  }

  saveOffsiteSale(): void {
    this.offsiteError.set('');
    this.offsiteSuccess.set('');
    const id = this.editingOffsiteId();
    const payload: CreateOffsiteSale = {
      ...this.offsiteForm,
      saleDateUtc: new Date(this.offsiteForm.saleDateUtc).toISOString(),
    };
    const req = id
      ? this.offsiteSaleService.update(id, payload)
      : this.offsiteSaleService.create(payload);

    req.subscribe({
      next: (saved) => {
        if (id) {
          this.offsiteSales.update((list) => list.map((s) => (s.id === id ? saved : s)));
        } else {
          this.offsiteSales.update((list) => [saved, ...list]);
        }
        this.offsiteSuccess.set(id ? 'Sale updated.' : 'Sale recorded.');
        this.showOffsiteForm.set(false);
      },
      error: (err) => {
        this.offsiteError.set(err.error?.message ?? 'Failed to save sale.');
      },
    });
  }

  deleteOffsiteSale(id: string): void {
    if (!confirm('Delete this offsite sale?')) {
      return;
    }
    this.offsiteSaleService.remove(id).subscribe({
      next: () => {
        this.offsiteSales.update((list) => list.filter((s) => s.id !== id));
      },
      error: () => this.offsiteError.set('Failed to delete sale.'),
    });
  }

  closeOffsiteForm(): void {
    this.showOffsiteForm.set(false);
  }

  private emptyOffsiteForm(): CreateOffsiteSale {
    return {
      dressName: '',
      era: '',
      category: '70s',
      size: '10',
      condition: 'good',
      salePrice: 0,
      costPrice: 0,
      platform: 'Etsy',
      saleDateUtc: new Date().toISOString().slice(0, 10),
      notes: null,
    };
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
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    if (!this.confirmLargeImageUpload(file, AdminPageComponent.IMAGE_RESIZE_WARN_BYTES)) {
      input.value = '';
      return;
    }
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
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    if (!this.confirmLargeImageUpload(file, AdminPageComponent.IMAGE_RESIZE_WARN_BYTES)) {
      input.value = '';
      return;
    }
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

    this.http.get<{ apiKeyConfigured: boolean; connected: boolean; shopId: string | null }>(`${environment.apiUrl}/api/marketplace/etsy/status`).subscribe({
      next: (r) => this.etsyStatus.set(r),
      error: () => this.etsyStatus.set({ apiKeyConfigured: false, connected: false, shopId: null }),
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

  connectEtsy(): void {
    this.marketplaceError.set('');
    this.http.get<{ url: string; state: string; codeVerifier: string }>(`${environment.apiUrl}/api/marketplace/etsy/connect`).subscribe({
      next: (r) => {
        this.etsyCodeVerifier = r.codeVerifier;
        localStorage.setItem('etsy_code_verifier', r.codeVerifier);
        window.location.href = r.url;
      },
      error: (err) => this.marketplaceError.set(err.error?.message ?? 'Failed to start Etsy connection.'),
    });
  }

  handleEtsyCallback(code: string): void {
    const codeVerifier = localStorage.getItem('etsy_code_verifier') || this.etsyCodeVerifier;
    if (!codeVerifier) {
      this.marketplaceError.set('Missing code verifier. Please try connecting again.');
      return;
    }
    localStorage.removeItem('etsy_code_verifier');

    this.http.post<{ message: string; shopId: string }>(`${environment.apiUrl}/api/marketplace/etsy/callback`, {
      code,
      codeVerifier,
    }).subscribe({
      next: (r) => {
        this.marketplaceSuccess.set(r.message);
        this.etsyStatus.set({ apiKeyConfigured: true, connected: true, shopId: r.shopId });
      },
      error: (err) => this.marketplaceError.set(err.error?.message ?? 'Failed to complete Etsy connection.'),
    });
  }

  disconnectEtsy(): void {
    this.http.post(`${environment.apiUrl}/api/marketplace/etsy/disconnect`, {}).subscribe({
      next: () => {
        this.marketplaceSuccess.set('Etsy disconnected.');
        this.etsyStatus.set({ apiKeyConfigured: true, connected: false, shopId: null });
      },
      error: (err) => this.marketplaceError.set(err.error?.message ?? 'Failed to disconnect Etsy.'),
    });
  }

  copyToClipboard(text: string): void {
    navigator.clipboard.writeText(text);
  }

  loadUsers(): void {
    this.usersLoading.set(true);
    this.usersError.set('');
    this.http.get<AdminUser[]>(`${environment.apiUrl}/api/admin/users`).subscribe({
      next: (users) => {
        this.adminUsers.set(users);
        this.usersLoading.set(false);
      },
      error: () => {
        this.usersError.set('Failed to load users.');
        this.usersLoading.set(false);
      },
    });
    this.loadMailingList();
  }

  hasUserAccount(email: string): boolean {
    return this.adminUsers().some(u => u.email.toLowerCase() === email.toLowerCase());
  }

  loadMailingList(): void {
    this.mailingListLoading.set(true);
    this.http.get<{ id: string; email: string; firstName: string | null; source: string; createdAtUtc: string }[]>(
      `${environment.apiUrl}/api/mailing-list/subscribers`
    ).subscribe({
      next: (subs) => {
        this.mailingListSubs.set(subs);
        this.mailingListLoading.set(false);
      },
      error: () => this.mailingListLoading.set(false),
    });
  }

  openViewAnalytics(product: Product): void {
    this.viewAnalyticsProduct.set(product);
    this.viewAnalyticsData.set(null);
    this.viewAnalyticsLoading.set(true);
    this.showViewAnalytics.set(true);
    this.http.get<ViewAnalytics>(`${environment.apiUrl}/api/products/${product.id}/views`).subscribe({
      next: (data) => {
        this.viewAnalyticsData.set(data);
        this.viewAnalyticsLoading.set(false);
      },
      error: () => this.viewAnalyticsLoading.set(false),
    });
  }

  closeViewAnalytics(): void {
    this.showViewAnalytics.set(false);
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
        const merged: Record<string, string> = { ...c };
        // Prefill each editor field with the template-level fallback so admins see
        // the current public-facing text, not an empty input.
        for (const section of this.contentSections) {
          for (const field of section.fields) {
            if (merged[field.key] === undefined && field.fallback !== undefined) {
              merged[field.key] = field.fallback;
            }
          }
        }
        this.contentForm = merged;
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
    if (this.contrastErrors().length > 0) {
      this.brandingError.set('Cannot save: colour combinations do not meet WCAG AA contrast requirements.');
      return;
    }
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
    if (!this.confirmLargeImageUpload(file, AdminPageComponent.LOGO_RESIZE_WARN_BYTES)) {
      input.value = '';
      return;
    }

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
    this.contrastCheckTrigger.update(v => v + 1);
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

  loadSeoHealth(): void {
    this.seoHealthLoading.set(true);
    this.seoHealthError.set('');
    this.http.get<SeoHealthSnapshot[]>(`${environment.apiUrl}/api/seo/health/snapshots?take=30`).subscribe({
      next: (snapshots) => {
        this.seoHealthSnapshots.set(snapshots);
        this.seoHealthLoading.set(false);
      },
      error: () => {
        this.seoHealthLoading.set(false);
        this.seoHealthError.set('Failed to load SEO health snapshots.');
      },
    });
  }

  runSeoHealthSnapshot(): void {
    this.seoHealthRunning.set(true);
    this.seoHealthError.set('');
    this.http.post<SeoHealthSnapshot>(`${environment.apiUrl}/api/seo/health/snapshots/run`, {}).subscribe({
      next: (snapshot) => {
        this.seoHealthRunning.set(false);
        this.seoHealthSnapshots.update((list) => [snapshot, ...list]);
      },
      error: () => {
        this.seoHealthRunning.set(false);
        this.seoHealthError.set('Snapshot capture failed.');
      },
    });
  }

  loadTraffic(days?: number): void {
    if (days !== undefined) {
      this.trafficWindowDays.set(days);
    }
    const window = this.trafficWindowDays();
    this.trafficLoading.set(true);
    this.trafficError.set('');

    this.http.get<TrafficStatus>(`${environment.apiUrl}/api/seo/traffic/status`).subscribe({
      next: (status) => this.trafficStatus.set(status),
      error: () => this.trafficStatus.set(null),
    });

    const overviewDays = Math.max(window, 30);
    forkJoin({
      overview: this.http.get<TrafficOverview>(
        `${environment.apiUrl}/api/seo/traffic/overview?days=${overviewDays}`),
      queries: this.http.get<QueryRollup[]>(
        `${environment.apiUrl}/api/seo/traffic/queries?days=${window}&limit=50`),
      pages: this.http.get<PageRollup[]>(
        `${environment.apiUrl}/api/seo/traffic/pages?days=${window}&limit=50`),
      opportunities: this.http.get<QueryRollup[]>(
        `${environment.apiUrl}/api/seo/traffic/opportunities?days=${window}`),
      sources: this.http.get<SourceRollup[]>(
        `${environment.apiUrl}/api/seo/traffic/sources?days=${window}&limit=20`),
      landingPages: this.http.get<LandingPageRollup[]>(
        `${environment.apiUrl}/api/seo/traffic/landing-pages?days=${window}&limit=50`),
      pageViews: this.http.get<PageViewStats>(
        `${environment.apiUrl}/api/seo/traffic/page-views?days=${window}&limit=50`),
    }).subscribe({
      next: (r) => {
        this.trafficOverview.set(r.overview);
        this.trafficTopQueries.set(r.queries);
        this.trafficTopPages.set(r.pages);
        this.trafficOpportunities.set(r.opportunities);
        this.trafficSources.set(r.sources);
        this.trafficLandingPages.set(r.landingPages);
        this.pageViews.set(r.pageViews);
        this.trafficLoading.set(false);
      },
      error: () => {
        this.trafficLoading.set(false);
        this.trafficError.set('Failed to load traffic data.');
      },
    });
  }

  runTrafficIngest(): void {
    this.trafficRunning.set(true);
    this.trafficError.set('');
    this.http.post(`${environment.apiUrl}/api/seo/traffic/run`, {}).subscribe({
      next: () => {
        this.trafficRunning.set(false);
        this.loadTraffic();
      },
      error: () => {
        this.trafficRunning.set(false);
        this.trafficError.set('Traffic ingest failed — check the service account is configured.');
      },
    });
  }

  changeTrafficWindow(days: number): void {
    this.loadTraffic(days);
  }

  botShare(pv: PageViewStats): number {
    const total = pv.humanViews + pv.botViews;
    return total > 0 ? (pv.botViews / total) * 100 : 0;
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

  deleteOrder(id: string): void {
    if (!confirm('Permanently delete this order? This cannot be undone.')) {
      return;
    }
    this.http.delete(`${environment.apiUrl}/api/orders/admin/${id}`).subscribe({
      next: () => {
        this.orders.update((orders) => orders.filter((o) => o.id !== id));
      },
      error: () => this.ordersError.set('Failed to delete order.'),
    });
  }

  platformFor(id: string): string {
    return this.invoicePlatform()[id] ?? this.invoicePlatforms[0];
  }

  setInvoicePlatform(id: string, platform: string): void {
    this.invoicePlatform.update((map) => ({ ...map, [id]: platform }));
  }

  previewInvoice(order: AdminOrder): void {
    this.ordersError.set('');
    this.orderService.previewInvoice(order.id, this.platformFor(order.id)).subscribe({
      next: (html) => {
        const url = URL.createObjectURL(new Blob([html], { type: 'text/html' }));
        window.open(url, '_blank');
        // Free the object URL once the new tab has had a chance to load it.
        setTimeout(() => URL.revokeObjectURL(url), 10000);
      },
      error: () => this.ordersError.set('Failed to load the invoice preview.'),
    });
  }

  sendInvoice(order: AdminOrder): void {
    if (this.invoiceSendingId()) {
      return;
    }
    this.ordersError.set('');
    this.invoiceSendingId.set(order.id);
    this.orderService.sendInvoice(order.id, this.platformFor(order.id)).subscribe({
      next: ({ sentTo }) => {
        this.invoiceSendingId.set(null);
        alert(`Invoice emailed to ${sentTo}.`);
      },
      error: () => {
        this.invoiceSendingId.set(null);
        this.ordersError.set('Failed to send the invoice email.');
      },
    });
  }

  openForm(): void {
    this.editingId.set(null);
    this.imagePreview.set(null);
    this.uploadError.set('');
    this.form = this.emptyForm();
    this.showForm.set(true);
    setTimeout(() => {
      if (this.descriptionEditor) {
        this.descriptionEditor.nativeElement.innerHTML = '';
      }
    });
  }

  edit(product: Product): void {
    this.editingId.set(product.id);
    this.imagePreview.set(null);
    this.uploadError.set('');
    this.form = {
      name: product.name,
      sku: product.sku ?? '',
      description: product.description,
      price: product.price,
      salePrice: product.salePrice ?? null,
      costPrice: product.costPrice ?? 0,
      stockPurchaseDate: product.stockPurchaseDate ?? null,
      supplier: product.supplier ?? '',
      era: product.era,
      category: product.category,
      size: product.size,
      condition: product.condition,
      material: product.material ?? null,
      imageUrl: product.imageUrl,
      additionalImageUrls: [...(product.additionalImageUrls ?? [])],
      videoUrls: [...(product.videoUrls ?? [])],
      inStock: product.inStock,
      status: this.resolveStatus(product),
    };
    this.showForm.set(true);
    setTimeout(() => {
      if (this.descriptionEditor) {
        this.descriptionEditor.nativeElement.innerHTML = product.description;
      }
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    if (!this.confirmLargeImageUpload(file, AdminPageComponent.IMAGE_RESIZE_WARN_BYTES)) {
      input.value = '';
      return;
    }

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

  analyseImage(): void {
    if (!this.form.imageUrl) {
      this.analyseError.set('Upload an image first.');
      return;
    }
    this.analysing.set(true);
    this.analyseError.set('');
    this.http.post<any>(`${environment.apiUrl}/api/products/analyse-image`, { imageUrl: this.form.imageUrl }).subscribe({
      next: (result) => {
        if (result.name) { this.form.name = result.name; }
        if (result.description) {
          this.form.description = result.description;
          if (this.descriptionEditor) {
            this.descriptionEditor.nativeElement.innerHTML = result.description;
          }
        }
        if (result.era) { this.form.era = result.era; }
        if (result.category) { this.form.category = result.category; }
        if (result.size) { this.form.size = result.size; }
        if (result.condition) { this.form.condition = result.condition; }
        if (result.suggestedPrice) { this.form.price = result.suggestedPrice; }
        this.analysing.set(false);
      },
      error: (err) => {
        this.analyseError.set(err.error?.error ?? 'Analysis failed. Please try again.');
        this.analysing.set(false);
      },
    });
  }

  save(): void {
    const id = this.editingId();
    if (id) {
      this.store.updateProduct(id, this.form);
    } else {
      this.store.addProduct(this.form);
    }
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
      sku: '',
      description: '',
      price: 0,
      salePrice: null,
      costPrice: 0,
      stockPurchaseDate: null,
      supplier: '',
      era: '',
      category: '70s',
      size: '10',
      condition: 'good',
      material: null,
      imageUrl: '',
      additionalImageUrls: [] as string[],
      videoUrls: [] as string[],
      inStock: true,
      status: 'live',
    };
  }

  readonly dragOverIndex = signal<number | null>(null);
  private dragStartIndex: number | null = null;

  onImageDragStart(index: number): void {
    this.dragStartIndex = index;
  }

  onImageDragOver(event: DragEvent, index: number): void {
    event.preventDefault();
    this.dragOverIndex.set(index);
  }

  onImageDragLeave(): void {
    this.dragOverIndex.set(null);
  }

  onImageDrop(targetIndex: number): void {
    this.dragOverIndex.set(null);
    if (this.dragStartIndex === null || this.dragStartIndex === targetIndex) {
      return;
    }
    const imgs = this.allProductImages();
    const [moved] = imgs.splice(this.dragStartIndex, 1);
    imgs.splice(targetIndex, 0, moved);
    this.form.imageUrl = imgs[0] ?? '';
    this.form.additionalImageUrls = imgs.slice(1);
    this.dragStartIndex = null;
  }

  onImageDragEnd(): void {
    this.dragOverIndex.set(null);
    this.dragStartIndex = null;
  }

  // Keyboard-accessible alternative to drag-and-drop reordering.
  moveImage(index: number, delta: number): void {
    const imgs = this.allProductImages();
    const target = index + delta;
    if (target < 0 || target >= imgs.length) {
      return;
    }
    [imgs[index], imgs[target]] = [imgs[target], imgs[index]];
    this.form.imageUrl = imgs[0] ?? '';
    this.form.additionalImageUrls = imgs.slice(1);
  }

  allProductImages(): string[] {
    const imgs: string[] = [];
    if (this.form.imageUrl) {
      imgs.push(this.form.imageUrl);
    }
    for (const img of this.form.additionalImageUrls ?? []) {
      if (img && !imgs.includes(img)) {
        imgs.push(img);
      }
    }
    return imgs;
  }

  setPrimaryImage(url: string): void {
    const others = this.allProductImages().filter(img => img !== url);
    this.form.imageUrl = url;
    this.form.additionalImageUrls = others;
  }

  removeProductImage(url: string): void {
    if (url === this.form.imageUrl) {
      const others = (this.form.additionalImageUrls ?? []).filter(img => img !== url);
      this.form.imageUrl = others[0] ?? '';
      this.form.additionalImageUrls = others.slice(1);
    } else {
      this.form.additionalImageUrls = (this.form.additionalImageUrls ?? []).filter(img => img !== url);
    }
  }

  async onProductImageSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const files = input.files;
    if (!files || files.length === 0) {
      return;
    }
    const filesToUpload: File[] = [];
    for (let i = 0; i < files.length; i++) {
      if (this.confirmLargeImageUpload(files[i], AdminPageComponent.IMAGE_RESIZE_WARN_BYTES)) {
        filesToUpload.push(files[i]);
      }
    }
    input.value = '';
    if (filesToUpload.length === 0) {
      return;
    }
    this.uploading.set(true);
    this.uploadError.set('');
    let anyFailed = false;
    // Upload sequentially: each image triggers a server-side decode + 4 webp variants,
    // and running them in parallel can OOM the API instance.
    for (const file of filesToUpload) {
      try {
        const res = await firstValueFrom(this.productService.uploadImage(file));
        if (!this.form.imageUrl) {
          this.form.imageUrl = res.imageUrl;
        } else {
          this.form.additionalImageUrls = [...(this.form.additionalImageUrls ?? []), res.imageUrl];
        }
      } catch {
        anyFailed = true;
      }
    }
    if (anyFailed) {
      this.uploadError.set('One or more uploads failed.');
    }
    this.uploading.set(false);
  }

  readonly videoUploading = signal(false);
  readonly videoUploadError = signal('');

  onVideoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = input.files;
    if (!files || files.length === 0) {
      return;
    }
    this.videoUploading.set(true);
    this.videoUploadError.set('');
    let pending = files.length;
    for (let i = 0; i < files.length; i++) {
      this.productService.uploadVideo(files[i]).subscribe({
        next: (res) => {
          this.form.videoUrls = [...(this.form.videoUrls ?? []), res.videoUrl];
          pending--;
          if (pending === 0) {
            this.videoUploading.set(false);
          }
        },
        error: () => {
          pending--;
          if (pending === 0) {
            this.videoUploading.set(false);
          }
          this.videoUploadError.set(`Video upload failed. Max size is ${AdminPageComponent.MAX_UPLOAD_DISPLAY}.`);
        },
      });
    }
    input.value = '';
  }

  removeVideo(url: string): void {
    this.form.videoUrls = (this.form.videoUrls ?? []).filter(v => v !== url);
  }

  loadFinance(): void {
    this.financeLoading.set(true);
    this.financeError.set('');
    this.http.get<FinanceTransaction[]>(`${environment.apiUrl}/api/finance`).subscribe({
      next: (txns) => {
        this.financeTransactions.set(txns);
        this.financeLoading.set(false);
      },
      error: () => {
        this.financeError.set('Failed to load transactions.');
        this.financeLoading.set(false);
      },
    });
    this.http.get<FinanceSummary>(`${environment.apiUrl}/api/finance/summary`).subscribe({
      next: (s) => this.financeSummary.set(s),
    });
  }

  backfillSales(): void {
    this.backfillingSales.set(true);
    this.financeError.set('');
    this.financeSuccess.set('');
    this.http.post<{
      backfilled: number;
      totalPaid: number;
      totalSoldProducts: number;
      breakdown: { fromOrders: number; fromProducts: number; cogs: number };
    }>(
      `${environment.apiUrl}/api/finance/backfill-sales`, {}
    ).subscribe({
      next: (res) => {
        this.backfillingSales.set(false);
        const totalKnownSales = res.totalPaid + res.totalSoldProducts;
        if (res.backfilled === 0) {
          this.financeSuccess.set(
            totalKnownSales === 0
              ? 'No known sales to backfill — nothing in the Orders table and no products marked Sold.'
              : `Everything is already in the ledger — ${totalKnownSales} known sale${totalKnownSales === 1 ? '' : 's'} and their cost-of-goods expenses.`,
          );
        } else {
          const parts: string[] = [];
          if (res.breakdown.fromOrders > 0) {
            parts.push(`${res.breakdown.fromOrders} sale${res.breakdown.fromOrders === 1 ? '' : 's'} from paid orders`);
          }
          if (res.breakdown.fromProducts > 0) {
            parts.push(`${res.breakdown.fromProducts} sale${res.breakdown.fromProducts === 1 ? '' : 's'} from products marked Sold`);
          }
          if (res.breakdown.cogs > 0) {
            parts.push(`${res.breakdown.cogs} cost-of-goods expense${res.breakdown.cogs === 1 ? '' : 's'}`);
          }
          this.financeSuccess.set(
            `Backfilled ${res.backfilled} ledger entr${res.backfilled === 1 ? 'y' : 'ies'} (${parts.join(', ')}).`,
          );
          this.loadFinance();
        }
      },
      error: () => {
        this.backfillingSales.set(false);
        this.financeError.set('Failed to backfill sales.');
      },
    });
  }

  get filteredFinanceTransactions(): FinanceTransaction[] {
    const monthFilter = this.financeMonthFilter();
    const sourceFilter = this.financeSourceFilter();
    let result = this.financeTransactions();
    if (monthFilter !== 'all') {
      result = result.filter(t => t.date.startsWith(monthFilter));
    }
    if (sourceFilter === 'site') {
      result = result.filter(t => t.platform === 'Website');
    } else if (sourceFilter === 'external') {
      // External = a known non-Website platform (Etsy, Depop, Vinted, eBay, etc.).
      // Transactions with no platform set are considered Unspecified and excluded
      // from both Site and External filters so the buckets don't overlap.
      result = result.filter(t => !!t.platform && t.platform !== 'Website');
    }
    return result;
  }

  get selectedMonthSummary(): FinanceSummary['byMonth'][0] | null {
    const filter = this.financeMonthFilter();
    const summary = this.financeSummary();
    if (!summary) {
      return null;
    }
    if (filter === 'all') {
      return null;
    }
    return summary.byMonth.find(m => m.month === filter) ?? null;
  }

  openFinanceForm(): void {
    this.editingTransactionId.set(null);
    this.financeForm = { date: new Date().toISOString().slice(0, 10), description: '', amount: 0, category: 'Stock', platform: '', reference: '', notes: '' };
    this.financeReceiptUrl = null;
    this.financeError.set('');
    this.financeSuccess.set('');
    this.showFinanceForm.set(true);
  }

  editTransaction(txn: FinanceTransaction): void {
    this.editingTransactionId.set(txn.id);
    this.financeForm = {
      date: txn.date.slice(0, 10),
      description: txn.description,
      amount: txn.amount,
      category: txn.category,
      platform: txn.platform ?? '',
      reference: txn.reference ?? '',
      notes: txn.notes ?? '',
    };
    this.financeReceiptUrl = txn.receiptUrl;
    this.financeError.set('');
    this.financeSuccess.set('');
    this.showFinanceForm.set(true);
  }

  saveTransaction(): void {
    this.financeError.set('');
    const id = this.editingTransactionId();
    const body: any = {
      ...this.financeForm,
      platform: this.financeForm.platform || null,
      reference: this.financeForm.reference || null,
      notes: this.financeForm.notes || null,
    };

    if (id) {
      body.receiptUrl = this.financeReceiptUrl ?? '';
      this.http.put(`${environment.apiUrl}/api/finance/${id}`, body).subscribe({
        next: () => {
          this.financeSuccess.set('Transaction updated.');
          this.showFinanceForm.set(false);
          this.loadFinance();
        },
        error: (err) => this.financeError.set(err.error?.message ?? 'Failed to save.'),
      });
    } else {
      this.http.post<FinanceTransaction>(`${environment.apiUrl}/api/finance`, body).subscribe({
        next: (created) => {
          if (this.financeReceiptUrl) {
            this.http.put(`${environment.apiUrl}/api/finance/${created.id}`, { receiptUrl: this.financeReceiptUrl }).subscribe({
              next: () => {
                this.financeSuccess.set('Transaction created.');
                this.showFinanceForm.set(false);
                this.loadFinance();
              },
            });
          } else {
            this.financeSuccess.set('Transaction created.');
            this.showFinanceForm.set(false);
            this.loadFinance();
          }
        },
        error: (err) => this.financeError.set(err.error?.message ?? 'Failed to create.'),
      });
    }
  }

  deleteTransaction(id: string): void {
    if (!confirm('Delete this transaction?')) {
      return;
    }
    this.http.delete(`${environment.apiUrl}/api/finance/${id}`).subscribe({
      next: () => this.loadFinance(),
      error: () => this.financeError.set('Failed to delete transaction.'),
    });
  }

  onReceiptUpload(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) {
      return;
    }
    this.financeReceiptUploading.set(true);
    const formData = new FormData();
    formData.append('file', file);
    this.http.post<{ receiptUrl: string }>(`${environment.apiUrl}/api/finance/upload-receipt`, formData).subscribe({
      next: (res) => {
        this.financeReceiptUrl = res.receiptUrl;
        this.financeReceiptUploading.set(false);
      },
      error: () => {
        this.financeReceiptUploading.set(false);
        this.financeError.set('Receipt upload failed.');
      },
    });
  }

  exportFinanceCsv(): void {
    const filter = this.financeMonthFilter();
    let url = `${environment.apiUrl}/api/finance/export`;
    if (filter !== 'all') {
      const [year, month] = filter.split('-');
      url += `?year=${year}&month=${parseInt(month)}`;
    }
    this.http.get(url, { responseType: 'blob' }).subscribe({
      next: (blob) => {
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = filter === 'all' ? 'transactions-all.csv' : `transactions-${filter}.csv`;
        a.click();
        URL.revokeObjectURL(a.href);
      },
      error: () => this.financeError.set('Export failed.'),
    });
  }

  // Monzo bank methods
  connectMonzo(): void {
    this.http.get<{ url: string; state: string }>(`${environment.apiUrl}/api/monzo/connect`).subscribe({
      next: (res) => {
        localStorage.setItem('monzo_state', res.state);
        window.location.href = res.url;
      },
      error: () => this.monzoError.set('Failed to start Monzo connection.'),
    });
  }

  private handleMonzoCallback(code: string, state: string): void {
    this.activeTab.set('finance');
    this.financeSubTab.set('bank');
    this.monzoLoading.set(true);
    this.monzoError.set('');

    // Clean up URL
    this.router.navigate(['/admin'], { queryParams: {} });

    this.http.post<{ pendingApproval?: boolean }>(`${environment.apiUrl}/api/monzo/callback`, { code, state }).subscribe({
      next: (res) => {
        if (res.pendingApproval) {
          this.monzoPendingApproval.set(true);
          this.monzoLoading.set(false);
        } else {
          this.monzoConnected.set(true);
          this.loadMonzoBalance();
          this.loadMonzoTransactions();
        }
      },
      error: (err) => {
        this.monzoError.set(err.error?.error ?? 'Failed to connect Monzo.');
        this.monzoLoading.set(false);
      },
    });
  }

  disconnectMonzo(): void {
    if (!confirm('Disconnect Monzo? You can reconnect at any time.')) { return; }
    this.http.post(`${environment.apiUrl}/api/monzo/disconnect`, {}).subscribe({
      next: () => {
        this.monzoConnected.set(false);
        this.monzoBalance.set(null);
        this.monzoPots.set([]);
        this.monzoTransactions.set([]);
      },
      error: () => this.monzoError.set('Failed to disconnect.'),
    });
  }

  loadMonzo(): void {
    this.monzoLoading.set(true);
    this.monzoError.set('');
    this.http.get<{ connected: boolean; pendingApproval: boolean }>(`${environment.apiUrl}/api/monzo/status`).subscribe({
      next: (status) => {
        this.monzoConnected.set(status.connected);
        this.monzoPendingApproval.set(status.pendingApproval);
        if (status.connected) {
          this.loadMonzoBalance();
          this.loadMonzoPots();
          this.loadMonzoTransactions();
          this.loadMonzoSummary();
        } else {
          this.monzoLoading.set(false);
        }
      },
      error: () => {
        this.monzoError.set('Failed to check Monzo status.');
        this.monzoLoading.set(false);
      },
    });
  }

  verifyMonzo(): void {
    this.monzoLoading.set(true);
    this.monzoError.set('');
    this.http.post<{ verified: boolean; message?: string }>(`${environment.apiUrl}/api/monzo/verify`, {}).subscribe({
      next: (res) => {
        if (res.verified) {
          this.monzoPendingApproval.set(false);
          this.monzoConnected.set(true);
          this.loadMonzoBalance();
          this.loadMonzoPots();
          this.loadMonzoTransactions();
        } else {
          this.monzoError.set(res.message ?? 'Still waiting for approval in the Monzo app.');
          this.monzoLoading.set(false);
        }
      },
      error: (err) => {
        this.monzoError.set(err.error?.error ?? 'Verification failed.');
        this.monzoLoading.set(false);
      },
    });
  }

  loadMonzoBalance(): void {
    this.http.get<MonzoBalance>(`${environment.apiUrl}/api/monzo/balance`).subscribe({
      next: (balance) => this.monzoBalance.set(balance),
      error: () => {},
    });
  }

  loadMonzoPots(): void {
    this.http.get<MonzoPot[]>(`${environment.apiUrl}/api/monzo/pots`).subscribe({
      next: (pots) => this.monzoPots.set(pots),
      error: () => {},
    });
  }

  loadMonzoTransactions(): void {
    this.monzoLoading.set(true);
    this.http.get<MonzoTransaction[]>(`${environment.apiUrl}/api/monzo/transactions`).subscribe({
      next: (txns) => {
        this.monzoTransactions.set(txns);
        this.monzoLoading.set(false);
      },
      error: () => {
        this.monzoError.set('Failed to load Monzo transactions.');
        this.monzoLoading.set(false);
      },
    });
  }

  syncMonzo(): void {
    this.monzoSyncing.set(true);
    this.monzoError.set('');
    this.http.post(`${environment.apiUrl}/api/monzo/sync`, {}).subscribe({
      next: () => {
        this.monzoSyncing.set(false);
        this.loadMonzoTransactions();
        this.loadMonzoBalance();
        this.loadMonzoPots();
        this.loadMonzoSummary();
      },
      error: () => {
        this.monzoError.set('Sync failed.');
        this.monzoSyncing.set(false);
      },
    });
  }

  annotateMonzoTransaction(txn: MonzoTransaction, patch: Partial<MonzoAnnotatePayload>): void {
    this.monzoAnnotating.set(txn.id);
    this.http.patch<MonzoTransaction>(`${environment.apiUrl}/api/monzo/transactions/${txn.id}/annotate`, patch).subscribe({
      next: (updated) => {
        this.monzoTransactions.update(list => list.map(t => t.id === updated.id ? updated : t));
        this.monzoAnnotating.set(null);
      },
      error: () => this.monzoAnnotating.set(null),
    });
  }

  uploadMonzoReceipt(txn: MonzoTransaction, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) { return; }
    this.monzoReceiptUploading.set(txn.id);
    const formData = new FormData();
    formData.append('file', file);
    this.http.post<MonzoTransaction>(`${environment.apiUrl}/api/monzo/transactions/${txn.id}/upload-receipt`, formData).subscribe({
      next: (updated) => {
        this.monzoTransactions.update(list => list.map(t => t.id === updated.id ? updated : t));
        this.monzoReceiptUploading.set(null);
      },
      error: () => {
        this.monzoError.set('Receipt upload failed.');
        this.monzoReceiptUploading.set(null);
      },
    });
  }

  loadMonzoSummary(): void {
    this.http.get<MonzoSummary>(`${environment.apiUrl}/api/monzo/summary`).subscribe({
      next: (summary) => this.monzoSummary.set(summary),
      error: () => {},
    });
  }

  exportMonzoCsv(): void {
    const filter = this.monzoMonthFilter();
    let url = `${environment.apiUrl}/api/monzo/export`;
    if (filter !== 'all') {
      const [year, month] = filter.split('-');
      url += `?year=${year}&month=${parseInt(month)}`;
    }
    this.http.get(url, { responseType: 'blob' }).subscribe({
      next: (blob) => {
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = filter === 'all' ? 'monzo-all.csv' : `monzo-${filter}.csv`;
        a.click();
        URL.revokeObjectURL(a.href);
      },
      error: () => this.monzoError.set('Export failed.'),
    });
  }

  get filteredMonzoTransactions(): MonzoTransaction[] {
    const filter = this.monzoMonthFilter();
    const txns = this.monzoTransactions();
    if (filter === 'all') { return txns; }
    const [year, month] = filter.split('-').map(Number);
    return txns.filter(t => {
      const d = new Date(t.date);
      return d.getFullYear() === year && d.getMonth() + 1 === month;
    });
  }

  get monzoSelectedMonthSummary(): MonzoSummary['byMonth'][0] | null {
    const summary = this.monzoSummary();
    const filter = this.monzoMonthFilter();
    if (!summary) { return null; }
    if (filter === 'all') {
      return {
        month: 'All Time',
        income: summary.totalIncome,
        expenses: summary.totalExpenses,
        profit: summary.totalProfit,
        count: summary.transactionCount,
        byCategory: summary.byMonth.flatMap(m => m.byCategory),
        byPlatform: summary.byMonth.flatMap(m => m.byPlatform),
      };
    }
    return summary.byMonth.find(m => m.month === filter) ?? null;
  }

  switchFinanceSubTab(tab: 'transactions' | 'bank'): void {
    this.financeSubTab.set(tab);
    if (tab === 'bank' && this.monzoTransactions().length === 0) {
      this.loadMonzo();
    }
  }

  stripHtml(html: string): string {
    const div = document.createElement('div');
    div.innerHTML = html;
    return div.textContent ?? '';
  }

  private savedRange: Range | null = null;

  saveSelection(): void {
    const selection = window.getSelection();
    if (selection && selection.rangeCount > 0) {
      this.savedRange = selection.getRangeAt(0);
    }
  }

  private restoreSelection(): void {
    if (this.savedRange) {
      const selection = window.getSelection();
      if (selection) {
        selection.removeAllRanges();
        selection.addRange(this.savedRange);
      }
    }
  }

  execRichText(command: string): void {
    this.restoreSelection();
    if (command === 'insertParagraph') {
      document.execCommand('insertHTML', false, '<p><br></p>');
    } else if (command === 'insertLineBreak') {
      document.execCommand('insertHTML', false, '<br>');
    } else {
      document.execCommand(command, false);
    }
  }

  onDescriptionKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && event.shiftKey) {
      event.preventDefault();
      document.execCommand('insertHTML', false, '<br>');
    }
  }

  onDescriptionInput(event: Event): void {
    this.form.description = (event.target as HTMLElement).innerHTML;
  }

  onDescriptionPaste(event: ClipboardEvent): void {
    event.preventDefault();
    const html = event.clipboardData?.getData('text/html') ?? '';
    if (html) {
      const cleaned = this.sanitisePastedHtml(html);
      document.execCommand('insertHTML', false, cleaned);
    } else {
      const text = event.clipboardData?.getData('text/plain') ?? '';
      document.execCommand('insertText', false, text);
    }
  }

  private sanitisePastedHtml(html: string): string {
    const parser = new DOMParser();
    const doc = parser.parseFromString(html, 'text/html');
    const allowedTags = new Set(['P', 'BR', 'B', 'STRONG', 'I', 'EM', 'U', 'UL', 'OL', 'LI', 'H2', 'H3', 'H4', 'A']);

    function clean(node: Node): string {
      if (node.nodeType === Node.TEXT_NODE) {
        return node.textContent ?? '';
      }
      if (node.nodeType !== Node.ELEMENT_NODE) {
        return '';
      }
      const el = node as Element;
      const tag = el.tagName;
      const children = Array.from(el.childNodes).map(clean).join('');

      if (allowedTags.has(tag)) {
        if (tag === 'A') {
          const href = el.getAttribute('href');
          if (href && (href.startsWith('http') || href.startsWith('/'))) {
            return `<a href="${href}">${children}</a>`;
          }
          return children;
        }
        return `<${tag.toLowerCase()}>${children}</${tag.toLowerCase()}>`;
      }

      // Convert divs and spans to their content, add line break for block elements
      if (['DIV', 'SECTION', 'ARTICLE', 'HEADER', 'FOOTER', 'BLOCKQUOTE'].includes(tag)) {
        return `<p>${children}</p>`;
      }

      return children;
    }

    return clean(doc.body);
  }
}
