import { Component, inject, signal, OnInit, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MonoTypeOperatorFunction, retry, timer, throwError } from 'rxjs';
import { isColdStart } from './cold-start';
import {
  ToolService, GarmentSummary, GarmentDetail, DateResult, ToolMetrics, BulkUploadResult,
} from '../../services/tool.service';

const EVIDENCE_TYPES = [
  'CareLabel', 'BrandLabel', 'Zip', 'Construction', 'Fabric',
  'PhoneNumber', 'OriginText', 'RegulatoryMark', 'Sizing', 'Other',
];

@Component({
  selector: 'app-seller-tool',
  standalone: true,
  imports: [CommonModule, FormsModule],
  styleUrl: './seller-tool.component.scss',
  templateUrl: './seller-tool.component.html',
})
export class SellerToolComponent implements OnInit {
  private readonly tool = inject(ToolService);
  private readonly platformId = inject(PLATFORM_ID);

  readonly evidenceTypes = EVIDENCE_TYPES;

  /**
   * Capture slots, required ones first. Mirrors the server's standard; the authoritative version
   * (with per-slot resolution floors and guidance) is served from /capture-standard, which this
   * list should be replaced by once the capture UI grows past a single dropdown.
   */
  readonly captureSlots = [
    'CareLabel',
    'FlatLayFront',
    'BrandLabel',
    'FlatLayBack',
    'Zip',
    'ConstructionDetail',
    'Unspecified',
  ];

  readonly loading = signal(true);

  /**
   * Whether the last load actually failed, as distinct from succeeding with nothing.
   *
   * Without this the page rendered "No garments yet" and "Could not load your garments" at the same
   * time, because an empty list is the initial state and a failure never replaces it. It reads as
   * broken, and worse, it tells you the archive is empty at the exact moment you cannot see it.
   */
  readonly loadFailed = signal(false);

  /** True while we are waiting out a suspended tool rather than reporting a failure. */
  readonly waking = signal(false);

  readonly garments = signal<GarmentSummary[]>([]);
  readonly selected = signal<GarmentDetail | null>(null);
  readonly dating = signal<DateResult | null>(null);
  readonly showNew = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');

  /** Usage metrics (admin only). Null while loading, or if the caller isn't an admin. */
  readonly metrics = signal<ToolMetrics | null>(null);

  /** Whether the seller has ruled on the flag currently on screen, so we ask once and only once. */
  readonly flagVerdict = signal<'upheld' | 'dismissed' | null>(null);

  newTitle = '';
  newReference = '';
  evType = 'CareLabel';
  evFeature = '';
  evValue = '';
  capType = 'CareLabel';
  capFeature = '';
  captureFile: File | null = null;
  /** Which shot this is — drives the server's resolution floor for the slot. */
  capSlot = 'CareLabel';
  /**
   * Archive rights for THIS capture. Deliberately not remembered between uploads: the grant is
   * recorded per image so the archive's provenance survives a seller leaving or the terms changing,
   * and a sticky checkbox would quietly turn that into a per-account flag.
   */
  capArchiveRights = false;

  /**
   * Whether a photographed or logged zip is the garment’s own. Required by the server whenever
   * a zip is logged: a replaced zip recorded as original dates the repair, not the garment, and
   * quietly corrupts the corpus it feeds. “Unsure” is always offered — forcing a guess is worse.
   */
  evZipOriginality = '';
  capZipOriginality = '';
  readonly zipOriginalities = ['Original', 'Replaced', 'Unsure'];

  /** Back-catalogue upload: many photos at once, from the camera roll. */
  bulkFiles: File[] = [];
  bulkArchiveRights = false;
  bulkType = 'CareLabel';
  bulkSlot = 'CareLabel';
  bulkFeature = '';
  readonly bulkResult = signal<BulkUploadResult | null>(null);
  claimEarliest?: number;
  claimLatest?: number;

  /**
   * How many times to wait out a cold start, and for how long.
   *
   * Two attempts over about four seconds. Enough for a Fly machine to resume, short enough that a
   * genuinely unreachable tool still says so quickly — a spinner that never resolves is a worse
   * lie than an error.
   */
  private static readonly WakeAttempts = 2;
  private static readonly WakeDelayMs = 1500;

