using Eden_Relics_BE.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Eden_Relics_BE.Data;

public class EdenRelicsDbContext : DbContext
{
    public EdenRelicsDbContext(DbContextOptions<EdenRelicsDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
    public DbSet<TrackedKeyword> TrackedKeywords => Set<TrackedKeyword>();
    public DbSet<SiteBranding> SiteBranding => Set<SiteBranding>();
    public DbSet<SiteContent> SiteContent => Set<SiteContent>();
    public DbSet<ProductListing> ProductListings => Set<ProductListing>();
    public DbSet<MailingListSubscriber> MailingListSubscribers => Set<MailingListSubscriber>();
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<Favourite> Favourites => Set<Favourite>();
    public DbSet<ProductView> ProductViews => Set<ProductView>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<MonzoTransaction> MonzoTransactions => Set<MonzoTransaction>();
    public DbSet<MonzoToken> MonzoTokens => Set<MonzoToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global query filter for soft deletes on all BaseEntity types
        modelBuilder.Entity<Product>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Order>().HasQueryFilter(e => !e.IsDeleted);

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Email).HasMaxLength(256);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.FirstName).HasMaxLength(100);
            entity.Property(u => u.LastName).HasMaxLength(100);
            entity.Property(u => u.Role).HasMaxLength(20);
            entity.Property(u => u.PasswordHash).HasMaxLength(500);
            entity.Property(u => u.ExternalProvider).HasMaxLength(20);
            entity.Property(u => u.ExternalProviderId).HasMaxLength(256);
            entity.Property(u => u.MfaSecret).HasMaxLength(256);
            entity.Property(u => u.EmailVerificationToken).HasMaxLength(64);

            entity.Property(u => u.DeliveryAddressLine1).HasMaxLength(200);
            entity.Property(u => u.DeliveryAddressLine2).HasMaxLength(200);
            entity.Property(u => u.DeliveryCity).HasMaxLength(100);
            entity.Property(u => u.DeliveryCounty).HasMaxLength(100);
            entity.Property(u => u.DeliveryPostcode).HasMaxLength(20);
            entity.Property(u => u.DeliveryCountry).HasMaxLength(100);

            entity.Property(u => u.BillingAddressLine1).HasMaxLength(200);
            entity.Property(u => u.BillingAddressLine2).HasMaxLength(200);
            entity.Property(u => u.BillingCity).HasMaxLength(100);
            entity.Property(u => u.BillingCounty).HasMaxLength(100);
            entity.Property(u => u.BillingPostcode).HasMaxLength(20);
            entity.Property(u => u.BillingCountry).HasMaxLength(100);

            entity.Property(u => u.PaymentCardholderName).HasMaxLength(200);
            entity.Property(u => u.PaymentCardLast4).HasMaxLength(4);
            entity.Property(u => u.PaymentCardBrand).HasMaxLength(20);
        });

        modelBuilder.Entity<UserCredential>(entity =>
        {
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId);
            entity.Property(c => c.CredentialId).HasMaxLength(1024);
            entity.Property(c => c.PublicKey).HasMaxLength(2048);
            entity.Property(c => c.UserHandle).HasMaxLength(128);
            entity.Property(c => c.CredType).HasMaxLength(32);
            entity.Property(c => c.Nickname).HasMaxLength(100);
            entity.HasIndex(c => c.CredentialId);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(o => o.Status).HasMaxLength(20);
            entity.Property(o => o.Total).HasPrecision(10, 2);
            entity.Property(o => o.GuestEmail).HasMaxLength(256);
            entity.Property(o => o.StripeSessionId).HasMaxLength(256);
            entity.HasOne(o => o.User).WithMany().HasForeignKey(o => o.UserId).IsRequired(false);
            entity.HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId);
            entity.Property(o => o.ShippingMethod).HasMaxLength(20);
            entity.Property(o => o.ShippingCost).HasPrecision(10, 2);
            entity.Property(o => o.ShipAddressLine1).HasMaxLength(200);
            entity.Property(o => o.ShipAddressLine2).HasMaxLength(200);
            entity.Property(o => o.ShipCity).HasMaxLength(100);
            entity.Property(o => o.ShipCounty).HasMaxLength(100);
            entity.Property(o => o.ShipPostcode).HasMaxLength(20);
            entity.Property(o => o.ShipCountry).HasMaxLength(100);
            entity.Property(o => o.BillAddressLine1).HasMaxLength(200);
            entity.Property(o => o.BillAddressLine2).HasMaxLength(200);
            entity.Property(o => o.BillCity).HasMaxLength(100);
            entity.Property(o => o.BillCounty).HasMaxLength(100);
            entity.Property(o => o.BillPostcode).HasMaxLength(20);
            entity.Property(o => o.BillCountry).HasMaxLength(100);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(i => i.ProductName).HasMaxLength(200);
            entity.Property(i => i.UnitPrice).HasPrecision(10, 2);
        });

        modelBuilder.Entity<SiteContent>(entity =>
        {
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.Property(c => c.Key).HasMaxLength(100);
            entity.Property(c => c.Value).HasMaxLength(10000);
            entity.HasIndex(c => c.Key).IsUnique();
        });

        modelBuilder.Entity<TrackedKeyword>(entity =>
        {
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.Property(k => k.Keyword).HasMaxLength(200);
            entity.Property(k => k.PageUrl).HasMaxLength(500);
            entity.Property(k => k.Notes).HasMaxLength(500);
        });

        modelBuilder.Entity<SiteBranding>(entity =>
        {
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.Property(b => b.LogoUrl).HasMaxLength(500);
            entity.Property(b => b.BgPrimary).HasMaxLength(20);
            entity.Property(b => b.BgSecondary).HasMaxLength(20);
            entity.Property(b => b.BgCard).HasMaxLength(20);
            entity.Property(b => b.BgDark).HasMaxLength(20);
            entity.Property(b => b.TextPrimary).HasMaxLength(20);
            entity.Property(b => b.TextSecondary).HasMaxLength(20);
            entity.Property(b => b.TextMuted).HasMaxLength(20);
            entity.Property(b => b.TextInverse).HasMaxLength(20);
            entity.Property(b => b.Accent).HasMaxLength(20);
            entity.Property(b => b.AccentHover).HasMaxLength(20);
            entity.Property(b => b.FontDisplay).HasMaxLength(100);
            entity.Property(b => b.FontBody).HasMaxLength(100);
        });

        modelBuilder.Entity<BlogPost>(entity =>
        {
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.Property(b => b.Title).HasMaxLength(300);
            entity.Property(b => b.Slug).HasMaxLength(300);
            entity.HasIndex(b => b.Slug).IsUnique();
            entity.Property(b => b.Excerpt).HasMaxLength(500);
            entity.Property(b => b.FeaturedImageUrl).HasMaxLength(500);
            entity.Property(b => b.Author).HasMaxLength(100);
        });

        modelBuilder.Entity<MailingListSubscriber>(entity =>
        {
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.Property(m => m.Email).HasMaxLength(256);
            entity.HasIndex(m => m.Email).IsUnique();
            entity.Property(m => m.FirstName).HasMaxLength(100);
            entity.Property(m => m.Source).HasMaxLength(30);
        });

        modelBuilder.Entity<ProductListing>(entity =>
        {
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.Property(l => l.Platform).HasMaxLength(20);
            entity.Property(l => l.ExternalListingId).HasMaxLength(200);
            entity.Property(l => l.ExternalUrl).HasMaxLength(500);
            entity.Property(l => l.Status).HasMaxLength(20);
            entity.HasOne(l => l.Product).WithMany(p => p.Listings).HasForeignKey(l => l.ProductId);
        });

        modelBuilder.Entity<Favourite>(entity =>
        {
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.HasOne(f => f.User).WithMany().HasForeignKey(f => f.UserId);
            entity.HasOne(f => f.Product).WithMany().HasForeignKey(f => f.ProductId);
            entity.HasIndex(f => new { f.UserId, f.ProductId }).IsUnique();
        });

        modelBuilder.Entity<ProductView>(entity =>
        {
            entity.HasOne(v => v.Product).WithMany().HasForeignKey(v => v.ProductId);
            entity.Property(v => v.IpAddress).HasMaxLength(45);
            entity.Property(v => v.ReferrerUrl).HasMaxLength(2000);
            entity.Property(v => v.UtmSource).HasMaxLength(200);
            entity.Property(v => v.UtmMedium).HasMaxLength(200);
            entity.Property(v => v.UtmCampaign).HasMaxLength(200);
            entity.Property(v => v.Channel).HasMaxLength(20);
            entity.Property(v => v.Country).HasMaxLength(100);
            entity.Property(v => v.UserAgent).HasMaxLength(500);
            entity.Property(v => v.DeviceType).HasMaxLength(20);
            entity.Property(v => v.OperatingSystem).HasMaxLength(50);
            entity.Property(v => v.ScreenResolution).HasMaxLength(20);
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.Property(t => t.Description).HasMaxLength(300);
            entity.Property(t => t.Amount).HasPrecision(10, 2);
            entity.Property(t => t.Category).HasMaxLength(30);
            entity.Property(t => t.Platform).HasMaxLength(30);
            entity.Property(t => t.Reference).HasMaxLength(100);
            entity.Property(t => t.ReceiptUrl).HasMaxLength(500);
            entity.Property(t => t.Notes).HasMaxLength(500);
        });

        modelBuilder.Entity<MonzoToken>(entity =>
        {
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.Property(t => t.AccessToken).HasMaxLength(500);
            entity.Property(t => t.RefreshToken).HasMaxLength(500);
            entity.Property(t => t.AccountId).HasMaxLength(100);
        });

        modelBuilder.Entity<MonzoTransaction>(entity =>
        {
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.Property(t => t.MonzoId).HasMaxLength(100);
            entity.HasIndex(t => t.MonzoId).IsUnique();
            entity.Property(t => t.Description).HasMaxLength(500);
            entity.Property(t => t.Amount).HasPrecision(10, 2);
            entity.Property(t => t.Currency).HasMaxLength(10);
            entity.Property(t => t.Category).HasMaxLength(50);
            entity.Property(t => t.MerchantName).HasMaxLength(200);
            entity.Property(t => t.MerchantLogo).HasMaxLength(500);
            entity.Property(t => t.Notes).HasMaxLength(1000);
            entity.Property(t => t.Tags).HasMaxLength(500);
            entity.Property(t => t.DeclineReason).HasMaxLength(100);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(p => p.Price).HasPrecision(10, 2);
            entity.Property(p => p.SalePrice).HasPrecision(10, 2);
            entity.Property(p => p.CostPrice).HasPrecision(10, 2);
            entity.Property(p => p.Supplier).HasMaxLength(200);
            entity.Property(p => p.Name).HasMaxLength(200);
            entity.Property(p => p.Era).HasMaxLength(50);
            entity.Property(p => p.Category).HasMaxLength(20);
            entity.Property(p => p.Size).HasMaxLength(20);
            entity.Property(p => p.Condition).HasMaxLength(20);
            entity.Property(p => p.ImageUrl).HasMaxLength(500);
            entity.Property(p => p.AdditionalImageUrls).HasColumnType("jsonb");

            DateTime seededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            entity.HasData(
                new Product
                {
                    Id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001"),
                    Name = "Bohemian Maxi Dress",
                    Description = "Flowing 1970s bohemian maxi dress with earthy floral print. Empire waist and angel sleeves in lightweight cotton gauze.",
                    Price = 195m,
                    Era = "1970s",
                    Category = "70s",
                    Size = "10",
                    Condition = "good",
                    ImageUrl = "https://placehold.co/400x500/FF6347/FFF?text=Boho+Maxi+Dress",
                    InStock = true,
                    CreatedAtUtc = seededAt,
                    UpdatedAtUtc = seededAt
                },
                new Product
                {
                    Id = Guid.Parse("a1b2c3d4-0002-0000-0000-000000000002"),
                    Name = "Wrap Dress",
                    Description = "Iconic 1970s wrap dress in a bold geometric print. Flattering silhouette with tie waist and flutter sleeves.",
                    Price = 275m,
                    Era = "1970s",
                    Category = "70s",
                    Size = "12",
                    Condition = "excellent",
                    ImageUrl = "https://placehold.co/400x500/556B2F/FFF?text=Wrap+Dress",
                    InStock = true,
                    CreatedAtUtc = seededAt,
                    UpdatedAtUtc = seededAt
                },
                new Product
                {
                    Id = Guid.Parse("a1b2c3d4-0003-0000-0000-000000000003"),
                    Name = "Power Shoulder Dress",
                    Description = "Bold 1980s power dress in electric blue with structured shoulders and nipped waist. Gold button details down the front.",
                    Price = 185m,
                    Era = "1980s",
                    Category = "80s",
                    Size = "8",
                    Condition = "excellent",
                    ImageUrl = "https://placehold.co/400x500/191970/FFF?text=Power+Dress",
                    InStock = true,
                    CreatedAtUtc = seededAt,
                    UpdatedAtUtc = seededAt
                },
                new Product
                {
                    Id = Guid.Parse("a1b2c3d4-0004-0000-0000-000000000004"),
                    Name = "Sequin Party Dress",
                    Description = "Dazzling 1980s sequin mini dress in hot pink. All-over sequin embellishment with dramatic puff sleeves.",
                    Price = 220m,
                    Era = "1980s",
                    Category = "80s",
                    Size = "6",
                    Condition = "good",
                    ImageUrl = "https://placehold.co/400x500/8B0000/FFF?text=Sequin+Dress",
                    InStock = true,
                    CreatedAtUtc = seededAt,
                    UpdatedAtUtc = seededAt
                },
                new Product
                {
                    Id = Guid.Parse("a1b2c3d4-0005-0000-0000-000000000005"),
                    Name = "Silk Slip Dress",
                    Description = "Minimalist 1990s silk slip dress in champagne. Bias-cut with delicate spaghetti straps and lace trim at the hem.",
                    Price = 210m,
                    Era = "1990s",
                    Category = "90s",
                    Size = "8",
                    Condition = "mint",
                    ImageUrl = "https://placehold.co/400x500/DAA520/FFF?text=Silk+Slip+Dress",
                    InStock = true,
                    CreatedAtUtc = seededAt,
                    UpdatedAtUtc = seededAt
                },
                new Product
                {
                    Id = Guid.Parse("a1b2c3d4-0006-0000-0000-000000000006"),
                    Name = "Grunge Babydoll Dress",
                    Description = "Classic 1990s babydoll dress in dark floral. Oversized fit with empire waist and velvet ribbon trim.",
                    Price = 145m,
                    Era = "1990s",
                    Category = "90s",
                    Size = "14",
                    Condition = "good",
                    ImageUrl = "https://placehold.co/400x500/2F4F4F/FFF?text=Babydoll+Dress",
                    InStock = true,
                    CreatedAtUtc = seededAt,
                    UpdatedAtUtc = seededAt
                },
                new Product
                {
                    Id = Guid.Parse("a1b2c3d4-0007-0000-0000-000000000007"),
                    Name = "Butterfly Halter Dress",
                    Description = "Early 2000s halter dress with butterfly print. Low-rise fit with handkerchief hem and rhinestone buckle detail.",
                    Price = 165m,
                    Era = "2000s",
                    Category = "y2k",
                    Size = "6",
                    Condition = "excellent",
                    ImageUrl = "https://placehold.co/400x500/FF69B4/FFF?text=Y2K+Halter",
                    InStock = true,
                    CreatedAtUtc = seededAt,
                    UpdatedAtUtc = seededAt
                },
                new Product
                {
                    Id = Guid.Parse("a1b2c3d4-0008-0000-0000-000000000008"),
                    Name = "Velvet Mini Dress",
                    Description = "Y2K velvet mini dress in deep plum. Scooped neckline with ruched sides and subtle stretch for a perfect fit.",
                    Price = 135m,
                    Era = "2000s",
                    Category = "y2k",
                    Size = "10",
                    Condition = "excellent",
                    ImageUrl = "https://placehold.co/400x500/8B4513/FFF?text=Velvet+Mini",
                    InStock = true,
                    CreatedAtUtc = seededAt,
                    UpdatedAtUtc = seededAt
                },
                new Product
                {
                    Id = Guid.Parse("a1b2c3d4-0009-0000-0000-000000000009"),
                    Name = "Asymmetric Midi Dress",
                    Description = "Contemporary asymmetric midi dress in sage green. One-shoulder design with pleated skirt and clean modern lines.",
                    Price = 285m,
                    Era = "2020s",
                    Category = "modern",
                    Size = "12",
                    Condition = "mint",
                    ImageUrl = "https://placehold.co/400x500/556B2F/FFF?text=Asymmetric+Midi",
                    InStock = true,
                    CreatedAtUtc = seededAt,
                    UpdatedAtUtc = seededAt
                },
                new Product
                {
                    Id = Guid.Parse("a1b2c3d4-0010-0000-0000-000000000010"),
                    Name = "Cut-Out Maxi Dress",
                    Description = "Modern cut-out maxi dress in black. Strategic side cut-outs with a high neck and flowing skirt.",
                    Price = 320m,
                    Era = "2020s",
                    Category = "modern",
                    Size = "16",
                    Condition = "mint",
                    ImageUrl = "https://placehold.co/400x500/1C1C1C/FFF?text=Cut-Out+Maxi",
                    InStock = true,
                    CreatedAtUtc = seededAt,
                    UpdatedAtUtc = seededAt
                }
            );
        });
    }

    public override int SaveChanges()
    {
        SetTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetTimestamps()
    {
        IEnumerable<EntityEntry<BaseEntity>> entries = ChangeTracker.Entries<BaseEntity>();
        DateTime utcNow = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = utcNow;
                entry.Entity.UpdatedAtUtc = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = utcNow;
            }
        }
    }
}
