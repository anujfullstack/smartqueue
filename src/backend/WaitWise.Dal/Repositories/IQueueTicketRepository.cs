using WaitWise.Dal.Models;

namespace WaitWise.Dal.Repositories;

public interface IQueueTicketRepository
{
    Task<QueueTicket> CreateAsync(QueueTicket ticket);
    Task<QueueTicket?> GetByIdAsync(Guid id);
    Task<QueueTicket?> GetByGuestTokenAsync(string guestToken);
    Task<IReadOnlyList<QueueTicket>> GetActiveTicketsForQueueAsync(Guid queueId);
    Task UpdateStatusAsync(Guid id, TicketStatus status);
    Task UpdatePositionAsync(Guid id, int position);
    Task<int> GetNextTicketNumberAsync(Guid queueId);
    Task AddServiceLogAsync(ServiceLog log);
}
