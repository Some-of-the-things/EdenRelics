import {
  filterFinanceRows,
  financeScopeLabel,
  summariseFinanceRows,
  type FinanceLedgerRow,
} from './finance-summary';

const LEDGER: FinanceLedgerRow[] = [
  { date: '2026-07-02', amount: 120, platform: 'Website' },
  { date: '2026-07-11', amount: 53, platform: 'Etsy' },
  { date: '2026-07-19', amount: -48.31, platform: null },
  { date: '2026-06-04', amount: 240.5, platform: 'Website' },
  { date: '2026-06-21', amount: -60, platform: null },
  { date: '2026-06-28', amount: 84.19, platform: 'Depop' },
];

describe('filterFinanceRows', () => {
  it('returns everything when both filters are off', () => {
    expect(filterFinanceRows(LEDGER, 'all', 'all').length).toBe(6);
  });

  it('filters to a month by date prefix', () => {
    const july = filterFinanceRows(LEDGER, '2026-07', 'all');
    expect(july.length).toBe(3);
    expect(july.every((r) => r.date.startsWith('2026-07'))).toBe(true);
  });

  it('separates site from external without overlap or double-counting', () => {
    const site = filterFinanceRows(LEDGER, 'all', 'site');
    const external = filterFinanceRows(LEDGER, 'all', 'external');
    expect(site.every((r) => r.platform === 'Website')).toBe(true);
    expect(external.every((r) => r.platform && r.platform !== 'Website')).toBe(true);
    // The two unspecified-platform rows belong to neither bucket.
    expect(site.length + external.length).toBe(LEDGER.length - 2);
  });

  it('applies month and source together', () => {
    expect(filterFinanceRows(LEDGER, '2026-07', 'site').length).toBe(1);
  });

  it('does not mutate the input', () => {
    const copy = [...LEDGER];
    filterFinanceRows(LEDGER, '2026-07', 'site');
    expect(LEDGER).toEqual(copy);
  });
});

describe('summariseFinanceRows', () => {
  it('totals income, expenses, profit and count over the rows it is given', () => {
    expect(summariseFinanceRows(LEDGER)).toEqual({
      income: 497.69,
      expenses: 108.31,
      profit: 389.38,
      count: 6,
    });
  });

  /**
   * The whole point of the fix: the headline must describe the same rows the
   * table shows. Before, it described all of them regardless of the filters.
   */
  it('reflects the active filters rather than the whole ledger', () => {
    const july = summariseFinanceRows(filterFinanceRows(LEDGER, '2026-07', 'all'));
    expect(july).toEqual({ income: 173, expenses: 48.31, profit: 124.69, count: 3 });

    const julySite = summariseFinanceRows(filterFinanceRows(LEDGER, '2026-07', 'site'));
    expect(julySite).toEqual({ income: 120, expenses: 0, profit: 120, count: 1 });
  });

  it('rounds to pence rather than exposing float drift', () => {
    const drifty: FinanceLedgerRow[] = [
      { date: '2026-07-01', amount: 0.1, platform: null },
      { date: '2026-07-02', amount: 0.2, platform: null },
    ];
    expect(summariseFinanceRows(drifty).income).toBe(0.3);
  });

  it('handles an empty set without producing NaN', () => {
    expect(summariseFinanceRows([])).toEqual({ income: 0, expenses: 0, profit: 0, count: 0 });
  });
});

describe('financeScopeLabel', () => {
  it('names what the totals cover', () => {
    expect(financeScopeLabel('all', 'all')).toBe('All time');
    expect(financeScopeLabel('2026-07', 'all')).toBe('2026-07');
    expect(financeScopeLabel('all', 'site')).toBe('All time · site sales');
    expect(financeScopeLabel('2026-07', 'external')).toBe('2026-07 · external sales');
  });
});
