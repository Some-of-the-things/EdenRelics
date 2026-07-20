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
  templateUrl: './seller-tool.component.html',
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
