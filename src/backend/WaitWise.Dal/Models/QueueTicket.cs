namespace WaitWise.Dal.Models;

public enum TicketStatus
{
    Waiting,
    Called,
    InService,
    Completed,
    Cancelled,
    NoShow,
    Expired
}

public class QueueTicket
{
    public Guid Id { get; set; }
    public Guid QueueId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string GuestToken { get; set; } = string.Empty;
    public int PartySize { get; set; } = 1;
    public int Position { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Waiting;
    public int EstimatedWaitMinutes { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? CalledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Queue Queue { get; set; } = null!;
}
