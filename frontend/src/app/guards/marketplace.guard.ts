import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { MarketplaceService } from '../services/marketplace.service';

/**
 * Allows a route only once the multi-seller marketplace is live; otherwise redirects to /shop.
 * Awaits the marketplace flag so gating is correct during server-side rendering too.
 */
export const marketplaceGuard: CanActivateFn = async () => {
  const marketplace = inject(MarketplaceService);
  const router = inject(Router);
  const enabled = await marketplace.ensureLoaded();
  return enabled ? true : router.parseUrl('/shop');
};
