using System.Security.Cryptography;
using System.Text;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Eden_Relics_BE.Services;

namespace Eden_Relics_BE.Controllers;

/// <summary>
/// Public iCal (RFC 5545) feed of the operator's regulatory obligations. Token-gated via a
/// query string so the URL can be subscribed from Google Calendar / Outlook / iPhone without
/// each client supporting OAuth. The token is the <c>Liabilities:IcalToken</c> config value.
/// With no token configured the endpoint returns 404 — fail-closed.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/calendar")]
public class CalendarFeedController(
    EdenRelicsDbContext context,
    IOptions<LiabilityOptions> options) : ControllerBase
{
    private const string OrganizerEmail = "site@edenrelics.co.uk";
    private readonly LiabilityOptions _options = options.Value;

    /// <summary>Returns the iCal feed if the supplied token matches the configured one. 404 otherwise.</summary>
    [HttpGet("obligations.ics")]
    [EnableRateLimiting("public-write")]
    public async Task<IActionResult> Feed([FromQuery] string? token, CancellationToken ct)
    {
        string? expected = _options.IcalToken;
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(token))
        {
            return NotFound();
        }
        if (!FixedTimeEquals(token, expected))
        {
            return NotFound();
        }

        List<LiabilityObligation> rows = await context.LiabilityObligations
            .OrderBy(o => o.DueDate)
            .ToListAsync(ct);
        string body = BuildVCalendar(rows);
        // UTF-8 *without* BOM — Outlook desktop refuses to import an .ics file that begins
        // with the BOM bytes (it can't match the leading "BEGIN:VCALENDAR" line).
        return Content(body, "text/calendar; charset=utf-8", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        byte[] x = Encoding.UTF8.GetBytes(a);
        byte[] y = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(x, y);
    }

    private static string BuildVCalendar(List<LiabilityObligation> rows)
    {
        StringBuilder sb = new();
        AppendLine(sb, "BEGIN:VCALENDAR");
        AppendLine(sb, "VERSION:2.0");
        AppendLine(sb, "PRODID:-//Eden Relics//Admin Obligations//EN");
        AppendLine(sb, "CALSCALE:GREGORIAN");
        AppendLine(sb, "METHOD:PUBLISH");
        AppendLine(sb, "X-WR-CALNAME:Eden Relics - Regulatory obligations");
        AppendLine(sb, "X-WR-TIMEZONE:Europe/London");

        // VTIMEZONE Europe/London — new Outlook for Windows refuses to enable the import button
        // when a VEVENT references a TZID that isn't defined in the file.
        AppendLine(sb, "BEGIN:VTIMEZONE");
        AppendLine(sb, "TZID:Europe/London");
        AppendLine(sb, "X-LIC-LOCATION:Europe/London");
        AppendLine(sb, "BEGIN:DAYLIGHT");
        AppendLine(sb, "TZOFFSETFROM:+0000");
        AppendLine(sb, "TZOFFSETTO:+0100");
        AppendLine(sb, "TZNAME:BST");
        AppendLine(sb, "DTSTART:19700329T010000");
        AppendLine(sb, "RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=-1SU");
        AppendLine(sb, "END:DAYLIGHT");
        AppendLine(sb, "BEGIN:STANDARD");
        AppendLine(sb, "TZOFFSETFROM:+0100");
        AppendLine(sb, "TZOFFSETTO:+0000");
        AppendLine(sb, "TZNAME:GMT");
        AppendLine(sb, "DTSTART:19701025T020000");
        AppendLine(sb, "RRULE:FREQ=YEARLY;BYMONTH=10;BYDAY=-1SU");
        AppendLine(sb, "END:STANDARD");
        AppendLine(sb, "END:VTIMEZONE");

        TimeZoneInfo london = ResolveLondonTimeZone();
        string nowStamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
        foreach (LiabilityObligation o in rows)
        {
            string createdStamp = DateTime.SpecifyKind(o.CreatedAtUtc, DateTimeKind.Utc).ToString("yyyyMMddTHHmmssZ");
            string modifiedStamp = DateTime.SpecifyKind(o.UpdatedAtUtc, DateTimeKind.Utc).ToString("yyyyMMddTHHmmssZ");

            AppendLine(sb, "BEGIN:VEVENT");
            AppendLine(sb, $"UID:obligation-{o.Id}@edenrelics.co.uk");
            AppendLine(sb, $"DTSTAMP:{nowStamp}");
            AppendLine(sb, $"CREATED:{createdStamp}");
            AppendLine(sb, $"LAST-MODIFIED:{modifiedStamp}");
            AppendLine(sb, $"ORGANIZER;CN=Eden Relics Operator:mailto:{OrganizerEmail}");
            AppendLine(sb, "SEQUENCE:0");
            AppendLine(sb, "TRANSP:OPAQUE");
            AppendLine(sb, "CLASS:PRIVATE");

            if (o.ScheduledFor is { } scheduled)
            {
                DateTime scheduledUtc = DateTime.SpecifyKind(scheduled, DateTimeKind.Utc);
                DateTime londonStart = TimeZoneInfo.ConvertTimeFromUtc(scheduledUtc, london);
                DateTime londonEnd = londonStart.AddHours(1);
                AppendLine(sb, $"DTSTART;TZID=Europe/London:{londonStart:yyyyMMddTHHmmss}");
                AppendLine(sb, $"DTEND;TZID=Europe/London:{londonEnd:yyyyMMddTHHmmss}");
            }
            else
            {
                string due = o.DueDate.ToString("yyyyMMdd");
                string dueNext = o.DueDate.AddDays(1).ToString("yyyyMMdd");
                AppendLine(sb, $"DTSTART;VALUE=DATE:{due}");
                AppendLine(sb, $"DTEND;VALUE=DATE:{dueNext}");
            }

            AppendLine(sb, $"SUMMARY:{EscapeText(SanitiseAscii(BuildSummary(o)))}");
            AppendLine(sb, $"DESCRIPTION:{EscapeText(SanitiseAscii(BuildDescription(o)))}");
            AppendLine(sb, $"STATUS:{BuildStatus(o)}");
            AppendLine(sb, "END:VEVENT");
        }
        AppendLine(sb, "END:VCALENDAR");
        return sb.ToString();
    }

    private static TimeZoneInfo ResolveLondonTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/London"); }
        catch (TimeZoneNotFoundException) { }
        try { return TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time"); }
        catch (TimeZoneNotFoundException) { }
        return TimeZoneInfo.Utc;
    }

    private static string BuildSummary(LiabilityObligation o) => o.Status switch
    {
        LiabilityStatus.Complete => $"[Done] {o.Title}",
        LiabilityStatus.Submitted or LiabilityStatus.Paid => $"[Filed] {o.Title}",
        _ => o.Title,
    };

    private static string BuildDescription(LiabilityObligation o)
    {
        StringBuilder sb = new();
        sb.Append($"Statutory deadline: {o.DueDate:dd MMM yyyy}\n");
        sb.Append($"Status: {o.Status}\n");
        if (o.ScheduledFor is { } when)
        {
            sb.Append($"Scheduled: {when:dd MMM yyyy HH:mm} UTC\n");
        }
        if (!string.IsNullOrWhiteSpace(o.SubmissionReference))
        {
            sb.Append($"Submission ref: {o.SubmissionReference}\n");
        }
        if (o.PaidAmountMinor is { } amt)
        {
            decimal value = amt / 100m;
            sb.Append($"Paid: {value:0.00} {o.Currency.ToUpperInvariant()}\n");
        }
        if (!string.IsNullOrWhiteSpace(o.Notes))
        {
            sb.Append($"Notes: {o.Notes}\n");
        }
        return sb.ToString();
    }

    private static string BuildStatus(LiabilityObligation o) => o.Status switch
    {
        LiabilityStatus.Complete => "CONFIRMED",
        LiabilityStatus.Waived => "CANCELLED",
        _ => "TENTATIVE",
    };

    private static string EscapeText(string input)
    {
        return input
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n");
    }

    private static string SanitiseAscii(string input)
        => input
            .Replace('—', '-')
            .Replace('–', '-')
            .Replace("…", "...")
            .Replace('‘', '\'')
            .Replace('’', '\'')
            .Replace('“', '"')
            .Replace('”', '"');

    /// <summary>Append a line, folding to ≤ 75 octets per RFC 5545 §3.1.</summary>
    private static void AppendLine(StringBuilder sb, string line)
    {
        const int maxOctets = 75;
        byte[] bytes = Encoding.UTF8.GetBytes(line);
        if (bytes.Length <= maxOctets)
        {
            sb.Append(line);
            sb.Append("\r\n");
            return;
        }

        int written = 0;
        bool first = true;
        while (written < bytes.Length)
        {
            int chunkLimit = first ? maxOctets : (maxOctets - 1);
            int take = Math.Min(chunkLimit, bytes.Length - written);
            while (take > 0 && written + take < bytes.Length && (bytes[written + take] & 0xC0) == 0x80)
            {
                take--;
            }
            if (!first)
            {
                sb.Append(' ');
            }
            sb.Append(Encoding.UTF8.GetString(bytes, written, take));
            sb.Append("\r\n");
            written += take;
            first = false;
        }
    }
}
