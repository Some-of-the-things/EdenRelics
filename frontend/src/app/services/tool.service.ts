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
  /** What the claim rests on (PrimaryLegislation … CommunityConsensus) — not how hard it is. */
  provenance?: string;
  /** The rule's id in the research document, e.g. "CARE-04". */
  specId?: string;
  /** False when a transition group superseded this bound; the reason says which. */
  applied?: boolean;
  exclusionReason?: string;
}

export interface DateResult {
  earliest?: number;
  latest?: number;
  outcome: string;
  claimFlag?: { strength: string; message: string };
  evidence: DateResultChain[];
}

export interface CaptureResult {
  id: string;
  imageKey: string;
  displayImageKey?: string;
  slot: string;
  width?: number;
  height?: number;
}

export interface CaptureStandardSlot {
  slot: string;
  required: boolean;
  minimumLongEdge: number;
  guidance: string;
}

export interface CaptureStandard {
  version: string;
  maxBytes: number;
  acceptedContentTypes: string[];
  slots: CaptureStandardSlot[];
}

/** One bound the engine derived, with the reasoning attached — the point of the whole design. */
export interface DatingChainLink {
  ruleId: string;
  specId: string;
  feature: string;
  bound: string;
  strength: 'Hard' | 'Soft';
  provenance: string;
  /** False when the bound was computed but set aside (currently: superseded by a transition group). */
  applied: boolean;
  exclusionReason: string | null;
  source: string | null;
}

export interface DatingPreview {
  earliest: number | null;
  latest: number | null;
  outcome: 'Estimated' | 'HardContradiction' | 'SoftContradiction';
  range: string;
  claimFlag: { strength: 'Hard' | 'Soft'; message: string } | null;
  evidence: DatingChainLink[];
}

export interface DatingFeature {
  feature: string;
  type: string;
  matchKind: string;
  specIds: string[];
  notBefore: number | null;
  notAfter: number | null;
  strength: 'Hard' | 'Soft';
  /** Value-matching rules do nothing without the label text they match against. */
  needsValue: boolean;
}

export interface CaptureCompleteness {
  isComplete: boolean;
  captureCount: number;
  missingRequired: string[];
  missingRequested: string[];
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

  /**
   * Upload a label/flat-lay photo (multipart). Requires the tool's R2 storage to be configured.
   *
   * `slot` names which shot this is, so the server can apply the right resolution floor — a care
   * label needs more pixels than a flat-lay, because it is the part something has to still be able
   * to read years later. `archiveRights` is recorded per capture rather than per account, so the
   * archive's provenance survives a seller leaving or the terms changing; the server refuses the
   * upload without it.
   */
  capture(
    garmentId: string,
    file: File,
    type: string,
    feature: string,
    slot: string,
    archiveRights: boolean,
  ): Observable<CaptureResult> {
    const form = new FormData();
    form.append('file', file);
    form.append('type', type);
    form.append('feature', feature);
    form.append('slot', slot);
    form.append('archiveRights', String(archiveRights));
    // Don't set Content-Type — the browser adds the multipart boundary.
    return this.http.post<CaptureResult>(`${this.base}/garments/${garmentId}/capture`, form, { headers: this.authHeaders() });
  }

  /** The capture standard, so the UI renders slots and guidance from the server's definition. */
  captureStandard(): Observable<CaptureStandard> {
    return this.http.get<CaptureStandard>(`${this.base}/capture-standard`, { headers: this.authHeaders() });
  }

  /** What is still missing before this garment meets the standard. */
  captureCompleteness(garmentId: string): Observable<CaptureCompleteness> {
    return this.http.get<CaptureCompleteness>(
      `${this.base}/garments/${garmentId}/captures/completeness`, { headers: this.authHeaders() });
  }

  runDating(garmentId: string, claim?: { earliest?: number; latest?: number }): Observable<DateResult> {
    const body = { claimEarliest: claim?.earliest ?? null, claimLatest: claim?.latest ?? null };
    return this.http.post<DateResult>(`${this.base}/garments/${garmentId}/date`, body, { headers: this.authHeaders() });
  }

  /**
   * Runs the engine on ad-hoc observations and returns the full reasoning, without creating a
   * garment or storing an estimate. Admin only. This is what the admin dating bench uses: dating
   * through {@link runDating} would seed throwaway garments into the archive, and the archive is
   * the asset.
   */
  datingPreview(
    evidence: { feature: string; type?: string; rawValue?: string | null }[],
    claim?: { earliest?: number | null; latest?: number | null },
  ): Observable<DatingPreview> {
    const body = {
      evidence,
      claimEarliest: claim?.earliest ?? null,
      claimLatest: claim?.latest ?? null,
    };
    return this.http.post<DatingPreview>(`${this.base}/dating/preview`, body, { headers: this.authHeaders() });
  }

  /** The features the live rule set can act on, so the UI never offers one no rule matches. */
  datingFeatures(): Observable<DatingFeature[]> {
    return this.http.get<DatingFeature[]>(`${this.base}/dating/features`, { headers: this.authHeaders() });
  }
}
