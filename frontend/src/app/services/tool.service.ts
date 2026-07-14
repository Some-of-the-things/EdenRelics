import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

// --- Shapes returned by the seller tool (seller-tool/Api) ---

export interface GarmentSummary {
  id: string;
  title?: string;
  sellerRef?: string;
  reference?: string;
  createdAtUtc: string;
  evidenceCount: number;
  latestEarliest?: number;
  latestLatest?: number;
  latestOutcome?: string;
  latestConfirmation?: string;
}

export interface ToolEvidence {
  id: string;
  type: string;
  feature: string;
  rawValue?: string;
  imageKey?: string;
  origin: string;
  confirmation: string;
}

export interface ToolEstimate {
  id: string;
  earliest?: number;
  latest?: number;
  outcome: string;
  confirmation: string;
  computedAtUtc: string;
}

export interface GarmentDetail {
  id: string;
  title?: string;
  sellerRef?: string;
  reference?: string;
  evidence: ToolEvidence[];
  estimates: ToolEstimate[];
}

export interface CreateGarment {
  title?: string;
  sellerRef?: string;
  reference?: string;
}

export interface AddEvidence {
  type: string;
  feature: string;
  rawValue?: string;
  origin?: string;
}

export interface DateResultChain {
  ruleId: string;
  feature: string;
  bound: string;
  strength: string;
  source?: string;
}

export interface DateResult {
  earliest?: number;
  latest?: number;
  outcome: string;
  claimFlag?: { strength: string; message: string };
  evidence: DateResultChain[];
}

/**
 * Client for the standalone seller tool (dating engine + evidence archive), which lives on its own
 * origin (`environment.toolApiUrl`). The shared auth interceptor only attaches the bearer to the main
 * API origin, so this service attaches it explicitly. Browser-only: `getToken()` returns null during
 * SSR, so components must load data after render (the route is client-rendered anyway).
 */
@Injectable({ providedIn: 'root' })
export class ToolService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly base = environment.toolApiUrl;

  private authHeaders(): { Authorization: string } {
    return { Authorization: `Bearer ${this.auth.getToken() ?? ''}` };
  }

  listGarments(): Observable<GarmentSummary[]> {
    return this.http.get<GarmentSummary[]>(`${this.base}/garments`, { headers: this.authHeaders() });
  }

  getGarment(id: string): Observable<GarmentDetail> {
    return this.http.get<GarmentDetail>(`${this.base}/garments/${id}`, { headers: this.authHeaders() });
  }

  createGarment(dto: CreateGarment): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base}/garments`, dto, { headers: this.authHeaders() });
  }

  addEvidence(garmentId: string, dto: AddEvidence): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base}/garments/${garmentId}/evidence`, dto, { headers: this.authHeaders() });
  }

  /** Upload a label/flat-lay photo (multipart). Requires the tool's R2 storage to be configured. */
  capture(garmentId: string, file: File, type: string, feature: string): Observable<{ id: string; imageKey: string }> {
    const form = new FormData();
    form.append('file', file);
    form.append('type', type);
    form.append('feature', feature);
    // Don't set Content-Type — the browser adds the multipart boundary.
    return this.http.post<{ id: string; imageKey: string }>(`${this.base}/garments/${garmentId}/capture`, form, { headers: this.authHeaders() });
  }

  runDating(garmentId: string, claim?: { earliest?: number; latest?: number }): Observable<DateResult> {
    const body = { claimEarliest: claim?.earliest ?? null, claimLatest: claim?.latest ?? null };
    return this.http.post<DateResult>(`${this.base}/garments/${garmentId}/date`, body, { headers: this.authHeaders() });
  }
}
