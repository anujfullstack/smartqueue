using Microsoft.Extensions.Logging;
using WaitWise.Dal.Models;
using WaitWise.Dal.Repositories;

namespace WaitWise.Services.Tickets;

public class TicketService(
    IQueueTicketRepository ticketRepository,
    IQueueRepository queueRepository,
    ILogger<TicketService> logger) : ITicketService
{
    public async Task<TicketStatusResult> GetTicketStatusAsync(Guid ticketId, string guestToken)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found.");

        if (ticket.GuestToken != guestToken)
            throw new UnauthorizedAccessException("Invalid guest token.");

        var queue = await queueRepository.GetByIdAsync(ticket.QueueId)
            ?? throw new KeyNotFoundException("Queue not found.");

        var activeTickets = await ticketRepository.GetActiveTicketsForQueueAsync(ticket.QueueId);
        var position = activeTickets.FirstOrDefault(t => t.Id == ticketId)?.Position ?? 0;

        var avgServiceMinutes = await queueRepository.GetAverageServiceMinutesAsync(ticket.QueueId);
        var serviceMinutes = avgServiceMinutes > 0 ? avgServiceMinutes : queue.DefaultServiceMinutes;
        var estimatedWait = position > 0 ? (int)Math.Ceiling(serviceMinutes * (position - 1)) : 0;

        return new TicketStatusResult(
            ticket.Id,
            ticket.TicketNumber,
            ticket.Status.ToString(),
            position,
            estimatedWait,
            queue.Status.ToString(),
            ticket.JoinedAt
        );
    }

    public async Task CancelTicketAsync(Guid ticketId, string guestToken)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found.");

        if (ticket.GuestToken != guestToken)
            throw new UnauthorizedAccessException("Invalid guest token.");

        if (ticket.Status is not (TicketStatus.Waiting or TicketStatus.Called))
            throw new InvalidOperationException($"Cannot cancel a ticket with status {ticket.Status}.");

        await ticketRepository.UpdateStatusAsync(ticketId, TicketStatus.Cancelled);
        logger.LogInformation("Ticket {TicketId} cancelled by guest", ticketId);
    }

    public async Task MarkNoShowAsync(Guid ticketId)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found.");

        if (ticket.Status != TicketStatus.Called)
            throw new InvalidOperationException($"Cannot mark no-show — ticket status is {ticket.Status}.");

        await ticketRepository.UpdateStatusAsync(ticketId, TicketStatus.NoShow);
        logger.LogInformation("Ticket {TicketId} marked no-show", ticketId);
    }

    public async Task CompleteTicketAsync(Guid ticketId)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found.");

        if (ticket.Status is not (TicketStatus.Called or TicketStatus.InService))
            throw new InvalidOperationException($"Cannot complete ticket with status {ticket.Status}.");

        var durationMinutes = (int)Math.Ceiling((DateTime.UtcNow - (ticket.CalledAt ?? ticket.JoinedAt)).TotalMinutes);

        await ticketRepository.UpdateStatusAsync(ticketId, TicketStatus.Completed);

        await ticketRepository.AddServiceLogAsync(new ServiceLog
        {
            Id = Guid.NewGuid(),
            QueueId = ticket.QueueId,
            TicketId = ticketId,
            ServiceDurationMinutes = Math.Max(1, durationMinutes),
            LoggedAt = DateTime.UtcNow
        });

        logger.LogInformation("Ticket {TicketId} completed in {Duration}min", ticketId, durationMinutes);
    }
}
