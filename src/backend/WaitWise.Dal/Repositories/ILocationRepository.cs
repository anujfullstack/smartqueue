using WaitWise.Dal.Models;

namespace WaitWise.Dal.Repositories;

public interface ILocationRepository
{
    Task<Location?> GetByIdAsync(Guid id);
    Task<Location?> GetBySlugAsync(string slug);
}
