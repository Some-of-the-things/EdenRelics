using System.Globalization;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Catalogue of UK statutory obligations with their cadences. The service walks the catalogue
/// once per call and inserts any obligation whose due date falls within the next 12 months and
/// which isn't already on file. Idempotency lives at the (Kind, PeriodEnd) unique index.
/// </summary>
public sealed class LiabilityScheduleService(
    EdenRelicsDbContext db,
    IOptions<LiabilityOptions> options,
    ILogger<LiabilityScheduleService> logger) : ILiabilityScheduleService
{
    private readonly LiabilityOptions _options = options.Value;

    private static readonly TimeSpan LookAhead = TimeSpan.FromDays(370);

    public async Task<int> EnsureUpcomingAsync(DateTime asOf, CancellationToken ct = default)
    {
        DateOnly today = DateOnly.FromDateTime(asOf);
        DateOnly horizon = DateOnly.FromDateTime(asOf.Add(LookAhead));
        int inserted = 0;

        foreach (PlannedObligation planned in EnumeratePlanned(today, horizon))
        {
            if (planned.DueDate < today || planned.DueDate > horizon)
            {
                continue;
            }

            bool exists = await db.LiabilityObligations
                .AnyAsync(o => o.Kind == planned.Kind && o.PeriodEnd == planned.PeriodEnd, ct);
            if (exists)
            {
                continue;
            }

            db.LiabilityObligations.Add(new LiabilityObligation
            {
                Kind = planned.Kind,
                Title = planned.Title,
                PeriodStart = planned.PeriodStart,
                PeriodEnd = planned.PeriodEnd,
                DueDate = planned.DueDate,
                Status = LiabilityStatus.Pending,
                OwedAmountMinor = null,
                Currency = planned.Currency,
            });
            inserted++;
        }

        if (inserted > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("LiabilityScheduleService inserted {Count} new obligations", inserted);
        }
        return inserted;
    }

    private IEnumerable<PlannedObligation> EnumeratePlanned(DateOnly today, DateOnly horizon)
    {
        // VAT quarterly returns + payments (stagger 1 by default).
        foreach ((DateOnly qStart, DateOnly qEnd) in EnumerateVatQuarters(today, horizon))
        {
            DateOnly vatDue = qEnd.AddMonths(1).AddDays(7);
            string label = $"VAT — {qStart:MMM yyyy}–{qEnd:MMM yyyy}";
            yield return new PlannedObligation(LiabilityKind.UkVatReturn, $"UK VAT Return — {label}", qStart, qEnd, vatDue, "gbp");
            yield return new PlannedObligation(LiabilityKind.UkVatPayment, $"UK VAT Payment — {label}", qStart, qEnd, vatDue, "gbp");
        }

        // PAYE monthly submissions (tax month runs 6th to 5th).
        foreach ((DateOnly mStart, DateOnly mEnd) in EnumeratePayeMonths(today, horizon))
        {
            DateOnly payeDue = mEnd.AddDays(17); // 22nd of the following month (electronic payment deadline)
            yield return new PlannedObligation(LiabilityKind.PayeRti,
                $"PAYE RTI — tax month to {mEnd:dd MMM yyyy}", mStart, mEnd, payeDue, "gbp");
        }

        // EU OSS quarterly returns (calendar quarters, end-of-month-after deadline).
        foreach ((DateOnly qStart, DateOnly qEnd) in EnumerateCalendarQuarters(today, horizon))
        {
            DateOnly ossDue = qEnd.AddMonths(1);
            ossDue = new DateOnly(ossDue.Year, ossDue.Month, DateTime.DaysInMonth(ossDue.Year, ossDue.Month));
            yield return new PlannedObligation(LiabilityKind.EuOssReturn,
                $"EU OSS Return — {qStart:MMM yyyy}–{qEnd:MMM yyyy}", qStart, qEnd, ossDue, "eur");
        }

        // Fixed-calendar yearly obligations (UK tax year runs 6 Apr to 5 Apr).
        foreach (int year in YearsFor(today, horizon))
        {
            DateOnly taxYearStart = new(year - 1, 4, 6);
            DateOnly taxYearEnd = new(year, 4, 5);

            // P60 — issued to employees by 31 May for the just-ended tax year.
            yield return new PlannedObligation(LiabilityKind.P60Issuance,
                $"Issue P60s — tax year {taxYearStart:yyyy}/{(taxYearEnd.Year % 100):D2}",
                taxYearStart, taxYearEnd, new DateOnly(year, 5, 31), "gbp");

            // P11D — benefits-in-kind by 6 July.
            yield return new PlannedObligation(LiabilityKind.P11dFiling,
                $"File P11D / P11D(b) — tax year {taxYearStart:yyyy}/{(taxYearEnd.Year % 100):D2}",
                taxYearStart, taxYearEnd, new DateOnly(year, 7, 6), "gbp");

            // Self Assessment — deadline 31 January for the prior tax year.
            yield return new PlannedObligation(LiabilityKind.SelfAssessment,
                $"Self Assessment — tax year {taxYearStart:yyyy}/{(taxYearEnd.Year % 100):D2}",
                taxYearStart, taxYearEnd, new DateOnly(year + 1, 1, 31), "gbp");
        }

        // Year-end-anchored obligations (Corp Tax, Statutory Accounts) — only generate if ARD is set.
        if (TryParseMonthDay(_options.AccountingReferenceDate, out int ardMonth, out int ardDay))
        {
            foreach (int year in YearsFor(today, horizon))
            {
                DateOnly yearEnd = SafeDateOnly(year, ardMonth, ardDay);
                DateOnly yearStart = yearEnd.AddYears(-1).AddDays(1);
                string label = $"y/e {yearEnd:dd MMM yyyy}";

                // Statutory accounts — 9 months after year end.
                yield return new PlannedObligation(LiabilityKind.StatutoryAccounts,
                    $"File statutory accounts — {label}",
                    yearStart, yearEnd, yearEnd.AddMonths(9), "gbp");

                // Corporation Tax payment — 9 months + 1 day after year end.
                yield return new PlannedObligation(LiabilityKind.CorporationTaxPayment,
                    $"Corporation Tax payment — {label}",
                    yearStart, yearEnd, yearEnd.AddMonths(9).AddDays(1), "gbp");

                // CT600 — 12 months after year end.
                yield return new PlannedObligation(LiabilityKind.CorporationTaxReturn,
                    $"File CT600 — {label}",
                    yearStart, yearEnd, yearEnd.AddYears(1), "gbp");
            }
        }

        // Confirmation Statement — anchored to its made-up date (NOT the ARD).
        if (TryParseMonthDay(_options.ConfirmationStatementMadeUpDate, out int csMonth, out int csDay))
        {
            foreach (int year in YearsFor(today, horizon))
            {
                DateOnly madeUp = SafeDateOnly(year, csMonth, csDay);
                yield return new PlannedObligation(LiabilityKind.ConfirmationStatement,
                    $"Confirmation Statement — {madeUp:dd MMM yyyy}",
                    madeUp, madeUp, madeUp.AddDays(14), "gbp");
            }
        }
    }

    private IEnumerable<(DateOnly Start, DateOnly End)> EnumerateVatQuarters(DateOnly today, DateOnly horizon)
    {
        int[] starts = _options.VatStagger switch
        {
            2 => [4, 7, 10, 1],   // Stagger 2: quarters Apr/Jul/Oct/Jan
            3 => [5, 8, 11, 2],   // Stagger 3: quarters May/Aug/Nov/Feb
            _ => [1, 4, 7, 10],   // Stagger 1 (default): Jan/Apr/Jul/Oct
        };
        return EnumerateQuartersStartingMonths(today, horizon, starts);
    }

    private static IEnumerable<(DateOnly Start, DateOnly End)> EnumerateCalendarQuarters(DateOnly today, DateOnly horizon)
        => EnumerateQuartersStartingMonths(today, horizon, [1, 4, 7, 10]);

    private static IEnumerable<(DateOnly Start, DateOnly End)> EnumerateQuartersStartingMonths(DateOnly today, DateOnly horizon, int[] startMonths)
    {
        int firstYear = today.Year - 1;
        int lastYear = horizon.Year + 1;
        for (int year = firstYear; year <= lastYear; year++)
        {
            foreach (int startMonth in startMonths)
            {
                int yr = year;
                int sm = startMonth;
                DateOnly start = new(yr, sm, 1);
                int endMonth = sm + 2;
                int endYear = yr;
                if (endMonth > 12) { endMonth -= 12; endYear++; }
                DateOnly end = new(endYear, endMonth, DateTime.DaysInMonth(endYear, endMonth));
                if (end < today.AddMonths(-3) || start > horizon)
                {
                    continue;
                }
                yield return (start, end);
            }
        }
    }

    private static IEnumerable<(DateOnly Start, DateOnly End)> EnumeratePayeMonths(DateOnly today, DateOnly horizon)
    {
        // Tax month: 6th of month N to 5th of month N+1.
        DateOnly cursor = new DateOnly(today.Year, today.Month, 6).AddMonths(-2);
        DateOnly cap = horizon.AddDays(7);
        while (cursor <= cap)
        {
            DateOnly start = cursor;
            DateOnly end = start.AddMonths(1).AddDays(-1);
            yield return (start, end);
            cursor = cursor.AddMonths(1);
        }
    }

    private static IEnumerable<int> YearsFor(DateOnly today, DateOnly horizon)
    {
        for (int y = today.Year - 1; y <= horizon.Year + 1; y++)
        {
            yield return y;
        }
    }

    private static bool TryParseMonthDay(string? input, out int month, out int day)
    {
        month = 0;
        day = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }
        string[] parts = input.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }
        return int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out month)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out day)
            && month is >= 1 and <= 12
            && day is >= 1 and <= 31;
    }

    private static DateOnly SafeDateOnly(int year, int month, int day)
    {
        int safeDay = Math.Min(day, DateTime.DaysInMonth(year, month));
        return new DateOnly(year, month, safeDay);
    }

    private readonly record struct PlannedObligation(
        LiabilityKind Kind,
        string Title,
        DateOnly PeriodStart,
        DateOnly PeriodEnd,
        DateOnly DueDate,
        string Currency);
}
