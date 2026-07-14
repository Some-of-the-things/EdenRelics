import { Component, inject, signal, OnInit, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  ToolService, GarmentSummary, GarmentDetail, DateResult,
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
  template: `
    <section class="tool">
      <header class="tool__head">
        <h1 class="tool__title">Vintage dating tool</h1>
        <p class="tool__lead">
          Build a garment’s evidence set, then let the engine date it from the <em>whole</em> set of
          evidence — never a single label. Machine estimates are <strong>proposed</strong> until you
          confirm them.
        </p>
      </header>

      <div class="tool__grid">
        <!-- Garment list + create -->
        <aside class="tool__panel">
          <div class="tool__row">
            <h2 class="tool__heading">Your garments</h2>
            <button class="btn btn--ghost" type="button" (click)="toggleNew()">
              {{ showNew() ? 'Cancel' : 'New' }}
            </button>
          </div>

          @if (showNew()) {
            <form class="form" (ngSubmit)="createGarment()">
              <label class="field">
                <span class="field__label">Title</span>
                <input class="field__input" name="gt" [(ngModel)]="newTitle" placeholder="e.g. Cut-label wool dress" />
              </label>
              <label class="field">
                <span class="field__label">Your reference (optional)</span>
                <input class="field__input" name="gr" [(ngModel)]="newReference" placeholder="listing id / SKU" />
              </label>
              <button class="btn btn--primary" type="submit" [disabled]="busy()">Create garment</button>
            </form>
          }

          @if (loading()) {
            <p class="tool__muted">Loading…</p>
          } @else if (garments().length === 0) {
            <p class="tool__muted">No garments yet. Create one to start building its evidence set.</p>
          } @else {
            <ul class="glist">
              @for (g of garments(); track g.id) {
                <li>
                  <button class="glist__item" type="button"
                    [class.glist__item--active]="selected()?.id === g.id" (click)="select(g.id)">
                    <span class="glist__name">{{ g.title || 'Untitled garment' }}</span>
                    <span class="glist__meta">
                      {{ g.evidenceCount }} evidence
                      @if (g.latestEarliest != null || g.latestLatest != null) {
                        · {{ rangeLabel(g.latestEarliest, g.latestLatest) }}
                      }
                    </span>
                  </button>
                </li>
              }
            </ul>
          }
        </aside>

        <!-- Detail -->
        <div class="tool__panel tool__detail">
          @if (!selected()) {
            <p class="tool__muted">Select a garment — or create one — to add evidence and run the dating engine.</p>
          } @else {
            <h2 class="tool__heading">{{ selected()!.title || 'Untitled garment' }}</h2>

            <h3 class="tool__sub">Evidence</h3>
            @if (selected()!.evidence.length === 0) {
              <p class="tool__muted">No evidence yet — add a feature below.</p>
            } @else {
              <table class="dtable">
                <thead><tr><th>Type</th><th>Feature</th><th>Value</th><th>Origin</th><th>State</th></tr></thead>
                <tbody>
                  @for (e of selected()!.evidence; track e.id) {
                    <tr>
                      <td>{{ e.type }}</td>
                      <td class="dtable__mono">{{ e.feature }}</td>
                      <td>{{ e.rawValue || '—' }}</td>
                      <td>{{ e.origin }}</td>
                      <td><span class="badge badge--{{ e.confirmation.toLowerCase() }}">{{ e.confirmation }}</span></td>
                    </tr>
                  }
                </tbody>
              </table>
            }

            <form class="form form--inline" (ngSubmit)="addEvidence()">
              <label class="field field--type">
                <span class="field__label">Type</span>
                <select class="field__input" name="et" [(ngModel)]="evType">
                  @for (t of evidenceTypes; track t) { <option [value]="t">{{ t }}</option> }
                </select>
              </label>
              <label class="field field--grow">
                <span class="field__label">Feature code</span>
                <input class="field__input" name="ef" [(ngModel)]="evFeature" placeholder="care.tumble-dry-symbol" />
              </label>
              <label class="field field--grow">
                <span class="field__label">Value (optional)</span>
                <input class="field__input" name="ev" [(ngModel)]="evValue" />
              </label>
              <button class="btn btn--primary" type="submit" [disabled]="!evFeature.trim() || busy()">Add</button>
            </form>

            <details class="tool__capture">
              <summary>Capture a label photo</summary>
              <p class="tool__muted tool__muted--small">
                Uploads the photo to the evidence archive and records it. Requires the tool’s image
                storage (R2) to be configured — otherwise the upload will fail.
              </p>
              <form class="form form--inline" (ngSubmit)="capture()">
                <input class="tool__file" type="file" accept="image/*" (change)="onFile($event)" />
                <label class="field field--type">
                  <span class="field__label">Type</span>
                  <select class="field__input" name="ct" [(ngModel)]="capType">
                    @for (t of evidenceTypes; track t) { <option [value]="t">{{ t }}</option> }
                  </select>
                </label>
                <label class="field field--grow">
                  <span class="field__label">Feature code</span>
                  <input class="field__input" name="cf" [(ngModel)]="capFeature" placeholder="care.wash-symbol" />
                </label>
                <button class="btn btn--primary" type="submit" [disabled]="!captureFile || busy()">Upload</button>
              </form>
            </details>

            <h3 class="tool__sub">Dating</h3>
            <form class="form form--inline" (ngSubmit)="runDating()">
              <label class="field field--type">
                <span class="field__label">Claimed earliest</span>
                <input class="field__input" name="ce" type="number" [(ngModel)]="claimEarliest" placeholder="e.g. 1970" />
              </label>
              <label class="field field--type">
                <span class="field__label">Claimed latest</span>
                <input class="field__input" name="cl" type="number" [(ngModel)]="claimLatest" placeholder="e.g. 1979" />
              </label>
              <button class="btn btn--primary" type="submit" [disabled]="busy()">Run dating engine</button>
            </form>

            @if (dating(); as d) {
              <div class="result">
                <p class="result__range">{{ rangeLabel(d.earliest, d.latest) }}</p>
                <p class="result__outcome">
                  Outcome: <strong>{{ d.outcome }}</strong>
                  <span class="badge badge--proposed">Proposed</span>
                </p>
                @if (d.claimFlag) {
                  <p class="result__flag result__flag--{{ d.claimFlag.strength.toLowerCase() }}">
                    ⚑ {{ d.claimFlag.strength }} conflict with the claimed date — {{ d.claimFlag.message }}
                  </p>
                }
                @if (d.evidence.length) {
                  <h4 class="tool__sub tool__sub--tight">Why</h4>
                  <ul class="chain">
                    @for (c of d.evidence; track $index) {
                      <li>
                        <span class="dtable__mono">{{ c.feature }}</span> → {{ c.bound }}
                        <span class="chain__strength">({{ c.strength }})</span>
                        @if (c.source) { <span class="chain__src">· {{ c.source }}</span> }
                      </li>
                    }
                  </ul>
                }
              </div>
            }
          }
        </div>
      </div>

      @if (error()) { <p class="tool__error">{{ error() }}</p> }
    </section>
  `,
})
export class SellerToolComponent implements OnInit {
  private readonly tool = inject(ToolService);
  private readonly platformId = inject(PLATFORM_ID);

