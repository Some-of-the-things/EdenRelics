import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatingChainLink, DatingFeature, DatingPreview, ToolService } from '../../services/tool.service';

/** One row in the bench: a feature the rules can act on, plus the label text where a rule needs it. */
interface Observation {
  feature: string;
  type: string;
  rawValue: string;
  needsValue: boolean;
}

/** A ready-made case, so the engine's behaviour can be seen without knowing the feature codes. */
interface Scenario {
  name: string;
  what: string;
  features: string[];
  values?: Record<string, string>;
  claim?: { earliest: number; latest: number };
}

/**
 * The dating bench: feed the engine observations and see the range it derives, the outcome, and
 * every bound with its citation.
 *
 * It calls /dating/preview rather than the real garment-dating endpoint on purpose — dating a
 * garment writes a proposed estimate, and using that here would fill the evidence archive with
 * throwaway rows. The archive is the asset.
 *
 * The feature picker is loaded from the live rule set rather than hardcoded, so it can never offer
 * a feature no rule matches — which is how a demo starts quietly lying about what the tool does.
 */
@Component({
  selector: 'app-admin-dating',
  imports: [FormsModule],
  templateUrl: './admin-dating.component.html',
  styleUrl: './admin-dating.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminDatingComponent {
  private readonly tool = inject(ToolService);

  readonly features = signal<DatingFeature[]>([]);
  readonly observations = signal<Observation[]>([]);
  readonly result = signal<DatingPreview | null>(null);
  readonly loading = signal(false);
  readonly running = signal(false);
  readonly error = signal('');

  claimEarliest: number | null = null;
  claimLatest: number | null = null;

  /** Features not yet added, for the picker. */
  readonly available = computed(() => {
    const chosen = new Set(this.observations().map((o) => o.feature));
    return this.features().filter((f) => !chosen.has(f.feature));
  });

  readonly hardCount = computed(() => this.observations().length);

  readonly appliedLinks = computed(() => (this.result()?.evidence ?? []).filter((l) => l.applied));
  readonly setAsideLinks = computed(() => (this.result()?.evidence ?? []).filter((l) => !l.applied));

  /**
   * The cases worth seeing first. Feature codes are checked against the live rule set before a
   * scenario is offered, so a scenario can't silently break when the rules are edited.
   */
  private readonly scenarios: Scenario[] = [
    {
      name: 'Cut-label dress',
      what: 'No maker\'s label at all — dated purely on how it is built. This is the case the whole design exists for.',
      features: ['care.tumble-dry-symbol', 'care.numbered-wash-tub'],
    },
    {
      name: 'Seller has the decade wrong',
      what: 'A dryer symbol cannot appear on a 1970s garment. This is the misdating the tool is for.',
      features: ['care.tumble-dry-symbol', 'care.numbered-wash-tub'],
      claim: { earliest: 1970, latest: 1979 },
    },
    {
      name: 'Evidence that contradicts itself',
      what: 'A dryer symbol (not before 1980) with an origin line reading Ceylon (not after 1972). Both are hard bounds and cannot both be true.',
      features: ['care.tumble-dry-symbol', 'origin.text'],
      values: { 'origin.text': 'Made in Ceylon' },
    },
    {
      name: 'Two conventions at once',
      what: 'A numbered tub and an underlined tub together. These genuinely coexisted, so the window widens instead of narrowing to a false precision.',
      features: ['care.numbered-wash-tub', 'care.wash-tub-underline'],
    },
  ];

  readonly runnableScenarios = computed(() => {
    const live = new Set(this.features().map((f) => f.feature));
    return this.scenarios.filter((s) => s.features.every((f) => live.has(f)));
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.tool.datingFeatures().subscribe({
      next: (features) => {
        this.features.set(features);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(
          err.status === 0
            ? 'Could not reach the seller tool API. It runs as its own service — check it is up.'
            : err.status === 403
              ? 'The tool API rejected this token. The dating bench is admin-only.'
              : (err.error?.error ?? 'Could not load the rule set.'),
        );
      },
    });
  }

  addFeature(code: string): void {
    if (!code) {
      return;
    }
    const f = this.features().find((x) => x.feature === code);
    if (!f) {
      return;
    }
    this.observations.update((os) => [
      ...os,
      { feature: f.feature, type: f.type, rawValue: '', needsValue: f.needsValue },
    ]);
    this.result.set(null);
  }

  removeObservation(index: number): void {
    this.observations.update((os) => os.filter((_, i) => i !== index));
    this.result.set(null);
  }

  setRawValue(index: number, value: string): void {
    this.observations.update((os) => os.map((o, i) => (i === index ? { ...o, rawValue: value } : o)));
  }

  clear(): void {
    this.observations.set([]);
    this.result.set(null);
    this.claimEarliest = null;
    this.claimLatest = null;
    this.error.set('');
  }

  loadScenario(s: Scenario): void {
    const live = this.features();
    this.observations.set(
      s.features
        .map((code) => live.find((f) => f.feature === code))
        .filter((f): f is DatingFeature => !!f)
        .map((f) => ({
          feature: f.feature,
          type: f.type,
          rawValue: s.values?.[f.feature] ?? '',
          needsValue: f.needsValue,
        })),
    );
    this.claimEarliest = s.claim?.earliest ?? null;
    this.claimLatest = s.claim?.latest ?? null;
    this.result.set(null);
    this.run();
  }

  run(): void {
    const observations = this.observations();
    this.error.set('');
    if (observations.length === 0) {
      this.error.set('Add at least one observation.');
      return;
    }
    this.running.set(true);
    this.tool
      .datingPreview(
        observations.map((o) => ({
          feature: o.feature,
          type: o.type,
          rawValue: o.rawValue.trim() || null,
        })),
        { earliest: this.claimEarliest, latest: this.claimLatest },
      )
      .subscribe({
        next: (result) => {
          this.result.set(result);
          this.running.set(false);
        },
        error: (err) => {
          this.running.set(false);
          this.error.set(err.error?.error ?? 'The engine could not run on these observations.');
        },
      });
  }

  /** Plain-English gloss on the outcome — the words matter more than the enum name here. */
  outcomeLabel(outcome: DatingPreview['outcome']): string {
    switch (outcome) {
      case 'Estimated':
        return 'Dated';
      case 'HardContradiction':
        return 'Impossible combination';
      case 'SoftContradiction':
        return 'Softer signal disagrees';
    }
  }

  outcomeNote(outcome: DatingPreview['outcome']): string {
    switch (outcome) {
      case 'Estimated':
        return 'The bounds below all hold at once. The range is what survived their intersection.';
      case 'HardContradiction':
        return 'These hard bounds cannot all be true, so there is no honest range — a replaced label, a mixed-up photo, or a fake. The engine surfaces it rather than averaging the bounds into a plausible-looking answer.';
      case 'SoftContradiction':
        return 'The firm evidence agrees, but a softer signal points elsewhere. Worth a look rather than a block.';
    }
  }

  /** True when the range came back inverted, which is how an empty intersection reads. */
  readonly rangeIsEmpty = computed(() => {
    const r = this.result();
    return !!r && r.earliest !== null && r.latest !== null && r.earliest > r.latest;
  });

  trackLink = (_: number, l: DatingChainLink): string => l.ruleId + l.bound;
}
