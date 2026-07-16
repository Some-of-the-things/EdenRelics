import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { TopPicksService } from '../services/top-picks.service';

/**
 * Allows a route only once the curated "Our Top Picks" edit is switched on (TopPicks:Enabled);
 * otherwise redirects to /shop. Awaits the flag so gating is correct during server-side rendering too.
 */
export const topPicksGuard: CanActivateFn = async () => {
  const topPicks = inject(TopPicksService);
  const router = inject(Router);
  const enabled = await topPicks.ensureLoaded();
  return enabled ? true : router.parseUrl('/shop');
};