  readonly evidenceTypes = EVIDENCE_TYPES;

  readonly loading = signal(true);
  readonly garments = signal<GarmentSummary[]>([]);
  readonly selected = signal<GarmentDetail | null>(null);
  readonly dating = signal<DateResult | null>(null);
  readonly showNew = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');

  newTitle = '';
  newReference = '';
  evType = 'CareLabel';
  evFeature = '';
  evValue = '';
  capType = 'CareLabel';
  capFeature = '';
  captureFile: File | null = null;
  claimEarliest?: number;
  claimLatest?: number;

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.loadGarments();
    }
  }

  private loadGarments(): void {
    this.loading.set(true);
    this.tool.listGarments().subscribe({
      next: (list) => { this.garments.set(list); this.loading.set(false); },
      error: () => { this.error.set('Could not load your garments. Your session may have expired — try reloading.'); this.loading.set(false); },
    });
  }

  toggleNew(): void {
    this.showNew.set(!this.showNew());
  }

  select(id: string): void {
    this.dating.set(null);
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
      type: this.evType, feature: this.evFeature.trim(), rawValue: this.evValue || undefined, origin: 'human',
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
    this.error.set('');
    this.busy.set(true);
    this.tool.capture(g.id, this.captureFile, this.capType, this.capFeature.trim()).subscribe({
      next: () => { this.captureFile = null; this.capFeature = ''; this.busy.set(false); this.refresh(g.id); },
      error: () => { this.error.set('Upload failed — the tool’s image storage may not be configured yet.'); this.busy.set(false); },
    });
  }

  runDating(): void {
    const g = this.selected();
    if (!g) { return; }
    this.error.set('');
    this.busy.set(true);
    this.tool.runDating(g.id, { earliest: this.claimEarliest, latest: this.claimLatest }).subscribe({
      next: (r) => { this.dating.set(r); this.busy.set(false); this.refresh(g.id); },
      error: () => { this.error.set('Could not run the dating engine.'); this.busy.set(false); },
    });
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
