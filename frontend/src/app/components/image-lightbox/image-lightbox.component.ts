import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  OnDestroy,
  OnInit,
  computed,
  inject,
  input,
  model,
  output,
  signal,
} from '@angular/core';
import { FocusTrapDirective } from '../../directives/focus-trap.directive';
import { imageSrcAt, VARIANT_WIDTHS } from '../../utils/image-variant-loader';

/** How far a touch has to travel sideways before it counts as a swipe. */
const SWIPE_THRESHOLD_PX = 50;
const ZOOM_SCALE = 2.5;

/**
 * Full-screen photo viewer for a listing. Shows the largest stored variant,
 * steps through the gallery (buttons, arrow keys, swipe), and zooms in on a
 * click — the zoomed image follows the pointer so a buyer can inspect a label,
 * a seam or a mark in the fabric. Create it inside an @if so the focus trap
 * and the body scroll lock last exactly as long as it is open.
 */
@Component({
  selector: 'app-image-lightbox',
  imports: [FocusTrapDirective],
  templateUrl: './image-lightbox.component.html',
  styleUrl: './image-lightbox.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '(document:keydown)': 'onKeydown($event)',
  },
})
export class ImageLightboxComponent implements OnInit, OnDestroy {
  private readonly document = inject(DOCUMENT);

  readonly images = input.required<string[]>();
  readonly alt = input('');
  readonly index = model(0);
  readonly closed = output<void>();

  readonly zoomed = signal(false);
  /** Transform origin for the zoomed image, as percentages of its box. */
  readonly origin = signal({ x: 50, y: 50 });

  readonly largestWidth = VARIANT_WIDTHS[VARIANT_WIDTHS.length - 1];
  readonly currentSrc = computed(() => imageSrcAt(this.images()[this.index()], this.largestWidth));
  readonly hasMany = computed(() => this.images().length > 1);

  private previousOverflow = '';
  private touchStartX: number | null = null;

  ngOnInit(): void {
    const body = this.document.body;
    this.previousOverflow = body.style.overflow;
    body.style.overflow = 'hidden';
  }

  ngOnDestroy(): void {
    this.document.body.style.overflow = this.previousOverflow;
  }

  close(): void {
    this.closed.emit();
  }

  next(): void {
    this.step(1);
  }

  prev(): void {
    this.step(-1);
  }

  private step(delta: number): void {
    const count = this.images().length;
    if (count < 2) {
      return;
    }
    this.zoomed.set(false);
    this.index.set((this.index() + delta + count) % count);
  }

  toggleZoom(event: MouseEvent): void {
    this.updateOrigin(event);
    this.zoomed.update((z) => !z);
  }

  onPointerMove(event: MouseEvent): void {
    if (this.zoomed()) {
      this.updateOrigin(event);
    }
  }

  private updateOrigin(event: MouseEvent): void {
    const box = (event.currentTarget as HTMLElement).getBoundingClientRect();
    if (box.width === 0 || box.height === 0) {
      return;
    }
    const x = ((event.clientX - box.left) / box.width) * 100;
    const y = ((event.clientY - box.top) / box.height) * 100;
    this.origin.set({ x: Math.min(100, Math.max(0, x)), y: Math.min(100, Math.max(0, y)) });
  }

  readonly transform = computed(() => (this.zoomed() ? `scale(${ZOOM_SCALE})` : 'none'));
  readonly transformOrigin = computed(() => `${this.origin().x}% ${this.origin().y}%`);

  onTouchStart(event: TouchEvent): void {
    this.touchStartX = event.touches.length === 1 ? event.touches[0].clientX : null;
  }

  onTouchEnd(event: TouchEvent): void {
    if (this.touchStartX === null || this.zoomed()) {
      this.touchStartX = null;
      return;
    }
    const dx = event.changedTouches[0].clientX - this.touchStartX;
    this.touchStartX = null;
    if (Math.abs(dx) < SWIPE_THRESHOLD_PX) {
      return;
    }
    if (dx < 0) {
      this.next();
    } else {
      this.prev();
    }
  }

  onKeydown(event: KeyboardEvent): void {
    switch (event.key) {
      case 'Escape':
        event.preventDefault();
        if (this.zoomed()) {
          this.zoomed.set(false);
        } else {
          this.close();
        }
        break;
      case 'ArrowRight':
        event.preventDefault();
        this.next();
        break;
      case 'ArrowLeft':
        event.preventDefault();
        this.prev();
        break;
    }
  }
}
