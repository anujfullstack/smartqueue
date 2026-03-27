using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WaitWise.Dal.Models;

namespace WaitWise.Dal.Repositories;

public class QueueTicketRepository(WaitWiseDbContext db, ILogger<QueueTicketRepository> logger) : IQueueTicketRepository
{
    private static readonly TicketStatus[] ActiveStatuses = [TicketStatus.Waiting, TicketStatus.Called, TicketStatus.InService];

    public async Task<QueueTicket> CreateAsync(QueueTicket ticket)
    {
        db.QueueTickets.Add(ticket);
        await db.SaveChangesAsync();
        logger.LogInformation("Created ticket {TicketId} for queue {QueueId}", ticket.Id, ticket.QueueId);
        return ticket;
    }

    public async Task<QueueTicket?> GetByIdAsync(Guid id)
    {
        return await db.QueueTickets.FindAsync(id);
    }

    public async Task<QueueTicket?> GetByGuestTokenAsync(string guestToken)
    {
        return await db.QueueTickets.FirstOrDefaultAsync(t => t.GuestToken == guestToken);
    }

    public async Task<IReadOnlyList<QueueTicket>> GetActiveTicketsForQueueAsync(Guid queueId)
    {
        return await db.QueueTickets
            .Where(t => t.QueueId == queueId && ActiveStatuses.Contains(t.Status))
            .OrderBy(t => t.Position)
            .ToListAsync();
    }

    public async Task UpdateStatusAsync(Guid id, TicketStatus status)
    {
        logger.LogInformation("Updating ticket {TicketId} status to {Status}", id, status);
        await db.QueueTickets
            .Where(t => t.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, status)
                .SetProperty(t => t.UpdatedAt, DateTime.UtcNow));
    }

    public async Task UpdatePositionAsync(Guid id, int position)
    {
        await db.QueueTickets
            .Where(t => t.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Position, position)
                .SetProperty(t => t.UpdatedAt, DateTime.UtcNow));
    }

    public async Task<int> GetNextTicketNumberAsync(Guid queueId)
    {
        var max = await db.QueueTickets
            .Where(t => t.QueueId == queueId)
            .MaxAsync(t => (int?)t.Position) ?? 0;
        return max + 1;
    }

    public async Task AddServiceLogAsync(ServiceLog log)
    {
        db.ServiceLogs.Add(log);
        await db.SaveChangesAsync();
    }
}