  /**
   * Wait out a suspended tool, once.
   *
   * The tool's machines suspend when idle, so its first request of the day fails and the page
   * says it cannot be reached. Its only user has already been taught once to believe that
   * message, by a CSP fault that produced exactly the same screen for a month — so the cheap fix
   * is to stop showing it for the one cause that resolves itself.
   */
  private wakeRetry<T>(): MonoTypeOperatorFunction<T> {
    return retry<T>({
      count: SellerToolComponent.WakeAttempts,
      delay: (error, attempt) => {
        if (!isColdStart(error)) {
          // Not a cold start: answer now rather than making them wait for the same answer.
          return throwError(() => error);
        }
        this.waking.set(true);
        return timer(SellerToolComponent.WakeDelayMs * attempt);
      },
    });
  }
  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.loadGarments();
      this.loadMetrics();
    }
  }

  private loadMetrics(): void {
    // Admin-only, and this page is reachable by non-admins once the beta opens, so a 403 here is an
    // expected answer rather than a fault: leave the panel off and say nothing.
    this.tool.metrics(28).pipe(this.wakeRetry()).subscribe({
      next: (m) => this.metrics.set(m),
      error: () => this.metrics.set(null),
    });
  }

  private loadGarments(): void {
    this.loading.set(true);
    this.tool.listGarments().pipe(this.wakeRetry()).subscribe({
      next: (list) => {
        this.garments.set(list);
        this.loadFailed.set(false);
        this.waking.set(false);
        this.loading.set(false);
      },
      error: () => {
        // Deliberately does not name a cause. This said "your session may have expired" for a month
        // while the real fault was a CSP that blocked the request before it was sent — a confident
        // wrong diagnosis on screen is worse than an honest vague one, because it sends whoever
        // reads it looking in the wrong place.
        this.error.set('Could not reach the dating tool. Try reloading; if it keeps happening the tool API may be unreachable from here.');
        this.loadFailed.set(true);
        this.waking.set(false);
        this.loading.set(false);
      },
    });
  }

  toggleNew(): void {
    this.showNew.set(!this.showNew());
  }

  select(id: string): void {
    this.dating.set(null);
    this.flagVerdict.set(null);
    this.loadDetail(id);
  }

  private loadDetail(id: string): void {
    this.tool.getGarment(id).subscribe({
      next: (g) => this.selected.set(g),
      error: () => this.error.set('Could not load that garment.'),
    });
  }

  createGarment(): void {
    this.error.set('');
    this.busy.set(true);
    this.tool.createGarment({ title: this.newTitle || undefined, reference: this.newReference || undefined }).subscribe({
      next: (res) => {
        this.newTitle = ''; this.newReference = '';
        this.showNew.set(false);
        this.busy.set(false);
        this.loadGarments();
        this.loadDetail(res.id);
      },
      error: () => { this.error.set('Could not create that garment.'); this.busy.set(false); },
    });
  }

  addEvidence(): void {
    const g = this.selected();
    if (!g || !this.evFeature.trim()) { return; }
    this.error.set('');
    this.busy.set(true);
    this.tool.addEvidence(g.id, {
      type: this.evType,
      feature: this.evFeature.trim(),
      rawValue: this.evValue || undefined,
      origin: 'human',
      zipOriginality: this.needsZipOriginality(this.evType) ? this.evZipOriginality || undefined : undefined,
    }).subscribe({
      next: () => { this.evFeature = ''; this.evValue = ''; this.busy.set(false); this.refresh(g.id); },
      error: () => { this.error.set('Could not add that evidence.'); this.busy.set(false); },
    });
  }

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.captureFile = input.files?.[0] ?? null;
  }

  capture(): void {
    const g = this.selected();
    if (!g || !this.captureFile) { return; }
    if (!this.capArchiveRights) {
      this.error.set('Confirm archive rights before uploading — the grant is recorded against each image.');
      return;
    }
    this.error.set('');
    this.busy.set(true);
    this.tool
      .capture(
        g.id, this.captureFile, this.capType, this.capFeature.trim(), this.capSlot, this.capArchiveRights,
        this.needsZipOriginality(this.capType) ? this.capZipOriginality || undefined : undefined,
      )
      .subscribe({
        next: () => {
          this.captureFile = null;
          this.capFeature = '';
          this.capArchiveRights = false;
          this.busy.set(false);
          this.refresh(g.id);
        },
        error: (err: unknown) => {
          // A rejection here is usually the capture standard doing its job — an undersized or
          // unreadable label — so show the server's reason rather than a generic failure.
          const detail = (err as { error?: { error?: string } })?.error?.error;
          this.error.set(detail ?? 'Upload failed — the tool’s image storage may not be configured yet.');
          this.busy.set(false);
        },
      });
  }

  runDating(): void {
    const g = this.selected();
    if (!g) { return; }
    this.error.set('');
    this.busy.set(true);
    this.tool.runDating(g.id, { earliest: this.claimEarliest, latest: this.claimLatest }).subscribe({
      next: (r) => {
        this.dating.set(r);
        this.flagVerdict.set(null);
        this.busy.set(false);
        this.refresh(g.id);
      },
      error: () => { this.error.set('Could not run the dating engine.'); this.busy.set(false); },
    });
  }

  /**
   * The seller's verdict on a flag: were they actually wrong? This is the other half of the metric
   * the whole thesis rests on — flags raised is only interesting next to how often the flag was
   * right. It is also how a bad rule gets found, which is why dismissing one is a first-class answer
   * and not a nuisance.
   */
  respondToFlag(upheld: boolean): void {
    const g = this.selected();
    if (!g || this.flagVerdict()) { return; }
    this.flagVerdict.set(upheld ? 'upheld' : 'dismissed');
    this.tool.recordEvent({
      kind: upheld ? 'DatingFlagUpheld' : 'DatingFlagDismissed',
      garmentId: g.id,
      detail: this.dating()?.evidence.map((e) => e.specId).filter(Boolean).slice(0, 6).join(',') || undefined,
    }).subscribe({
      // Never surface an instrumentation failure to the seller: their answer is recorded on screen
      // either way, and a metrics outage must not look like their action failing.
      next: () => this.loadMetrics(),
      error: () => undefined,
    });
  }

  /** True when the chosen evidence type is a zip, so the form must ask about originality. */
  needsZipOriginality(type: string): boolean {
    return type === 'Zip';
  }

  onBulkFiles(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.bulkFiles = input.files ? Array.from(input.files) : [];
    this.bulkResult.set(null);
  }

  /**
   * Seed the archive from the camera roll.
   *
   * These are photographs of garments that have already been and gone, so they are stored as
   * historical rather than held to the capture standard — and the per-file results are shown
   * rather than a single success, because a partial import is the normal outcome and the seller
   * needs to see which ones did not make it.
   */
  bulkUpload(): void {
    const g = this.selected();
    if (!g || this.bulkFiles.length === 0) { return; }
    if (!this.bulkArchiveRights) {
      this.error.set('Confirm archive rights before uploading — the grant is recorded against each image.');
      return;
    }
    this.error.set('');
    this.busy.set(true);
    this.tool
      .bulkUpload(
        g.id, this.bulkFiles, this.bulkType, this.bulkFeature.trim(), this.bulkSlot,
        this.bulkArchiveRights,
        this.needsZipOriginality(this.bulkType) ? this.capZipOriginality || undefined : undefined,
      )
      .subscribe({
        next: (result) => {
          this.bulkResult.set(result);
          this.bulkFiles = [];
          this.bulkArchiveRights = false;
          this.busy.set(false);
          this.refresh(g.id);
        },
        error: (err: unknown) => {
          const detail = (err as { error?: { error?: string } })?.error?.error;
          this.error.set(detail ?? 'Bulk upload failed.');
          this.busy.set(false);
        },
      });
  }
  /** A rate as a percentage, or an em dash — never 0%, which would read as a real measurement. */
  percent(rate: number | null | undefined): string {
    return rate == null ? '—' : `${Math.round(rate * 100)}%`;
  }

  duration(seconds: number | null | undefined): string {
    if (seconds == null) { return '—'; }
    if (seconds < 90) { return `${seconds}s`; }
    return `${Math.floor(seconds / 60)}m ${seconds % 60}s`;
  }

  /** Reload the open garment (evidence/estimates) and the list summaries after a mutation. */
  private refresh(id: string): void {
    this.loadDetail(id);
    this.loadGarments();
  }

  rangeLabel(earliest?: number | null, latest?: number | null): string {
    if (earliest != null && latest != null) { return `${earliest}–${latest}`; }
    if (earliest != null) { return `${earliest} onwards`; }
    if (latest != null) { return `up to ${latest}`; }
    return 'No date bounds';
  }
}
