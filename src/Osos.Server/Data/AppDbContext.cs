using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Osos.Server.Data;

public sealed class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<OsosCredential> OsosCredentials => Set<OsosCredential>();
    public DbSet<SearchHistory> SearchHistories => Set<SearchHistory>();
    public DbSet<SearchResultSnapshot> SearchResultSnapshots => Set<SearchResultSnapshot>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<OsosCredential>(e =>
        {
            e.HasIndex(x => x.AppUserId).IsUnique();
            e.HasOne(x => x.AppUser).WithOne(u => u.OsosCredential)
             .HasForeignKey<OsosCredential>(x => x.AppUserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SearchHistory>(e =>
        {
            e.HasIndex(x => new { x.AppUserId, x.CreatedAt });
            e.HasOne(x => x.AppUser).WithMany(u => u.Searches)
             .HasForeignKey(x => x.AppUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Snapshot).WithOne(s => s.SearchHistory)
             .HasForeignKey<SearchResultSnapshot>(s => s.SearchHistoryId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
