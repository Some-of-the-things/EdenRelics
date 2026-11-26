import {
  ChangeDetectionStrategy,
  Component,
  PLATFORM_ID,
  inject,
  signal,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

/** Size bands smallest first, for comparing the chest and waist sizes. */
const SIZE_ORDER = ['4–6', '6–8', '8–10', '10–12', '12–14', '14–16', '16–18', '18–20', '20+'];

interface SizeResult {
  ukSize: string;
  chest: number;
  fullWaist: number;
  length: number;
  note: string;
  warning: string | null;
}

/**
 * Reusable vintage-size converter. Takes the flat garment measurements from an
 * Eden Relics listing (pit-to-pit, waist, length in inches) and estimates the
 * approximate modern UK size, doubling the flat measurements to full circumference.
 * Self-contained and embeddable anywhere (e.g. the sizing-guide blog post).
 */
@Component({
  selector: 'app-vintage-size-converter',
  templateUrl: './vintage-size-converter.component.html',
  styleUrl: './vintage-size-converter.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VintageSizeConverterComponent {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly result = signal<SizeResult | null>(null);
  readonly error = signal<string | null>(null);
  /** Transient "Copied!" confirmation after the link is copied to the clipboard. */
  readonly linkCopied = signal(false);

  /**
   * Copies the URL of the page hosting this tool so a reader can paste it into
   * their own blog/forum post — a low-friction nudge toward the inbound links
   * that build the site's authority. Best-effort: silently no-ops if the
   * Clipboard API is unavailable (older browsers, insecure contexts).
   */
  copyLink(): void {
    if (!this.isBrowser || !navigator.clipboard) {
      return;
    }
    navigator.clipboard
      .writeText(window.location.href)
      .then(() => {
        this.linkCopied.set(true);
        setTimeout(() => this.linkCopied.set(false), 2500);
      })
      .catch(() => {
        // Clipboard write refused (permissions/insecure context) — no-op.
      });
  }

  calculate(ptpStr: string, waistStr: string, lengthStr: string): void {
    const ptp = parseFloat(ptpStr);
    const waist = parseFloat(waistStr);
    const length = parseFloat(lengthStr);

    if (isNaN(ptp) || isNaN(waist) || isNaN(length)) {
      this.error.set('Please enter all three measurements.');
      this.result.set(null);
      return;
    }
    this.error.set(null);

    const chest = ptp * 2;
    const fullWaist = waist * 2;
    const sizeByChest = this.sizeFromChest(chest);
    const sizeByWaist = this.sizeFromWaist(fullWaist);

    let lengthNote: string;
    if (length >= 50) {
      lengthNote = ` At ${length}" this is ankle to floor length on most people. Check it against your height.`;
    } else if (length >= 41) {
      lengthNote = ` At ${length}" this is a midi, roughly mid-calf on someone 5'4".`;
    } else if (length >= 33) {
      lengthNote = ` At ${length}" this is around knee length.`;
    } else {
      lengthNote = ` At ${length}" this is a mini or short dress.`;
    }

    const note =
      `Based on a full chest of ${chest.toFixed(1)}" and full waist of ${fullWaist.toFixed(1)}", ` +
      `this piece corresponds to approximately a modern UK ${sizeByChest}. ` +
      `These are garment measurements, so the range already allows for fit: a close fit on the larger size, ` +
      `with a little room on the smaller one.${lengthNote}`;

    // Only a waist smaller than the chest size is a problem. A bigger one is normal for shifts,
    // blousons and elastic waists.
    const warning = SIZE_ORDER.indexOf(sizeByWaist) < SIZE_ORDER.indexOf(sizeByChest)
      ? `The waist (${sizeByWaist}) is smaller than the chest (${sizeByChest}). If the waist is fixed, ` +
        "check it against your own waist before buying. If it's elasticated or ties, it will have more give."
      : null;

    this.result.set({
      ukSize: `UK ${sizeByChest}`,
      chest,
      fullWaist,
      length,
      note,
      warning,
    });
  }

  /**
   * Full chest (pit to pit × 2) read against the Marks & Spencer body chart (bust: UK6 30.75,
   * UK8 32.25, UK10 34.25, UK12 36.25, UK14 38, UK16 40.25, UK18 42.5, UK20 45), giving that size
   * and the one below, as listings are sized.
   */
  sizeFromChest(c: number): string {
    if (c <= 31.5) { return '4–6'; }
    if (c <= 33.5) { return '6–8'; }
    if (c <= 35.5) { return '8–10'; }
    if (c <= 37.5) { return '10–12'; }
    if (c <= 39.5) { return '12–14'; }
    if (c <= 41.5) { return '14–16'; }
    if (c <= 44.5) { return '16–18'; }
    if (c <= 46) { return '18–20'; }
    return '20+';
  }

  /** Full waist (flat waist × 2) against the M&S waist chart. */
  sizeFromWaist(w: number): string {
    if (w <= 24.75) { return '4–6'; }
    if (w <= 26.5) { return '6–8'; }
    if (w <= 28.5) { return '8–10'; }
    if (w <= 30.5) { return '10–12'; }
    if (w <= 32.75) { return '12–14'; }
    if (w <= 35) { return '14–16'; }
    if (w <= 37.25) { return '16–18'; }
    if (w <= 39.5) { return '18–20'; }
    return '20+';
  }
}
