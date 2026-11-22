import { TestBed } from '@angular/core/testing';
import { productCardsHtml } from './admin-newsletter.component';
import { ReviewCardComponent } from '../../components/reviews/review-card.component';
import { ShopReview } from '../../components/reviews/reviews.data';

describe('newsletter product cards', () => {
  const piece = {
    id: 'p1',
    slug: 'laura-ashley-prairie-dress',
    name: 'Laura Ashley "Prairie" Dress <1980s>',
    price: 85,
    salePrice: null,
    imageUrl: 'https://img.example/p1.webp',
  };

  it('links each piece to its page, with image, name and price, two to a row', () => {
    const html = productCardsHtml([piece, { ...piece, id: 'p2', slug: 'midi', name: 'Midi', price: 40.5 }, { ...piece, id: 'p3', slug: 'maxi', name: 'Maxi' }], 'https://edenrelics.co.uk');
    expect(html).toContain('href="https://edenrelics.co.uk/product/laura-ashley-prairie-dress"');
    expect(html).toContain('src="https://img.example/p1.webp"');
    expect(html).toContain('£85');
    expect(html).toContain('£40.50');
    expect(html.match(/<tr>/g)?.length).toBe(2);
  });

  it('escapes names so a piece title cannot break the email', () => {
    const html = productCardsHtml([piece], 'https://edenrelics.co.uk');
    expect(html).toContain('Laura Ashley &quot;Prairie&quot; Dress &lt;1980s&gt;');
    expect(html).not.toContain('<1980s>');
  });

  it('shows a reduced price with the original struck through', () => {
    const html = productCardsHtml([{ ...piece, salePrice: 60 }], 'https://edenrelics.co.uk');
    expect(html).toContain('£60');
    expect(html).toContain('line-through');
  });
});

describe('review card labels', () => {
  const base: ShopReview = { name: 'A', source: 'buyer', rating: 5, text: 'Lovely', showOnHome: false, showOnProduct: false };

  function tagFor(review: ShopReview): string {
    const fixture = TestBed.createComponent(ReviewCardComponent);
    fixture.componentRef.setInput('review', review);
    fixture.detectChanges();
    return (fixture.nativeElement as HTMLElement).querySelector('.review-card__tag')!.textContent!.trim();
  }

  it('marks imported reviews as from our other shops, without naming the site', () => {
    expect(tagFor(base)).toBe('From our other shops');
  });

  it('marks reviews of orders placed here as verified purchases, with the reply', () => {
    const fixture = TestBed.createComponent(ReviewCardComponent);
    fixture.componentRef.setInput('review', { ...base, source: 'verified', pieces: ['Midi Dress'], reply: 'Thank you!' });
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.review-card__tag')!.textContent!.trim()).toBe('Verified purchase');
    expect(el.textContent).toContain('Midi Dress');
    expect(el.textContent).toContain('Reply from Eden Relics');
  });
});
