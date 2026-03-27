namespace WaitWise.Dal.Models;

public class ServiceLog
{
    public Guid Id { get; set; }
    public Guid QueueId { get; set; }
    public Guid TicketId { get; set; }
    public int ServiceDurationMinutes { get; set; }
    public DateTime LoggedAt { get; set; }

    public Queue Queue { get; set; } = null!;
}
