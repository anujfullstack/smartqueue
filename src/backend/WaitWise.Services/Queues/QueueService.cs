using Microsoft.Extensions.Logging;
using WaitWise.Dal.Models;
using WaitWise.Dal.Repositories;

namespace WaitWise.Services.Queues;

public class QueueService(
    IQueueRepository queueRepository,
    IQueueTicketRepository ticketRepository,
    ILogger<QueueService> logger) : IQueueService
{
    public async Task<JoinQueueResult> JoinQueueAsync(Guid queueId, string guestName, int partySize)
    {
        var queue = await queueRepository.GetByIdAsync(queueId)
            ?? throw new KeyNotFoundException($"Queue {queueId} not found.");

        if (queue.Status != QueueStatus.Open)
            throw new InvalidOperationException($"Queue is {queue.Status} and not accepting new guests.");

        var activeTickets = await ticketRepository.GetActiveTicketsForQueueAsync(queueId);
        var position = activeTickets.Count + 1;
        var avgServiceMinutes = await queueRepository.GetAverageServiceMinutesAsync(queueId);
        var serviceMinutes = avgServiceMinutes > 0 ? avgServiceMinutes : queue.DefaultServiceMinutes;
        var estimatedWait = (int)Math.Ceiling(serviceMinutes * (position - 1));

        var ticket = new QueueTicket
        {
            Id = Guid.NewGuid(),
            QueueId = queueId,
            TicketNumber = $"#{position:D3}",
            GuestName = guestName,
            GuestToken = Guid.NewGuid().ToString("N"),
            PartySize = partySize,
            Position = position,
            Status = TicketStatus.Waiting,
            EstimatedWaitMinutes = estimatedWait,
            JoinedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await ticketRepository.CreateAsync(ticket);
        logger.LogInformation("Guest joined queue {QueueId} at position {Position}", queueId, position);

        return new JoinQueueResult(ticket.Id, ticket.TicketNumber, ticket.GuestToken, position, estimatedWait);
    }

    public async Task<QueueStatusResult> GetQueueStatusAsync(Guid queueId)
    {
        var queue = await queueRepository.GetByIdAsync(queueId)
            ?? throw new KeyNotFoundException($"Queue {queueId} not found.");

        var activeTickets = await ticketRepository.GetActiveTicketsForQueueAsync(queueId);
        var avgServiceMinutes = await queueRepository.GetAverageServiceMinutesAsync(queueId);
        var serviceMinutes = avgServiceMinutes > 0 ? avgServiceMinutes : queue.DefaultServiceMinutes;
        var estimatedWait = (int)Math.Ceiling(serviceMinutes * activeTickets.Count);

        return new QueueStatusResult(
            queue.Id,
            queue.Name,
            queue.Status.ToString(),
            activeTickets.Count,
            estimatedWait
        );
    }

    public async Task AdvanceQueueAsync(Guid queueId)
    {
        var queue = await queueRepository.GetByIdAsync(queueId)
            ?? throw new KeyNotFoundException($"Queue {queueId} not found.");

        var activeTickets = await ticketRepository.GetActiveTicketsForQueueAsync(queueId);
        var next = activeTickets.FirstOrDefault(t => t.Status == TicketStatus.Waiting);

        if (next is null)
        {
            logger.LogWarning("Advance called on queue {QueueId} but no waiting tickets", queueId);
            return;
        }

        await ticketRepository.UpdateStatusAsync(next.Id, TicketStatus.Called);
        logger.LogInformation("Advanced queue {QueueId} — ticket {TicketId} called", queueId, next.Id);

        // Recalculate positions for remaining waiting tickets
        var avgServiceMinutes = await queueRepository.GetAverageServiceMinutesAsync(queueId);
        var serviceMinutes = avgServiceMinutes > 0 ? avgServiceMinutes : queue.DefaultServiceMinutes;

        var remaining = activeTickets
            .Where(t => t.Id != next.Id && t.Status == TicketStatus.Waiting)
            .OrderBy(t => t.Position)
            .ToList();

        for (var i = 0; i < remaining.Count; i++)
        {
            var newPosition = i + 1;
            await ticketRepository.UpdatePositionAsync(remaining[i].Id, newPosition);
        }
    }

    public async Task PauseQueueAsync(Guid queueId)
    {
        await queueRepository.UpdateStatusAsync(queueId, QueueStatus.Paused);
        logger.LogInformation("Queue {QueueId} paused", queueId);
    }

    public async Task CloseQueueAsync(Guid queueId)
    {
        await queueRepository.UpdateStatusAsync(queueId, QueueStatus.Closed);
        logger.LogInformation("Queue {QueueId} closed", queueId);
    }

    public async Task ReopenQueueAsync(Guid queueId)
    {
        await queueRepository.UpdateStatusAsync(queueId, QueueStatus.Open);
        logger.LogInformation("Queue {QueueId} reopened", queueId);
    }
}
