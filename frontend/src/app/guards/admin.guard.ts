import { inject, isDevMode } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  if (auth.isAuthenticated()) {
    return true;
  }
  return inject(Router).createUrlTree(['/login']);
};

export const adminGuard: CanActivateFn = () => {
  if (isDevMode()) {
    return true;
  }
  const auth = inject(AuthService);
  if (auth.isAdmin()) {
    return true;
  }
  return inject(Router).createUrlTree(['/login']);
};
