using System.Security.Claims;
using System.Text.Json;
using EdenRelics.SellerTool.Data;
using EdenRelics.SellerTool.Dating;
using Microsoft.EntityFrameworkCore;

namespace EdenRelics.SellerTool.Api;

/// <summary>
/// The tool's HTTP surface: build a garment's evidence set, run the dating engine over it (storing a
/// proposed estimate with its evidence chain), capture label images, and manage the rules store.
/// All endpoints require authentication; garments are scoped to their owner (admins see all); rule
/// management is admin-only.
/// </summary>
public static class ToolEndpoints
{
    /// <summary>
    /// Who may reach the tool at all.
    ///
    /// The seller-facing endpoints are owner-scoped and built for sellers, but during the closed
    /// beta the tool is not for customers — and an ordinary customer account carries a token this
    /// API accepts, so gating the Angular route was never the boundary. The policy is the boundary;
    /// see <c>Tool:AdminOnly</c> in Program.cs, which is the API mirror of adminGuard -> sellerGuard
    /// on the /seller-tool route. Both flip when the beta opens.
    /// </summary>
    public const string AccessPolicy = "ToolAccess";

    public static void MapToolEndpoints(this WebApplication app)
    {
        // --- Garments + evidence (owner-scoped) ---

        app.MapPost("/garments", async (CreateGarmentRequest req, ClaimsPrincipal user, ToolDbContext db, IEventRecorder events) =>
        {
            Garment garment = new()
            {
                OwnerId = UserId(user),
                Title = req.Title,
                SellerRef = req.SellerRef,
                Reference = req.Reference,
            };
            db.Garments.Add(garment);
            events.Record(UserId(user), ToolEventKind.GarmentCreated, garment.Id);
            await db.SaveChangesAsync();
            return Results.Created($"/garments/{garment.Id}", new { id = garment.Id });
        }).RequireAuthorization(AccessPolicy);

        app.MapGet("/garments", async (ClaimsPrincipal user, ToolDbContext db) =>
        {
            // Owner-scoped: a seller sees only their own garments; an admin sees all.
            Guid ownerId = UserId(user);
            bool isAdmin = user.IsInRole("Admin");
            List<Garment> garments = await db.Garments
                .Where(g => isAdmin || g.OwnerId == ownerId)
                .Include(g => g.Evidence)
                .Include(g => g.Estimates)
                .OrderByDescending(g => g.CreatedAtUtc)
                .ToListAsync();
            return Results.Ok(garments.Select(ToSummary).ToList());
        }).RequireAuthorization(AccessPolicy);

        app.MapGet("/garments/{id:guid}", async (Guid id, ClaimsPrincipal user, ToolDbContext db) =>
        {
            Garment? garment = await db.Garments
                .Include(g => g.Evidence)
                .Include(g => g.Estimates)
                .FirstOrDefaultAsync(g => g.Id == id);
            return garment is null || !CanAccess(garment, user) ? Results.NotFound() : Results.Ok(ToDto(garment));
        }).RequireAuthorization(AccessPolicy);

        app.MapPost("/garments/{id:guid}/evidence", async (Guid id, AddEvidenceRequest req, ClaimsPrincipal user, ToolDbContext db) =>
        {
            Garment? garment = await db.Garments.FindAsync(id);
            if (garment is null || !CanAccess(garment, user))
            {
                return Results.NotFound();
            }
            if (!Enum.TryParse(req.Type, ignoreCase: true, out EvidenceType type))
            {
                return Results.BadRequest(new { error = $"Unknown evidence type '{req.Type}'." });
            }
            ConfirmationState confirmation = Enum.TryParse(req.Confirmation, ignoreCase: true, out ConfirmationState c)
                ? c : ConfirmationState.Proposed;

            EvidenceRecord evidence = new()
            {
                GarmentId = id,
                Type = type,
                Feature = req.Feature,
                RawValue = req.RawValue,
                ImageKey = req.ImageKey,
                Origin = string.IsNullOrWhiteSpace(req.Origin) ? "machine" : req.Origin,
                Confirmation = confirmation,
            };
            db.EvidenceRecords.Add(evidence);
            await db.SaveChangesAsync();
            return Results.Created($"/garments/{id}", new { id = evidence.Id });
        }).RequireAuthorization(AccessPolicy);

        // --- Capture pipeline: upload a label/flat-lay photo -> R2 -> evidence record (the archive) ---

        // The capture standard itself, so the client renders slots and guidance from ONE definition
        // rather than duplicating it and drifting.
        app.MapGet("/capture-standard", () => Results.Ok(new
        {
            version = CaptureStandard.Version,
            maxBytes = CaptureStandard.MaxBytes,
            acceptedContentTypes = CaptureStandard.AcceptedContentTypes,
            slots = CaptureStandard.AllSlotsInOrder().Select(s => new
            {
                slot = s.ToString(),
                required = CaptureStandard.RequiredSlots.Contains(s),
                minimumLongEdge = CaptureStandard.MinimumLongEdge(s),
                guidance = CaptureStandard.Guidance(s),
            }),
        })).RequireAuthorization(AccessPolicy);

        app.MapPost("/garments/{id:guid}/capture", async (
            Guid id, HttpRequest request, ClaimsPrincipal user, ToolDbContext db, ICaptureService capture) =>
        {
            Garment? garment = await db.Garments.FindAsync(id);
            if (garment is null || !CanAccess(garment, user))
            {
                return Results.NotFound();
            }
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { error = "Expected a multipart/form-data upload." });
            }

            IFormCollection form = await request.ReadFormAsync();
            IFormFile? file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "No file uploaded." });
            }
            if (!Enum.TryParse(form["type"].ToString(), ignoreCase: true, out EvidenceType type))
            {
                return Results.BadRequest(new { error = $"Unknown evidence type '{form["type"]}'." });
            }
            // Unspecified is a legitimate slot (an ad-hoc extra shot), so an absent or unparseable
            // value falls back to it rather than failing the upload.
            if (!Enum.TryParse(form["slot"].ToString(), ignoreCase: true, out CaptureSlot slot))
            {
                slot = CaptureSlot.Unspecified;
            }
            bool archiveRights = form["archiveRights"].ToString() is "true" or "True" or "1";

            await using Stream stream = file.OpenReadStream();
            CaptureOutcome outcome = await capture.CaptureAsync(
                id, slot, type, form["feature"].ToString(),
                stream, file.ContentType ?? "application/octet-stream", file.Length, archiveRights);

            if (!outcome.Succeeded)
            {
                // A rejection is an expected outcome — a blurry or undersized label is the normal
                // case the standard exists to catch — so it returns a reason the UI can show.
                return Results.BadRequest(new { code = outcome.Rejection!.Code, error = outcome.Rejection.Message });
            }

            EvidenceRecord evidence = outcome.Evidence!;
            return Results.Created($"/garments/{id}", new
            {
                id = evidence.Id,
                imageKey = evidence.ImageKey,
                displayImageKey = evidence.DisplayImageKey,
                slot = evidence.Slot.ToString(),
                width = evidence.Width,
                height = evidence.Height,
            });
        }).RequireAuthorization(AccessPolicy);

        // What is still missing before this garment meets the standard.
        app.MapGet("/garments/{id:guid}/captures/completeness", async (
            Guid id, ClaimsPrincipal user, ToolDbContext db, ICaptureService capture) =>
        {
            Garment? garment = await db.Garments.FindAsync(id);
            if (garment is null || !CanAccess(garment, user))
            {
                return Results.NotFound();
            }
            CaptureCompleteness c = await capture.GetCompletenessAsync(id);
            return Results.Ok(new
            {
                isComplete = c.IsComplete,
                captureCount = c.CaptureCount,
                missingRequired = c.MissingRequired.Select(s => s.ToString()),
                missingRequested = c.MissingRequested.Select(s => s.ToString()),
            });
        }).RequireAuthorization(AccessPolicy);

        // --- Dating: run the engine over the garment's evidence, store a proposed estimate ---

        app.MapPost("/garments/{id:guid}/date", async (Guid id, DateGarmentRequest req, ClaimsPrincipal user, ToolDbContext db, IDatingEngine engine, IEventRecorder events) =>
        {
            Garment? garment = await db.Garments.Include(g => g.Evidence).FirstOrDefaultAsync(g => g.Id == id);
            if (garment is null || !CanAccess(garment, user))
            {
                return Results.NotFound();
            }

            List<Evidence> observed = garment.Evidence.Select(e => new Evidence(e.Feature, e.Type)).ToList();
            DateInterval? claim = req.ClaimEarliest is not null || req.ClaimLatest is not null
                ? new DateInterval(req.ClaimEarliest, req.ClaimLatest)
                : null;

            DatingResult result = engine.Estimate(observed, claim);

            db.DateEstimates.Add(new DateEstimate
            {
                GarmentId = id,
                Earliest = result.Range.Earliest,
                Latest = result.Range.Latest,
                Outcome = result.Outcome.ToString(),
                EvidenceChainJson = JsonSerializer.Serialize(result.Evidence),
                Confirmation = ConfirmationState.Proposed,   // machine-produced — proposed until confirmed
                ComputedAtUtc = DateTime.UtcNow,
            });

            // Recorded here rather than reported by the client, because this is the number that decides
            // whether the verification thesis holds. A client that forgets to report a flag makes the
            // headline metric look better than it is, and the headline metric is the one that must not
            // be flattering. Detail carries the rules that fired, so a rule that flags wrongly can be
            // traced back rather than merely suspected.
            if (result.ClaimFlag is not null)
            {
                events.Record(
                    UserId(user), ToolEventKind.DatingFlagRaised, id,
                    detail: string.Join(',', result.Evidence
                        .Where(e => e.Applied && !string.IsNullOrEmpty(e.SpecId))
                        .Select(e => e.SpecId)
                        .Distinct()
                        .Take(6)));
            }

            await db.SaveChangesAsync();

            return Results.Ok(new DateResultDto(
                result.Range.Earliest,
                result.Range.Latest,
                result.Outcome.ToString(),
                result.ClaimFlag is null ? null : new ClaimFlagDto(result.ClaimFlag.Strength.ToString(), result.ClaimFlag.Message),
                result.Evidence.Select(e => new EvidenceChainDto(e.RuleId, e.Feature, e.Bound, e.Strength.ToString(), e.Source)).ToList()));
        }).RequireAuthorization(AccessPolicy);

        // --- Dating preview (admin only): run the engine on ad-hoc evidence, persist nothing ---
        //
        // The garment endpoint above is the real workflow, but it needs a garment and it writes a
        // proposed estimate. Inspecting or demonstrating the engine through it would mean seeding
        // throwaway garments into the archive, and the archive is the asset — it must not fill up
        // with test rows. This runs the same engine over the same rules and stores nothing.

        app.MapPost("/dating/preview", (DatingPreviewRequest req, IDatingEngine engine) =>
        {
            if (req.Evidence is null || req.Evidence.Count == 0)
            {
                return Results.BadRequest(new { error = "Supply at least one observation." });
            }
            if (req.Evidence.Count > 40)
            {
                return Results.BadRequest(new { error = "Too many observations for one preview." });
            }

            List<Evidence> observed = [];
            foreach (PreviewEvidenceRequest e in req.Evidence)
            {
                if (string.IsNullOrWhiteSpace(e.Feature))
                {
                    return Results.BadRequest(new { error = "Every observation needs a feature code." });
                }
                EvidenceType type = Enum.TryParse(e.Type, ignoreCase: true, out EvidenceType t)
                    ? t
                    : EvidenceType.Other;
                observed.Add(new Evidence(e.Feature.Trim(), type,
                    string.IsNullOrWhiteSpace(e.RawValue) ? null : e.RawValue.Trim()));
            }

            DateInterval? claim = req.ClaimEarliest is not null || req.ClaimLatest is not null
                ? new DateInterval(req.ClaimEarliest, req.ClaimLatest)
                : null;

            DatingResult result = engine.Estimate(observed, claim);

            return Results.Ok(new DatingPreviewDto(
                result.Range.Earliest,
                result.Range.Latest,
                result.Outcome.ToString(),
                result.Range.ToString(),
                result.ClaimFlag is null
                    ? null
                    : new ClaimFlagDto(result.ClaimFlag.Strength.ToString(), result.ClaimFlag.Message),
                result.Evidence.Select(e => new PreviewChainDto(
                    e.RuleId, e.SpecId, e.Feature, e.Bound, e.Strength.ToString(),
                    e.Provenance.ToString(), e.Applied, e.ExclusionReason, e.Source)).ToList()));
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        // The features the LIVE rule set can act on, so a UI offers exactly those and no others.
        app.MapGet("/dating/features", (IRuleStore store) =>
        {
            List<DatingFeatureDto> features = store.VerifiedRules()
                .GroupBy(r => r.Feature, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g =>
                {
                    DatingRule first = g.First();
                    return new DatingFeatureDto(
                        g.Key,
                        first.Type.ToString(),
                        first.Match.ToString(),
                        [.. g.Select(r => r.SpecId).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(s => s, StringComparer.Ordinal)],
                        g.Min(r => r.NotBefore),
                        g.Max(r => r.NotAfter),
                        g.Any(r => r.Strength == BoundStrength.Hard) ? "Hard" : "Soft",
                        // Value-matching rules do nothing without the text they match against.
                        NeedsValue: g.All(r => r.Match != MatchKind.Feature));
                })
                .ToList();
            return Results.Ok(features);
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        // --- Instrumentation (brief §10) ---
        //
        // "Instrument from day one: listings created, time per listing, measurement acceptance rate,
        // extension failure rate per platform. Retrofitting analytics means losing the first months of
        // data." The first months are the beta, and the beta is what the go/no-go gate is judged on, so
        // this ships before there is anything to measure rather than after.

        app.MapPost("/events", async (RecordEventsRequest req, ClaimsPrincipal user, ToolDbContext db, IEventRecorder events) =>
        {
            if (req.Events is null || req.Events.Count == 0)
            {
                return Results.BadRequest(new { error = "Send at least one event." });
            }
            if (req.Events.Count > MaxEventsPerBatch)
            {
                return Results.BadRequest(new { error = $"Send at most {MaxEventsPerBatch} events per request." });
            }

            Guid sellerId = UserId(user);
            foreach (RecordEventRequest e in req.Events)
            {
                if (!Enum.TryParse(e.Kind, ignoreCase: true, out ToolEventKind kind))
                {
                    return Results.BadRequest(new { error = $"Unknown event kind '{e.Kind}'." });
                }
                // Server-owned kinds are recorded by the endpoints that cause them. Accepting them here
                // too would double-count the flag rate — and let a client inflate it deliberately.
                if (ServerOwnedKinds.Contains(kind))
                {
                    return Results.BadRequest(new { error = $"'{kind}' is recorded by the server, not reported by a client." });
                }
                events.Record(sellerId, kind, e.GarmentId, e.Platform, e.DurationMs, e.Detail, e.OccurredAtUtc);
            }

            await db.SaveChangesAsync();
            return Results.Accepted(value: new { recorded = req.Events.Count });
        }).RequireAuthorization(AccessPolicy);

        app.MapGet("/metrics/summary", async (int? days, IToolMetrics metrics) =>
            Results.Ok(await metrics.SummariseAsync(days ?? 28)))
            .RequireAuthorization(p => p.RequireRole("Admin"));

        // --- Rules store (admin only) ---

        app.MapGet("/rules", async (ToolDbContext db) => Results.Ok(await db.StoredRules.ToListAsync()))
            .RequireAuthorization(p => p.RequireRole("Admin"));

        app.MapPost("/rules", async (AddRuleRequest req, ToolDbContext db) =>
        {
            EvidenceType type = Enum.TryParse(req.Type, ignoreCase: true, out EvidenceType t) ? t : EvidenceType.Other;
            BoundStrength strength = Enum.TryParse(req.Strength, ignoreCase: true, out BoundStrength s) ? s : BoundStrength.Hard;
            StoredRule rule = new()
            {
                Id = req.Id,
                Feature = req.Feature,
                Type = type,
                NotBefore = req.NotBefore,
                NotAfter = req.NotAfter,
                Strength = strength,
                TransitionLagMonths = req.TransitionLagMonths,
                SourceCitation = req.SourceCitation,
                Status = RuleStatus.Unverified,   // new rules never affect output until verified
            };
            db.StoredRules.Add(rule);
            await db.SaveChangesAsync();
            return Results.Created($"/rules/{rule.Id}", new { id = rule.Id });
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        app.MapPost("/rules/{id}/verify", async (string id, ToolDbContext db) =>
        {
            StoredRule? rule = await db.StoredRules.FindAsync(id);
            if (rule is null)
            {
                return Results.NotFound();
            }
            rule.Status = RuleStatus.Verified;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = rule.Id, status = rule.Status.ToString() });
        }).RequireAuthorization(p => p.RequireRole("Admin"));
    }

    /// <summary>Enough for an extension that has been offline for a while, without letting one request
    /// write an unbounded number of rows.</summary>
    private const int MaxEventsPerBatch = 100;

    /// <summary>Kinds the API records itself, from the endpoint that causes them.</summary>
    private static readonly HashSet<ToolEventKind> ServerOwnedKinds =
        [ToolEventKind.GarmentCreated, ToolEventKind.DatingFlagRaised];

    private static Guid UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out Guid id)
            ? id : Guid.Empty;

    private static bool CanAccess(Garment garment, ClaimsPrincipal user) =>
        garment.OwnerId == UserId(user) || user.IsInRole("Admin");

    private static GarmentSummaryDto ToSummary(Garment g)
    {
        DateEstimate? latest = g.Estimates.OrderByDescending(e => e.ComputedAtUtc).FirstOrDefault();
        return new GarmentSummaryDto(
            g.Id, g.Title, g.SellerRef, g.Reference, g.CreatedAtUtc,
            g.Evidence.Count,
            latest?.Earliest, latest?.Latest, latest?.Outcome, latest?.Confirmation.ToString());
    }

    private static GarmentDto ToDto(Garment g) => new(
        g.Id, g.Title, g.SellerRef, g.Reference,
        g.Evidence.Select(e => new EvidenceDto(e.Id, e.Type.ToString(), e.Feature, e.RawValue, e.ImageKey, e.Origin, e.Confirmation.ToString())).ToList(),
        g.Estimates.Select(e => new EstimateDto(e.Id, e.Earliest, e.Latest, e.Outcome, e.Confirmation.ToString(), e.ComputedAtUtc)).ToList());
}
