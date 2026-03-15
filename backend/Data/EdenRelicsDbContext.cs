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
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(i => i.ProductName).HasMaxLength(200);
            entity.Property(i => i.UnitPrice).HasPrecision(10, 2);
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

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(p => p.Price).HasPrecision(10, 2);
            entity.Property(p => p.Name).HasMaxLength(200);
            entity.Property(p => p.Era).HasMaxLength(50);
            entity.Property(p => p.Category).HasMaxLength(20);
            entity.Property(p => p.Size).HasMaxLength(20);
            entity.Property(p => p.Condition).HasMaxLength(20);
            entity.Property(p => p.ImageUrl).HasMaxLength(500);

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
