import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HeaderComponent } from './header.component';

describe('HeaderComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HeaderComponent],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(HeaderComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should display Eden Relics logo text', () => {
    const fixture = TestBed.createComponent(HeaderComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Eden Relics');
  });

  it('should show cart badge when items in cart', () => {
    const fixture = TestBed.createComponent(HeaderComponent);
    const component = fixture.componentInstance;
    component.cartStore.addToCart({
      id: '1', name: 'Test', description: '', price: 10,
      era: '1970s', category: '70s', size: 'M',
      condition: 'good', imageUrl: '', inStock: true,
    });
    fixture.detectChanges();
    const badge = fixture.nativeElement.querySelector('.header__badge');
    expect(badge).toBeTruthy();
    expect(badge.textContent.trim()).toBe('1');
  });
});
