using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WaitWise.Dal.Models;

namespace WaitWise.Dal.Repositories;

public class QueueRepository(WaitWiseDbContext db, ILogger<QueueRepository> logger) : IQueueRepository
{
    public async Task<Queue?> GetByIdAsync(Guid id)
    {
        logger.LogDebug("Fetching queue {QueueId}", id);
        return await db.Queues.FindAsync(id);
    }

    public async Task<IReadOnlyList<Queue>> GetByLocationIdAsync(Guid locationId)
    {
        logger.LogDebug("Fetching queues for location {LocationId}", locationId);
        return await db.Queues
            .Where(q => q.LocationId == locationId)
            .ToListAsync();
    }

    public async Task UpdateStatusAsync(Guid id, QueueStatus status)
    {
        logger.LogInformation("Updating queue {QueueId} status to {Status}", id, status);
        await db.Queues
            .Where(q => q.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(q => q.Status, status)
                .SetProperty(q => q.UpdatedAt, DateTime.UtcNow));
    }

    public async Task<double> GetAverageServiceMinutesAsync(Guid queueId, int sampleSize = 10)
    {
        var avg = await db.ServiceLogs
            .Where(s => s.QueueId == queueId)
            .OrderByDescending(s => s.LoggedAt)
            .Take(sampleSize)
            .AverageAsync(s => (double?)s.ServiceDurationMinutes);

        return avg ?? 0;
    }
}
