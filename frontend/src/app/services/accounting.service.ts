import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

export interface MonthlyPnlRow {
  year: number;
  month: number;
  revenue: number;
  expenses: number;
  net: number;
}

export interface ExpenseCategoryTotal {
  category: string;
  amount: number;
}

export interface AccountingTransaction {
  id: string;
  date: string;
  description: string;
  amount: number;
  category: string;
  platform: string | null;
  reference: string | null;
  receiptUrl: string | null;
  notes: string | null;
  createdAtUtc: string;
}

export interface AccountingSnapshot {
  currency: string;
  months: MonthlyPnlRow[];
  totalRevenue: number;
  totalExpenses: number;
  totalNet: number;
  expenseSplit: ExpenseCategoryTotal[];
  recentTransactions: AccountingTransaction[];
  generatedAt: string;
}

@Injectable({ providedIn: 'root' })
export class AccountingService {
  private readonly http = inject(HttpClient);

  snapshot(): Promise<AccountingSnapshot> {
    return firstValueFrom(this.http.get<AccountingSnapshot>(`${environment.apiUrl}/api/finance/pnl`));
  }
}
