using Microsoft.EntityFrameworkCore;

namespace EdenRelics.SellerTool.Data;

/// <summary>
/// Persistence for the seller tool: the garment archive (garment → typed evidence → derived date
/// estimates, each with a confirmation state) and the dating rules store. Separate from the shop's
/// database — the tool is a decoupled service. Enums are stored as strings so the archive stays
/// human-readable.
/// </summary>
public class ToolDbContext(DbContextOptions<ToolDbContext> options) : DbContext(options)
{
    public DbSet<Garment> Garments => Set<Garment>();
    public DbSet<EvidenceRecord> EvidenceRecords => Set<EvidenceRecord>();
    public DbSet<DateEstimate> DateEstimates => Set<DateEstimate>();
    public DbSet<StoredRule> StoredRules => Set<StoredRule>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Garment>(e =>
        {
            e.Property(g => g.Reference).HasMaxLength(200);
            e.Property(g => g.Title).HasMaxLength(300);
            e.Property(g => g.SellerRef).HasMaxLength(200);
            e.HasMany(g => g.Evidence).WithOne(r => r.Garment!).HasForeignKey(r => r.GarmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(g => g.Estimates).WithOne(d => d.Garment!).HasForeignKey(d => d.GarmentId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EvidenceRecord>(e =>
        {
            e.Property(r => r.Type).HasConversion<string>().HasMaxLength(32);
            e.Property(r => r.Feature).HasMaxLength(120);
            e.Property(r => r.RawValue).HasMaxLength(1000);
            e.Property(r => r.ImageKey).HasMaxLength(512);
            e.Property(r => r.Origin).HasMaxLength(20);
            e.Property(r => r.Confirmation).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.ConfirmedBy).HasMaxLength(200);
            e.HasIndex(r => r.GarmentId);
            e.HasIndex(r => r.Feature);
        });

        b.Entity<DateEstimate>(e =>
        {
            e.Property(d => d.Outcome).HasMaxLength(32);
            e.Property(d => d.Confirmation).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(d => d.GarmentId);
        });

        b.Entity<StoredRule>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasMaxLength(64);
            e.Property(r => r.Feature).HasMaxLength(120);
            e.Property(r => r.Type).HasConversion<string>().HasMaxLength(32);
            e.Property(r => r.Strength).HasConversion<string>().HasMaxLength(8);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.SourceCitation).HasMaxLength(1000);
            e.HasIndex(r => new { r.Status, r.Feature });
        });
    }

    public override int SaveChanges()
    {
        Stamp();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Stamp();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void Stamp()
    {
        DateTime now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<ToolBaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
        }
    }
}
