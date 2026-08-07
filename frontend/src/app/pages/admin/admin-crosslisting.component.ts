import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  CrossListingPreview,
  CrossListingService,
  PlatformPlan,
  RelistCandidate,
} from '../../services/cross-listing.service';
import { ProductStore } from '../../store/product.store';
import { Product } from '../../models/product.model';
import { resolveProductStatus } from '../../utils/product-status';

/**
 * Cross-listing readiness for the shop's own stock — we are user zero while the seller beta is gated.
 *
 * Its job is to answer one question honestly: if I pressed publish on this piece, what would actually
 * go out and what wouldn't. So refusals are the primary content, not an error state, and extension
 * platforms show their pasteable content whether or not they could publish.
 */
@Component({
  selector: 'app-admin-crosslisting',
  imports: [FormsModule],
  templateUrl: './admin-crosslisting.component.html',
  styleUrl: './admin-crosslisting.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminCrosslistingComponent {
  private readonly crossListing = inject(CrossListingService);
  readonly store = inject(ProductStore);

  readonly selectedId = signal<string>('');
  readonly preview = signal<CrossListingPreview | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');

  readonly relistCandidates = signal<RelistCandidate[]>([]);
  readonly relistLoading = signal(false);
  readonly relistAsked = signal(false);
  readonly copiedPlatform = signal<string>('');

  /** Live stock only — there is no point asking what would publish for something already sold. */
  readonly listable = computed(() =>
    this.store
      .products()
      .filter((p) => resolveProductStatus(p) === 'live')
      .slice()
      .sort((a, b) => a.name.localeCompare(b.name)),
  );

  readonly readyCount = computed(
    () => this.preview()?.platforms.filter((p) => p.validation.canPublish).length ?? 0,
  );

  select(productId: string): void {
    this.selectedId.set(productId);
    this.preview.set(null);
    this.error.set('');
    if (productId) {
      this.load(productId);
    }
  }

  private load(productId: string): void {
    this.loading.set(true);
    this.crossListing.preview(productId).subscribe({
      next: (preview) => {
        this.preview.set(preview);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(
          err.status === 404
            ? 'That piece no longer exists.'
            : (err.error?.error ?? 'Could not work out cross-listing readiness.'),
        );
      },
    });
  }

  /**
   * Asked, never pushed. This method exists because someone pressed a button — see the note on the
   * service about not turning it into a notification.
   */
  askForRelistCandidates(): void {
    this.relistLoading.set(true);
    this.relistAsked.set(true);
    this.crossListing.relistCandidates().subscribe({
      next: (candidates) => {
        this.relistCandidates.set(candidates);
        this.relistLoading.set(false);
      },
      error: () => {
        this.relistLoading.set(false);
        this.error.set('Could not load relist candidates.');
      },
    });
  }

  /** Hands the seller the listing text. The whole point of the fallback is that it's usable. */
  async copyFallback(plan: PlatformPlan): Promise<void> {
    if (!plan.fallback) {
      return;
    }
    const text = `${plan.fallback.title}\n\n${plan.fallback.description}\n\n£${plan.fallback.price.toFixed(2)}`;
    try {
      await navigator.clipboard.writeText(text);
      this.copiedPlatform.set(plan.platform);
      setTimeout(() => this.copiedPlatform.set(''), 2000);
    } catch {
      // Clipboard can be refused (permissions, insecure context). The text is on screen regardless.
      this.error.set('Could not copy — select the text below and copy it manually.');
    }
  }

  transportLabel(plan: PlatformPlan): string {
    return plan.transport === 'server-api'
      ? 'Our server — works whether your machine is on or not'
      : 'Your browser — needs it open and awake';
  }

  fieldRows(plan: PlatformPlan): { key: string; value: string }[] {
    return Object.entries(plan.fields).map(([key, value]) => ({ key, value }));
  }

  productLabel(p: Product): string {
    return `${p.sku ? p.sku + ' · ' : ''}${p.name}`;
  }
}
