import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

/**
 * Thin wrapper over the mailing-list subscribe endpoint. Subscribing is
 * idempotent server-side (re-subscribes a previously unsubscribed address,
 * otherwise no-ops), so callers can post freely. `source` segments where the
 * signup came from (e.g. 'Homepage', 'Discount Popup').
 */
@Injectable({ providedIn: 'root' })
export class NewsletterService {
  private readonly http = inject(HttpClient);

  subscribe(email: string, source: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${environment.apiUrl}/api/mailing-list/subscribe`,
      { email, source },
    );
  }
}
