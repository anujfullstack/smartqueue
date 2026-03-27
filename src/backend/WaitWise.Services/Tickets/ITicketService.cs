namespace WaitWise.Services.Tickets;

public record TicketStatusResult(
    Guid TicketId,
    string TicketNumber,
    string Status,
    int Position,
    int EstimatedWaitMinutes,
    string QueueStatus,
    DateTime JoinedAt
);

public interface ITicketService
{
    Task<TicketStatusResult> GetTicketStatusAsync(Guid ticketId, string guestToken);
    Task CancelTicketAsync(Guid ticketId, string guestToken);
    Task MarkNoShowAsync(Guid ticketId);
    Task CompleteTicketAsync(Guid ticketId);
}
