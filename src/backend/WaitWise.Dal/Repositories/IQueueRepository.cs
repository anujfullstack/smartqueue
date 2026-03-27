using WaitWise.Dal.Models;

namespace WaitWise.Dal.Repositories;

public interface IQueueRepository
{
    Task<Queue?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<Queue>> GetByLocationIdAsync(Guid locationId);
    Task UpdateStatusAsync(Guid id, QueueStatus status);
    Task<double> GetAverageServiceMinutesAsync(Guid queueId, int sampleSize = 10);
}
