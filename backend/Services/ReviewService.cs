using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Reviews of website orders. Buyers reach the form from the emailed link (a token tied to the
/// order, no login) or from their account. Reviews publish immediately unless the filter holds
/// them (links, contact details, abuse); the admin can reply, and can hide one only with a reason
/// from <see cref="ReviewHideReasons"/>.
/// </summary>
public partial class ReviewService(
    IRepository<Review> reviews,
    IRepository<Order> orders,
    IRepository<Product> products,
    ImageStorageService storage) : IReviewService
{
    public static readonly string[] DispatchedStatuses = ["Shipped", "Delivered"];

    public const int MinCommentLength = 10;
    public const int MaxCommentLength = 2000;
    public const int MaxReplyLength = 2000;
    public const string AnonymousName = "Verified buyer";

    public async Task<List<PublicReviewDto>> GetPublishedAsync(int take)
    {
        int limit = Math.Clamp(take, 1, 100);

        List<Review> published = await reviews.Query()
            .Include(r => r.Order).ThenInclude(o => o.Items)
            .Where(r => r.Status == ReviewStatus.Approved)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(limit)
            .ToListAsync();

        return published.Select(ToPublicDto).ToList();
    }

    public async Task<List<PublicReviewDto>> GetPublishedForProductAsync(Guid productId)
    {
        List<Review> published = await reviews.Query()
            .Include(r => r.Order).ThenInclude(o => o.Items)
            .Where(r => r.Status == ReviewStatus.Approved && r.Order.Items.Any(i => i.ProductId == productId))
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

        return published.Select(ToPublicDto).ToList();
    }

    public async Task<ReviewSummaryDto> GetSummaryAsync()
    {
        List<int> ratings = await reviews.Query()
            .Where(r => r.Status == ReviewStatus.Approved)
            .Select(r => r.Rating)
            .ToListAsync();

        if (ratings.Count == 0)
        {
            return new ReviewSummaryDto(0, 0, 0, 0, 0);
        }

        double avg = Math.Round(ratings.Average(), 2);
        return new ReviewSummaryDto(ratings.Count, avg, avg, avg, avg);
    }

    public async Task<List<EligibleOrderDto>> GetEligibleOrdersAsync(Guid userId)
    {
        // Dispatched orders by this user that don't yet have a review. The subquery over the
        // reviews repository composes because both share the request DbContext.
        List<Order> eligible = await orders.Query()
            .Where(o => o.UserId == userId && DispatchedStatuses.Contains(o.Status))
            .Where(o => !reviews.Query().Any(r => r.OrderId == o.Id))
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync();

        return eligible.Select(o => new EligibleOrderDto(
            o.Id,
            o.CreatedAtUtc,
            o.Total,
            o.Items.Select(i => i.ProductName).ToList())).ToList();
    }

    public async Task<List<MyReviewDto>> GetMineAsync(Guid userId)
    {
        List<Review> mine = await reviews.Query()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

        return mine.Select(ToMyDto).ToList();
    }

    public async Task<ReviewInviteDto?> GetInviteAsync(Guid orderId, string? token, Guid? userId)
    {
        Order? order = await LoadAuthorisedOrderAsync(orderId, token, userId);
        if (order is null || !DispatchedStatuses.Contains(order.Status))
        {
            return null;
        }

        List<Guid> productIds = order.Items.Select(i => i.ProductId).ToList();
        Dictionary<Guid, Product> byId = await products.Query(includeDeleted: true)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        bool reviewed = await reviews.Query(includeDeleted: true).AnyAsync(r => r.OrderId == order.Id);
        return new ReviewInviteDto(
            order.Id,
            order.CreatedAtUtc,
            order.Items.Select(i =>
            {
                byId.TryGetValue(i.ProductId, out Product? p);
                return new ReviewInviteItemDto(i.ProductId, i.ProductName, p?.ImageUrl, p?.Slug);
            }).ToList(),
            order.User?.FirstName,
            order.User?.LastName,
            reviewed);
    }

    public async Task<bool> CanReviewAsync(Guid orderId, string? token, Guid? userId)
    {
        Order? order = await LoadAuthorisedOrderAsync(orderId, token, userId);
        return order is not null
            && DispatchedStatuses.Contains(order.Status)
            && !await reviews.Query(includeDeleted: true).AnyAsync(r => r.OrderId == order.Id);
    }

    public async Task<SubmitReviewResult> SubmitAsync(Guid orderId, string? token, Guid? userId, SubmitOrderReviewDto dto)
    {
        Order? order = await LoadAuthorisedOrderAsync(orderId, token, userId);
        if (order is null)
        {
            return new SubmitReviewResult(SubmitReviewOutcome.OrderNotFound, null);
        }
        if (!DispatchedStatuses.Contains(order.Status))
        {
            return new SubmitReviewResult(SubmitReviewOutcome.OrderNotDispatched, null);
        }
        if (await reviews.Query(includeDeleted: true).AnyAsync(r => r.OrderId == order.Id))
        {
            return new SubmitReviewResult(SubmitReviewOutcome.AlreadyReviewed, null);
        }

        if (dto.Rating is < 1 or > 5)
        {
            return Invalid("Choose a star rating from 1 to 5.");
        }
        string comment = (dto.Comment ?? string.Empty).Trim();
        if (comment.Length < MinCommentLength)
        {
            return Invalid($"Please write at least {MinCommentLength} characters.");
        }
        if (comment.Length > MaxCommentLength)
        {
            return Invalid($"Please keep your review under {MaxCommentLength} characters.");
        }
        string? photo = string.IsNullOrWhiteSpace(dto.PhotoUrl) ? null : dto.PhotoUrl.Trim();
        if (photo is not null && !IsOurReviewPhoto(photo))
        {
            return Invalid("That photo couldn't be attached. Please upload it again.");
        }

        string first = CleanName(dto.FirstName) ?? CleanName(order.User?.FirstName) ?? string.Empty;
        string last = CleanName(dto.LastName) ?? CleanName(order.User?.LastName) ?? string.Empty;
        string displayName = BuildDisplayName(first, last, dto.NameStyle);

        string? hold = HoldReasonFor(comment);
        Review review = new()
        {
            OrderId = order.Id,
            UserId = order.UserId,
            Rating = dto.Rating,
            TransactionRating = dto.Rating,
            DeliveryRating = dto.Rating,
            ProductRating = dto.Rating,
            Comment = comment,
            AuthorDisplayName = displayName,
            PhotoUrl = photo,
            Status = hold is null ? ReviewStatus.Approved : ReviewStatus.Pending,
            HoldReason = hold,
        };

        await reviews.AddAsync(review);
        return new SubmitReviewResult(SubmitReviewOutcome.Success, ToMyDto(review));
    }

    public async Task<List<AdminReviewDto>> GetForAdminAsync(string? status)
    {
        IQueryable<Review> query = reviews.Query()
            .Include(r => r.Order).ThenInclude(o => o.Items)
            .Include(r => r.User);

        if (Enum.TryParse(status, true, out ReviewStatus parsed))
        {
            query = query.Where(r => r.Status == parsed);
        }

        List<Review> list = await query.OrderByDescending(r => r.CreatedAtUtc).ToListAsync();
        return list.Select(ToAdminDto).ToList();
    }

    public async Task<AdminReviewDto?> PublishAsync(Guid id, Guid moderatorId)
    {
        Review? review = await LoadForAdminAsync(id);
        if (review is null)
        {
            return null;
        }
        review.Status = ReviewStatus.Approved;
        review.HiddenReason = null;
        review.ModeratedAtUtc = DateTime.UtcNow;
        review.ModeratedByUserId = moderatorId;
        await reviews.UpdateAsync(review);
        return ToAdminDto(review);
    }

    public async Task<AdminReviewDto?> HideAsync(Guid id, Guid moderatorId, string reason, string? note)
    {
        if (!ReviewHideReasons.All.Contains(reason))
        {
            throw new ArgumentException("Choose one of the reasons for hiding a review.", nameof(reason));
        }
        Review? review = await LoadForAdminAsync(id);
        if (review is null)
        {
            return null;
        }
        review.Status = ReviewStatus.Rejected;
        review.HiddenReason = reason;
        review.ModerationNote = string.IsNullOrWhiteSpace(note) ? null : Limit(note.Trim(), 500);
        review.ModeratedAtUtc = DateTime.UtcNow;
        review.ModeratedByUserId = moderatorId;
        await reviews.UpdateAsync(review);
        return ToAdminDto(review);
    }

    public async Task<AdminReviewDto?> ReplyAsync(Guid id, string? reply)
    {
        Review? review = await LoadForAdminAsync(id);
        if (review is null)
        {
            return null;
        }
        string text = (reply ?? string.Empty).Trim();
        if (text.Length > MaxReplyLength)
        {
            throw new ArgumentException($"Replies can be at most {MaxReplyLength} characters.", nameof(reply));
        }
        review.Reply = text.Length == 0 ? null : text;
        review.RepliedAtUtc = text.Length == 0 ? null : DateTime.UtcNow;
        await reviews.UpdateAsync(review);
        return ToAdminDto(review);
    }

    /// <summary>The order, if the token matches it or the signed-in user placed it.</summary>
    private async Task<Order?> LoadAuthorisedOrderAsync(Guid orderId, string? token, Guid? userId)
    {
        Order? order = await orders.Query()
            .Include(o => o.Items)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null)
        {
            return null;
        }
        if (userId is not null && order.UserId == userId)
        {
            return order;
        }
        return TokenMatches(order.ReviewToken, token) ? order : null;
    }

    private static bool TokenMatches(string? expected, string? given)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(given))
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given));
    }

    private bool IsOurReviewPhoto(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }
        if (!uri.AbsolutePath.Contains("/reviews/", StringComparison.Ordinal))
        {
            return false;
        }
        return !storage.IsConfigured || url.StartsWith(storage.PublicUrl + "/", StringComparison.Ordinal);
    }

    private async Task<Review?> LoadForAdminAsync(Guid id)
    {
        return await reviews.Query()
            .Include(r => r.Order).ThenInclude(o => o.Items)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    private static SubmitReviewResult Invalid(string error) => new(SubmitReviewOutcome.Invalid, null, error);

    /// <summary>
    /// Why a review should wait for a check before going live, or null to publish it now. Only
    /// spam and personal-data signals and plain abuse: never the rating or a negative opinion.
    /// </summary>
    public static string? HoldReasonFor(string comment)
    {
        if (EmailRegex().IsMatch(comment))
        {
            return "Contains an email address";
        }
        if (LinkRegex().IsMatch(comment))
        {
            return "Contains a link";
        }
        // A phone number is a run of at least 10 digits (spaces, dashes and brackets allowed), so
        // "1970 - 1980" or a price doesn't count.
        if (PhoneRegex().Matches(comment).Any(m => m.Value.Count(char.IsDigit) >= 10))
        {
            return "Contains a phone number";
        }
        if (AbuseRegex().IsMatch(comment))
        {
            return "Possible abusive language";
        }
        return null;
    }

    public static string BuildDisplayName(string first, string last, string? style)
    {
        if (style == "anonymous" || string.IsNullOrWhiteSpace(first))
        {
            return AnonymousName;
        }
        if (style == "first" || string.IsNullOrWhiteSpace(last))
        {
            return first;
        }
        return $"{first} {char.ToUpperInvariant(last[0])}.";
    }

    private static string? CleanName(string? name)
    {
        string n = WhitespaceRegex().Replace(name ?? string.Empty, " ").Trim();
        if (n.Length == 0)
        {
            return null;
        }
        return Limit(n, 40);
    }

    private static string Limit(string value, int max) => value.Length <= max ? value : value[..max];

    [GeneratedRegex(@"https?://|www\.|\b[a-z0-9-]+\.(com|co\.uk|uk|net|org|io|shop|store|xyz|ru|info|biz)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(\+?\d[\d\s().-]{8,}\d)")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\b(cunts?|fuck\w*|nigg\w*|fagg?ots?|retards?|whores?|sluts?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AbuseRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private static List<string> NamesOf(Review r) => r.Order?.Items.Select(i => i.ProductName).ToList() ?? [];

    private static PublicReviewDto ToPublicDto(Review r) => new(
        r.Id,
        r.AuthorDisplayName,
        r.Rating,
        r.TransactionRating,
        r.DeliveryRating,
        r.ProductRating,
        r.Comment,
        r.PhotoUrl,
        r.Reply,
        r.RepliedAtUtc,
        NamesOf(r),
        r.CreatedAtUtc);

    private static MyReviewDto ToMyDto(Review r) => new(
        r.Id,
        r.OrderId,
        r.Rating,
        r.TransactionRating,
        r.DeliveryRating,
        r.ProductRating,
        r.Comment,
        r.Status.ToString(),
        r.CreatedAtUtc,
        r.ModerationNote);

    private static AdminReviewDto ToAdminDto(Review r) => new(
        r.Id,
        r.OrderId,
        r.UserId,
        r.User?.Email ?? r.Order?.GuestEmail ?? string.Empty,
        r.AuthorDisplayName,
        r.Rating,
        r.TransactionRating,
        r.DeliveryRating,
        r.ProductRating,
        r.Comment,
        r.PhotoUrl,
        r.Status.ToString(),
        r.HoldReason,
        r.HiddenReason,
        r.Reply,
        r.RepliedAtUtc,
        r.CreatedAtUtc,
        r.ModeratedAtUtc,
        r.ModerationNote,
        r.Order?.Total ?? 0,
        NamesOf(r));
}
