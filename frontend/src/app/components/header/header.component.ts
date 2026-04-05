import { ChangeDetectionStrategy, Component, inject, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CartStore } from '../../store/cart.store';
import { AuthService } from '../../services/auth.service';
import { BrandingService } from '../../services/branding.service';

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
  private readonly brandingService = inject(BrandingService);
  readonly logoUrl = computed(() => this.brandingService.branding()?.logoUrl ?? 'logo.webp');
}
