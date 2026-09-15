import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { HomeComponent } from './home.component';
import { Product } from '../../models/product.model';
import { HomeSection } from '../../services/home-sections.service';
import { environment } from '../../../environments/environment';

const api = environment.apiUrl;

function product(id: string, overrides: Partial<Product> = {}): Product {
  return {
    id,
    name: `Dress ${id}`,
    slug: `dress-${id}`,
    description: '',
    price: 40,
    era: '1970s',
    category: '70s',
    size: '10',
    condition: 'good',
    imageUrl: `${id}.webp`,
    inStock: true,
    status: 'live',
    ...overrides,
  };
}

function section(kind: HomeSection['kind'], title: string, overrides: Partial<HomeSection> = {}): HomeSection {
  return {
    kind,
    eyebrow: '',
    title,
    description: '',
    buttonText: '',
    buttonLink: '',
    visible: true,
    productIds: [],
    ...overrides,
  };
}

describe('HomeComponent sections', () => {
  let http: HttpTestingController;

  async function render(opts: { sections?: HomeSection[] | 'error'; products: Product[]; topPicks?: string[] }) {
    TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: PLATFORM_ID, useValue: 'server' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(HomeComponent);
    fixture.detectChanges();

    http.match(`${api}/api/products`).forEach((r) => r.flush(opts.products));
    const picks = opts.topPicks ?? [];
    http
      .match(`${api}/api/top-picks`)
      .forEach((r) => r.flush({ enabled: picks.length > 0, productIds: picks, featuredProductIds: picks }));
    http.match(`${api}/api/home-sections`).forEach((r) =>
      opts.sections === 'error'
        ? r.flush('boom', { status: 500, statusText: 'Server Error' })
        : r.flush(opts.sections ?? []),
    );
    // Anything else the page asks for (blog, reviews, content) is irrelevant here.
    http.match(() => true).forEach((r) => r.flush([]));
    // Top Picks resolves through a promise, so let it settle before reading the page.
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  function headings(fixture: Awaited<ReturnType<typeof render>>): string[] {
    return Array.from(fixture.nativeElement.querySelectorAll('.home__featured-title') as NodeListOf<HTMLElement>).map(
      (h) => h.textContent!.trim(),
    );
  }

  it('renders strips in the saved order with the saved wording', async () => {
    const fixture = await render({
      products: [product('a'), product('b')],
      topPicks: ['a'],
      sections: [
        section('custom', 'Autumn Knits', { productIds: ['b'], buttonText: 'Shop the sale', buttonLink: '/shop/sale' }),
        section('top-picks', "Editor's Favourites"),
        section('latest-collection', 'The Wildflower Edit'),
      ],
    });

    // The latest collection has no live pieces in this catalogue, so it stays out.
    expect(headings(fixture)).toEqual(['Autumn Knits', "Editor's Favourites"]);
    const button = fixture.nativeElement.querySelector('.home__featured-btn') as HTMLAnchorElement;
    expect(button.textContent!.trim()).toBe('Shop the sale');
    expect(button.getAttribute('href')).toBe('/shop/sale');
  });

  it('leaves out hidden sections and sections with nothing live to show', async () => {
    const fixture = await render({
      products: [product('a'), product('sold', { status: 'sold', inStock: false })],
      sections: [
        section('top-picks', 'Our Top Picks'),
        section('custom', 'Hidden', { productIds: ['a'], visible: false }),
        section('custom', 'Only Sold', { productIds: ['sold'] }),
        section('custom', 'Shown', { productIds: ['a'] }),
        section('latest-collection', 'The Wildflower Edit'),
      ],
    });

    // Top Picks is switched off (no picks), so it stays out too.
    expect(headings(fixture)).toEqual(['Shown']);
  });

  it('keeps the original Top Picks strip when the sections cannot be loaded', async () => {
    const fixture = await render({ products: [product('a')], topPicks: ['a'], sections: 'error' });

    expect(headings(fixture)).toEqual(['Our Top Picks']);
  });
});
