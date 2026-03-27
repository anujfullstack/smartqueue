using WaitWise.Dal;
using WaitWise.Dal.Models;

namespace WaitWise.Api.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(WaitWiseDbContext db)
    {
        if (db.Organizations.Any())
            return;

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Ovenfresh Bakery",
            Slug = "ovenfresh",
            CreatedAt = DateTime.UtcNow
        };
        db.Organizations.Add(org);

        var location = new Location
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Name = "Ovenfresh Café – Main Branch",
            Slug = "ovenfresh-main",
            Description = "Freshly baked goods and great coffee since 2010.",
            Address = "12 Baker Street, Mumbai",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Locations.Add(location);

        var queue = new Queue
        {
            Id = Guid.NewGuid(),
            LocationId = location.Id,
            Name = "Walk-in Queue",
            Status = QueueStatus.Open,
            DefaultServiceMinutes = 5,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Queues.Add(queue);

        await db.SaveChangesAsync();
    }
}
