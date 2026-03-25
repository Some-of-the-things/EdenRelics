import { HttpInterceptorFn } from '@angular/common/http';
import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { catchError, switchMap, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService, AuthResponse } from '../services/auth.service';

let isRefreshing = false;

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const platformId = inject(PLATFORM_ID);

  if (!isPlatformBrowser(platformId)) return next(req);
  if (!req.url.startsWith(environment.apiUrl)) return next(req);

  const token = localStorage.getItem('eden_token');
  if (!token) return next(req);

  const authedReq = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

  // Don't intercept the refresh call itself
  if (req.url.includes('/auth/refresh')) return next(authedReq);

  const auth = inject(AuthService);
  const http = inject(HttpClient);

  return next(authedReq).pipe(
    catchError((err) => {
      if (err.status === 401 && !isRefreshing) {
        isRefreshing = true;
        return http.post<AuthResponse>(
          `${environment.apiUrl}/api/auth/refresh`,
          {},
          { headers: { Authorization: `Bearer ${token}` } }
        ).pipe(
          switchMap((res) => {
            isRefreshing = false;
            localStorage.setItem('eden_token', res.token);
            localStorage.setItem('eden_user', JSON.stringify(res.user));
            auth.currentUser.set(res.user);

            // Retry the original request with the new token
            const retryReq = req.clone({
              setHeaders: { Authorization: `Bearer ${res.token}` }
            });
            return next(retryReq);
          }),
          catchError((refreshErr) => {
            isRefreshing = false;
            auth.logout();
            return throwError(() => refreshErr);
          })
        );
      }
      return throwError(() => err);
    })
  );
};
