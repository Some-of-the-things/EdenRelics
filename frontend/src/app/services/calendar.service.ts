import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

/** Lifecycle status of an obligation (kebab-case from the backend enum serializer). */
export type LiabilityStatus = 'pending' | 'submitted' | 'paid' | 'complete' | 'waived';

/** One regulatory obligation or free-form calendar event. */
export interface LiabilityObligation {
  id: string;
  kind: string; // 'other' for free-form events; statutory kinds otherwise.
  title: string;
  periodStart: string; // ISO date (YYYY-MM-DD)
  periodEnd: string;
  dueDate: string;
  status: LiabilityStatus;
  isOverdue: boolean;
  scheduledFor: string | null; // ISO datetime
  filedAt: string | null;
  submissionReference: string | null;
  owedAmountMinor: number | null;
  currency: string;
  paidAt: string | null;
  paidAmountMinor: number | null;
  paymentReference: string | null;
  notes: string | null;
}

export interface CalendarConfig {
  ardConfigured: boolean;
  icalEnabled: boolean;
  icalSubscribeUrl: string | null;
}

export interface ScheduleObligationRequest {
  scheduledFor: string; // ISO datetime
}

export interface CompleteObligationRequest {
  submissionReference: string | null;
  paidAmountMinor: number | null;
  paymentReference: string | null;
  paidAt: string | null;
  filedAt: string | null;
  notes: string | null;
}

export interface CreateObligationRequest {
  title: string;
  dueDate: string; // ISO date
  scheduledFor: string | null;
  notes: string | null;
}

@Injectable({ providedIn: 'root' })
export class CalendarService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/api/calendar`;

  config(): Promise<CalendarConfig> {
    return firstValueFrom(this.http.get<CalendarConfig>(`${this.apiUrl}/config`));
  }

  list(opts: { from?: string; to?: string; openOnly?: boolean } = {}): Promise<LiabilityObligation[]> {
    let params = new HttpParams();
    if (opts.from) { params = params.set('from', opts.from); }
    if (opts.to) { params = params.set('to', opts.to); }
    if (opts.openOnly) { params = params.set('openOnly', 'true'); }
    return firstValueFrom(this.http.get<LiabilityObligation[]>(this.apiUrl, { params }));
  }

  schedule(id: string, body: ScheduleObligationRequest): Promise<LiabilityObligation> {
    return firstValueFrom(this.http.post<LiabilityObligation>(`${this.apiUrl}/${id}/schedule`, body));
  }

  unschedule(id: string): Promise<LiabilityObligation> {
    return firstValueFrom(this.http.post<LiabilityObligation>(`${this.apiUrl}/${id}/unschedule`, {}));
  }

  complete(id: string, body: CompleteObligationRequest): Promise<LiabilityObligation> {
    return firstValueFrom(this.http.post<LiabilityObligation>(`${this.apiUrl}/${id}/complete`, body));
  }

  waive(id: string): Promise<LiabilityObligation> {
    return firstValueFrom(this.http.post<LiabilityObligation>(`${this.apiUrl}/${id}/waive`, {}));
  }

  reopen(id: string): Promise<LiabilityObligation> {
    return firstValueFrom(this.http.post<LiabilityObligation>(`${this.apiUrl}/${id}/reopen`, {}));
  }

  create(body: CreateObligationRequest): Promise<LiabilityObligation> {
    return firstValueFrom(this.http.post<LiabilityObligation>(this.apiUrl, body));
  }

  remove(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.apiUrl}/${id}`));
  }
}
