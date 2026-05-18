import { ComponentRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ShareButtonsComponent } from './share-buttons.component';

describe('ShareButtonsComponent', () => {
  let component: ShareButtonsComponent;
  let ref: ComponentRef<ShareButtonsComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [ShareButtonsComponent] });
    const fixture = TestBed.createComponent(ShareButtonsComponent);
    component = fixture.componentInstance;
    ref = fixture.componentRef;
    ref.setInput('url', 'https://edenrelics.co.uk/product/silk-slip-dress');
    ref.setInput('title', '1990s Silk Slip Dress');
    ref.setInput('description', 'A minimalist 90s slip dress.');
    ref.setInput('image', 'https://images.example/abc-1600.webp');
    fixture.detectChanges();
  });

  it('builds a Pinterest share URL with url, media and description', () => {
    const url = new URL(component.pinterestUrl());
    expect(url.origin + url.pathname).toBe('https://www.pinterest.com/pin/create/button/');
    expect(url.searchParams.get('url')).toBe('https://edenrelics.co.uk/product/silk-slip-dress');
    expect(url.searchParams.get('media')).toBe('https://images.example/abc-1600.webp');
    expect(url.searchParams.get('description')).toBe('1990s Silk Slip Dress');
  });

  it('builds a Facebook share URL', () => {
    const url = new URL(component.facebookUrl());
    expect(url.origin + url.pathname).toBe('https://www.facebook.com/sharer/sharer.php');
    expect(url.searchParams.get('u')).toBe('https://edenrelics.co.uk/product/silk-slip-dress');
  });

  it('builds a WhatsApp share URL that contains title and url', () => {
    const url = new URL(component.whatsappUrl());
    expect(url.origin + url.pathname).toBe('https://wa.me/');
    const text = url.searchParams.get('text') ?? '';
    expect(text).toContain('1990s Silk Slip Dress');
    expect(text).toContain('https://edenrelics.co.uk/product/silk-slip-dress');
  });

  it('builds a mailto URL with subject and body', () => {
    const href = component.emailUrl();
    expect(href.startsWith('mailto:?')).toBe(true);
    // mailto URLs aren't URL-parseable in all engines; assert on the query manually.
    const query = href.slice('mailto:?'.length);
    const params = new URLSearchParams(query);
    expect(params.get('subject')).toBe('1990s Silk Slip Dress');
    const body = params.get('body') ?? '';
    expect(body).toContain('A minimalist 90s slip dress.');
    expect(body).toContain('https://edenrelics.co.uk/product/silk-slip-dress');
  });

  it('omits description from email body when none provided', () => {
    ref.setInput('description', '');
    const href = component.emailUrl();
    const params = new URLSearchParams(href.slice('mailto:?'.length));
    expect(params.get('body')).toBe('https://edenrelics.co.uk/product/silk-slip-dress');
  });

  it('uses title as Pinterest description when no description is set', () => {
    ref.setInput('description', '');
    const url = new URL(component.pinterestUrl());
    expect(url.searchParams.get('description')).toBe('1990s Silk Slip Dress');
  });

  it('copyLink writes the URL to clipboard and toggles copied state', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    });
    await component.copyLink();
    expect(writeText).toHaveBeenCalledWith('https://edenrelics.co.uk/product/silk-slip-dress');
    expect(component.copied()).toBe(true);
  });

  it('copyLink silently no-ops when clipboard API rejects', async () => {
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText: vi.fn().mockRejectedValue(new Error('denied')) },
    });
    await component.copyLink();
    expect(component.copied()).toBe(false);
  });
});
