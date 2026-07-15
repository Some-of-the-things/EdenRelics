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
    public static void MapToolEndpoints(this WebApplication app)
    {
        // --- Garments + evidence (owner-scoped) ---

        app.MapPost("/garments", async (CreateGarmentRequest req, ClaimsPrincipal user, ToolDbContext db) =>
        {
            Garment garment = new()
            {
                OwnerId = UserId(user),
                Title = req.Title,
                SellerRef = req.SellerRef,
                Reference = req.Reference,
            };
            db.Garments.Add(garment);
            await db.SaveChangesAsync();
            return Results.Created($"/garments/{garment.Id}", new { id = garment.Id });
        }).RequireAuthorization();

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
        }).RequireAuthorization();

        app.MapGet("/garments/{id:guid}", async (Guid id, ClaimsPrincipal user, ToolDbContext db) =>
        {
            Garment? garment = await db.Garments
                .Include(g => g.Evidence)
                .Include(g => g.Estimates)
                .FirstOrDefaultAsync(g => g.Id == id);
            return garment is null || !CanAccess(garment, user) ? Results.NotFound() : Results.Ok(ToDto(garment));
        }).RequireAuthorization();

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
        }).RequireAuthorization();

        // --- Capture pipeline: upload a label/flat-lay photo -> R2 -> evidence record (the archive) ---

        app.MapPost("/garments/{id:guid}/capture", async (Guid id, HttpRequest request, ClaimsPrincipal user, ToolDbContext db, IImageStore images) =>
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

            await using Stream stream = file.OpenReadStream();
            string imageKey = await images.PutAsync(stream, file.ContentType ?? "application/octet-stream", $"garments/{id}");

            EvidenceRecord evidence = new()
            {
                GarmentId = id,
                Type = type,
                Feature = form["feature"].ToString(),
                ImageKey = imageKey,
                Origin = "capture",
                Confirmation = ConfirmationState.Proposed,
            };
            db.EvidenceRecords.Add(evidence);
            await db.SaveChangesAsync();
            return Results.Created($"/garments/{id}", new { id = evidence.Id, imageKey });
        }).RequireAuthorization();

        // --- Dating: run the engine over the garment's evidence, store a proposed estimate ---

        app.MapPost("/garments/{id:guid}/date", async (Guid id, DateGarmentRequest req, ClaimsPrincipal user, ToolDbContext db, IDatingEngine engine) =>
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
            await db.SaveChangesAsync();

            return Results.Ok(new DateResultDto(
                result.Range.Earliest,
                result.Range.Latest,
                result.Outcome.ToString(),
                result.ClaimFlag is null ? null : new ClaimFlagDto(result.ClaimFlag.Strength.ToString(), result.ClaimFlag.Message),
                result.Evidence.Select(e => new EvidenceChainDto(e.RuleId, e.Feature, e.Bound, e.Strength.ToString(), e.Source)).ToList()));
        }).RequireAuthorization();

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
