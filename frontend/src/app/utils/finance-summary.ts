/**
 * Filtering and totalling for the admin Finance ledger.
 *
 * Pulled out of AdminPageComponent because the KPI figures and the table used to
 * disagree: the table filtered by month and source while the headline read the
 * server's unfiltered all-time summary, so narrowing to a month left "Total
 * Income" frozen and "94 Transactions" sitting above a single row. One filter
 * definition, one summariser, both used by both — and testable without booting
 * the admin page.
 */

/** The fields of a ledger row that filtering and totalling actually depend on. */
export interface FinanceLedgerRow {
  /** ISO date, `YYYY-MM-DD`. Month filtering is a prefix match on this. */
  date: string;
  /** Positive is income, negative is an expense. */
  amount: number;
  /** Sales channel; null means unspecified. */
  platform: string | null;
}

export type FinanceSourceFilter = 'all' | 'site' | 'external';

export interface FinanceTotals {
  income: number;
  expenses: number;
  profit: number;
  count: number;
}

/** Pounds and pence: summing floats drifts, so round each figure once at the end. */
function round(n: number): number {
  return Math.round(n * 100) / 100;
}

export function filterFinanceRows<T extends FinanceLedgerRow>(
  rows: readonly T[],
  monthFilter: string,
  sourceFilter: FinanceSourceFilter,
): T[] {
  let result = [...rows];
  if (monthFilter !== 'all') {
    result = result.filter((t) => t.date.startsWith(monthFilter));
  }
  if (sourceFilter === 'site') {
    result = result.filter((t) => t.platform === 'Website');
  } else if (sourceFilter === 'external') {
    // External = a known non-Website platform (Etsy, Depop, Vinted, eBay...).
    // Rows with no platform are Unspecified and belong to neither bucket, so the
    // two never overlap and never double-count.
    result = result.filter((t) => !!t.platform && t.platform !== 'Website');
  }
  return result;
}

export function summariseFinanceRows(rows: readonly FinanceLedgerRow[]): FinanceTotals {
  const income = round(rows.filter((t) => t.amount > 0).reduce((s, t) => s + t.amount, 0));
  const expenses = round(
    rows.filter((t) => t.amount < 0).reduce((s, t) => s + Math.abs(t.amount), 0),
  );
  return { income, expenses, profit: round(income - expenses), count: rows.length };
}

/** Human-readable statement of what a set of totals covers. */
export function financeScopeLabel(monthFilter: string, sourceFilter: FinanceSourceFilter): string {
  const period = monthFilter === 'all' ? 'All time' : monthFilter;
  const source =
    sourceFilter === 'site' ? 'site sales' : sourceFilter === 'external' ? 'external sales' : null;
  return source ? `${period} · ${source}` : period;
}
