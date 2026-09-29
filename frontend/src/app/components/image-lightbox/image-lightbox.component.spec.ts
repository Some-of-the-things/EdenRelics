import { TestBed, ComponentFixture } from '@angular/core/testing';
import { ImageLightboxComponent } from './image-lightbox.component';

describe('ImageLightboxComponent', () => {
  let fixture: ComponentFixture<ImageLightboxComponent>;
  let component: ImageLightboxComponent;

  const images = [
    'https://cdn.example/a-1600.webp',
    'https://cdn.example/b-1600.webp',
    'https://cdn.example/c-1600.webp',
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ImageLightboxComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ImageLightboxComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('images', images);
    fixture.componentRef.setInput('alt', 'Floral maxi dress');
    fixture.detectChanges();
  });

  it('shows the largest variant of the current photo', () => {
    const img: HTMLImageElement = fixture.nativeElement.querySelector('.lightbox__image');
    expect(img.getAttribute('src')).toBe('https://cdn.example/a-1600.webp');
  });

  it('wraps around when stepping past either end', () => {
    component.prev();
    expect(component.index()).toBe(2);
    component.next();
    expect(component.index()).toBe(0);
  });

  it('handles arrow keys and closes on Escape', () => {
    let closed = false;
    component.closed.subscribe(() => (closed = true));

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight' }));
    expect(component.index()).toBe(1);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(closed).toBe(true);
  });

  it('zooms out before closing on Escape', () => {
    let closed = false;
    component.closed.subscribe(() => (closed = true));
    component.zoomed.set(true);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(component.zoomed()).toBe(false);
    expect(closed).toBe(false);
  });

  it('locks page scroll while open and restores it on destroy', () => {
    expect(document.body.style.overflow).toBe('hidden');
    fixture.destroy();
    expect(document.body.style.overflow).toBe('');
  });

  it('hides navigation for a single photo', () => {
    fixture.componentRef.setInput('images', [images[0]]);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.lightbox__nav')).toBeNull();
  });
});
