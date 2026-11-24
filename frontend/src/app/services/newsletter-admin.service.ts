import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export type NewsletterStatus = 'Draft' | 'Scheduled' | 'Sending' | 'Sent';
export type EditorMode = 'visual' | 'html';

export interface Newsletter {
  id: string;
  subject: string;
  previewText: string;
  bodyHtml: string;
  editorMode: EditorMode;
  status: NewsletterStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
  scheduledForUtc: string | null;
  sendStartedAtUtc: string | null;
  sentAtUtc: string | null;
  recipientCount: number;
  sentCount: number;
  failedCount: number;
  skippedCount: number;
  lastError: string | null;
  openedCount?: number;
  clickedCount?: number;
  bouncedCount?: number;
}

export interface SaveNewsletter {
  subject: string;
  previewText: string;
  bodyHtml: string;
  editorMode: EditorMode;
}

export interface NewsletterPreview {
  html: string;
  text: string;
  /** The body after clean-up: what the visual editor shows. */
  bodyHtml: string;
}

export interface AdminSubscriber {
  id: string;
  email: string;
  firstName: string | null;
  source: string;
  signedUpAtUtc: string;
  unsubscribed: boolean;
  unsubscribedAtUtc: string | null;
}

/** Admin calls for the Newsletter tab: drafts, preview, test, send, and the subscriber list. */
@Injectable({ providedIn: 'root' })
export class NewsletterAdminService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/api/newsletters`;
  private readonly listUrl = `${environment.apiUrl}/api/mailing-list`;

  list(): Observable<Newsletter[]> {
    return this.http.get<Newsletter[]>(this.url);
  }

  get(id: string): Observable<Newsletter> {
    return this.http.get<Newsletter>(`${this.url}/${id}`);
  }

  create(body: SaveNewsletter): Observable<Newsletter> {
    return this.http.post<Newsletter>(this.url, body);
  }

  update(id: string, body: SaveNewsletter): Observable<Newsletter> {
    return this.http.put<Newsletter>(`${this.url}/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  preview(body: { subject: string; previewText: string; bodyHtml: string }): Observable<NewsletterPreview> {
    return this.http.post<NewsletterPreview>(`${this.url}/preview`, body);
  }

  sendTest(id: string, email: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.url}/${id}/test`, { email });
  }

  /** Sends now, or schedules when a future UTC time is given. */
  send(id: string, scheduledForUtc: string | null): Observable<Newsletter> {
    return this.http.post<Newsletter>(`${this.url}/${id}/send`, { scheduledForUtc });
  }

  cancelSchedule(id: string): Observable<Newsletter> {
    return this.http.post<Newsletter>(`${this.url}/${id}/cancel`, {});
  }

  /** A JPEG copy of a product photo, for email apps that can't show WebP (Outlook on Windows). */
  emailImage(url: string): Observable<{ url: string }> {
    return this.http.post<{ url: string }>(`${this.url}/email-image`, { url });
  }

  audience(): Observable<{ activeCount: number }> {
    return this.http.get<{ activeCount: number }>(`${this.url}/audience`);
  }

  subscribers(search: string, status: 'all' | 'active' | 'unsubscribed'): Observable<AdminSubscriber[]> {
    const params = new URLSearchParams();
    if (search.trim()) {
      params.set('search', search.trim());
    }
    if (status !== 'all') {
      params.set('status', status);
    }
    const qs = params.toString();
    return this.http.get<AdminSubscriber[]>(`${this.listUrl}/admin/subscribers${qs ? `?${qs}` : ''}`);
  }

  exportCsv(status: 'all' | 'active' | 'unsubscribed'): Observable<Blob> {
    const qs = status === 'all' ? '' : `?status=${status}`;
    return this.http.get(`${this.listUrl}/admin/subscribers.csv${qs}`, { responseType: 'blob' });
  }

  markUnsubscribed(id: string): Observable<void> {
    return this.http.post<void>(`${this.listUrl}/admin/subscribers/${id}/unsubscribe`, {});
  }

  removeSubscriber(id: string): Observable<void> {
    return this.http.delete<void>(`${this.listUrl}/admin/subscribers/${id}`);
  }
}
