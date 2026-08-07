import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

/**
 * How a listing physically reaches a platform. Hybrid by design: a real API where one exists, the
 * seller's own browser where none does. Serialised kebab-cased by the API.
 */
export type ListingTransport = 'server-api' | 'seller-browser-extension';

/** Whether we actually know a platform's field requirements yet. */
export type FieldMappingResearch = 'documented' | 'unresearched';

export interface ListingProblem {
  field: string;
  problem: string;
  fix: string | null;
}

export interface ListingValidation {
  blocking: ListingProblem[];
  warnings: ListingProblem[];
  canPublish: boolean;
}

/** Listing content in a form a human can paste, so an extension failure never leaves nothing. */
export interface PasteFallback {
  title: string;
  description: string;
  price: number;
}

export interface PlatformPlan {
  platform: string;
  transport: ListingTransport;
  research: FieldMappingResearch;
  validation: ListingValidation;
  fields: Record<string, string>;
  fallback: PasteFallback | null;
}

export interface CrossListingPreview {
  productId: string;
  sku: string;
  title: string;
  price: number;
  platforms: PlatformPlan[];
}

export interface RelistCandidate {
  productId: string;
  sku: string;
  title: string;
  platform: string;
  daysListed: number;
}

/**
 * Admin client for cross-listing readiness. Hits the main API, so the shared auth interceptor
 * attaches the bearer — unlike the seller-tool origin, which needs it explicitly.
 */
@Injectable({ providedIn: 'root' })
export class CrossListingService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/cross-listing`;

  /** What each platform would do with this piece right now, and what stops it. */
  preview(productId: string): Observable<CrossListingPreview> {
    return this.http.get<CrossListingPreview>(`${this.base}/preview/${productId}`);
  }

  /**
   * Pieces old enough to consider relisting. Pull-only: this is asked, never pushed. Do not drive a
   * badge or notification from it — a tool that keeps suggesting relisting trains the seller into the
   * machine-timed bumping pattern Vinted penalises, and the penalty lands on their account.
   */
  relistCandidates(): Observable<RelistCandidate[]> {
    return this.http.get<RelistCandidate[]>(`${this.base}/relist-candidates`);
  }
}
