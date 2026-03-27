using Microsoft.EntityFrameworkCore;
using WaitWise.Dal.Models;

namespace WaitWise.Dal;

public class WaitWiseDbContext(DbContextOptions<WaitWiseDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Queue> Queues => Set<Queue>();
    public DbSet<QueueTicket> QueueTickets => Set<QueueTicket>();
    public DbSet<ServiceLog> ServiceLogs => Set<ServiceLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
        });

        modelBuilder.Entity<Location>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasOne(x => x.Organization).WithMany(x => x.Locations)
                .HasForeignKey(x => x.OrganizationId);
        });

        modelBuilder.Entity<Queue>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Location).WithMany(x => x.Queues)
                .HasForeignKey(x => x.LocationId);
        });

        modelBuilder.Entity<QueueTicket>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.GuestToken).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.GuestToken).IsUnique();
            e.HasIndex(x => new { x.QueueId, x.Status });
            e.HasOne(x => x.Queue).WithMany(x => x.Tickets)
                .HasForeignKey(x => x.QueueId);
        });

        modelBuilder.Entity<ServiceLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Queue).WithMany(x => x.ServiceLogs)
                .HasForeignKey(x => x.QueueId);
        });
    }
}
