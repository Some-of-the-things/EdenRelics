import { ChangeDetectionStrategy, Component, HostListener, inject, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CartStore } from '../../store/cart.store';
import { AuthService } from '../../services/auth.service';
import { BrandingService } from '../../services/branding.service';
import { ContentService } from '../../services/content.service';

@Component({
  selector: 'app-header',
  imports: [RouterLink],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeaderComponent {
  readonly cartStore = inject(CartStore);
  readonly auth = inject(AuthService);
  readonly cms = inject(ContentService);
  private readonly brandingService = inject(BrandingService);
  readonly logoUrl = computed(() => this.brandingService.branding()?.logoUrl ?? 'logo.png');

  /** Whether the Shop dropdown is expanded. */
  readonly shopOpen = signal(false);

  toggleShop(event: Event): void {
    // Stop the click bubbling to the document listener, which would otherwise
    // immediately close the menu we're trying to open.
    event.stopPropagation();
    this.shopOpen.update((open) => !open);
  }

  closeShop(): void {
    this.shopOpen.set(false);
  }

  @HostListener('document:click')
  onDocumentClick(): void {
    if (this.shopOpen()) {
      this.closeShop();
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.shopOpen()) {
      this.closeShop();
    }
  }
}
