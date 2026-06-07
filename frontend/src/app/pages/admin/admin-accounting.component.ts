import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal, OnInit } from '@angular/core';
import {
  AccountingService,
  AccountingSnapshot,
  AccountingTransaction,
  MonthlyPnlRow,
} from '../../services/accounting.service';

const MONTH_LABELS = [
  'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
  'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
];

/**
 * Admin profit-and-loss view. A rolling 13-month P&L (revenue, expenses, net) plus a
 * by-category expense split, computed from the existing Transactions ledger. Income and
 * expense entry lives in the Finance tab; this surface is the reporting view + CSV export.
 */
@Component({
  selector: 'app-admin-accounting',
  imports: [DatePipe],
  templateUrl: './admin-accounting.component.html',
  styleUrl: './admin-accounting.component.scss',
})
export class AdminAccountingComponent implements OnInit {
  private readonly accounting = inject(AccountingService);

  protected readonly snapshot = signal<AccountingSnapshot | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly currency = computed(() => this.snapshot()?.currency ?? 'GBP');

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.snapshot.set(await this.accounting.snapshot());
      this.error.set(null);
    } catch {
      this.error.set('Could not load accounting data.');
    } finally {
      this.loading.set(false);
    }
  }

  protected formatMoney(amount: number): string {
    return new Intl.NumberFormat('en-GB', {
      style: 'currency',
      currency: this.currency(),
      maximumFractionDigits: 2,
    }).format(amount);
  }

  protected monthLabel(m: MonthlyPnlRow): string {
    return `${MONTH_LABELS[(m.month - 1) % 12]} ${m.year}`;
  }

  protected exportCsv(): void {
    const snap = this.snapshot();
    if (!snap) { return; }
    const rows: string[] = ['Year,Month,Revenue,Expenses,Net'];
    for (const m of snap.months) {
      rows.push([m.year, m.month, m.revenue.toFixed(2), m.expenses.toFixed(2), m.net.toFixed(2)].join(','));
    }
    const blob = new Blob([rows.join('\n')], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `eden-relics-accounting-${snap.generatedAt.slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  protected txnAmount(t: AccountingTransaction): string {
    return this.formatMoney(t.amount);
  }
}
