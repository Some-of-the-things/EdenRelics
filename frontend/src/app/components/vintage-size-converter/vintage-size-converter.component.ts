import { ChangeDetectionStrategy, Component, signal } from '@angular/core';

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
  readonly result = signal<SizeResult | null>(null);
  readonly error = signal<string | null>(null);

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

    let lengthNote = '';
    if (length >= 50) {
      lengthNote = ` At ${length}" this is a maxi — check it against your height.`;
    } else if (length >= 40) {
      lengthNote = ` At ${length}" this is a midi length.`;
    } else if (length <= 32) {
      lengthNote = ` At ${length}" this is a mini or short dress.`;
    }

    const note =
      `Based on a full chest of ${chest.toFixed(1)}" and full waist of ${fullWaist.toFixed(1)}", ` +
      `this piece corresponds to approximately a modern UK ${sizeByChest}. ` +
      `Allow 1–2" of ease for comfortable wear.${lengthNote}`;

    const warning = sizeByChest !== sizeByWaist
      ? `Chest and waist suggest different sizes (${sizeByChest} by chest, ${sizeByWaist} by waist). ` +
        'This dress has a defined waist — check both measurements against your own before buying.'
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

  private sizeFromChest(c: number): string {
    if (c <= 31) { return '4–6'; }
    if (c <= 33) { return '6–8'; }
    if (c <= 35) { return '8–10'; }
    if (c <= 37) { return '10–12'; }
    if (c <= 39) { return '12–14'; }
    if (c <= 42) { return '14–16'; }
    return '16+';
  }

  private sizeFromWaist(w: number): string {
    if (w <= 23) { return '4–6'; }
    if (w <= 25) { return '6–8'; }
    if (w <= 27) { return '8–10'; }
    if (w <= 29) { return '10–12'; }
    if (w <= 31) { return '12–14'; }
    if (w <= 34) { return '14–16'; }
    return '16+';
  }
}
