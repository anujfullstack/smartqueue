using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WaitWise.Dal.Models;

namespace WaitWise.Dal.Repositories;

public class LocationRepository(WaitWiseDbContext db, ILogger<LocationRepository> logger) : ILocationRepository
{
    public async Task<Location?> GetByIdAsync(Guid id)
    {
        logger.LogDebug("Fetching location {LocationId}", id);
        return await db.Locations
            .Include(l => l.Queues)
            .FirstOrDefaultAsync(l => l.Id == id && l.IsActive);
    }

    public async Task<Location?> GetBySlugAsync(string slug)
    {
        logger.LogDebug("Fetching location by slug {Slug}", slug);
        return await db.Locations
            .Include(l => l.Queues)
            .FirstOrDefaultAsync(l => l.Slug == slug && l.IsActive);
    }
}
