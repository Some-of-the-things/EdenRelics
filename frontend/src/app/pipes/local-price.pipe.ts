import { Pipe, PipeTransform, inject } from '@angular/core';
import { LocaleService } from '../services/locale.service';

@Pipe({ name: 'localPrice', pure: false })
export class LocalPricePipe implements PipeTransform {
  private readonly locale = inject(LocaleService);

  transform(gbpAmount: number | null | undefined): string {
    if (gbpAmount == null) {
      return '';
    }
    if (!this.locale.showLocalCurrency()) {
      return '';
    }
    return `(approx. ${this.locale.formatLocal(gbpAmount)})`;
  }
}
