import { HttpInterceptorFn } from '@angular/common/http';
import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const platformId = inject(PLATFORM_ID);

  if (!isPlatformBrowser(platformId)) return next(req);
  if (!req.url.startsWith(environment.apiUrl)) return next(req);

  const token = localStorage.getItem('eden_token');
  if (!token) return next(req);

  const auth = inject(AuthService);

  return next(
    req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
  ).pipe(
    tap({
      error: (err) => {
        if (err.status === 401) {
          auth.logout();
        }
      },
    })
  );
};
