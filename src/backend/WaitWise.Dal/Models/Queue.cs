namespace WaitWise.Dal.Models;

public enum QueueStatus
{
    Open,
    Paused,
    Closed
}

public class Queue
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public QueueStatus Status { get; set; } = QueueStatus.Open;
    public int DefaultServiceMinutes { get; set; } = 5;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Location Location { get; set; } = null!;
    public ICollection<QueueTicket> Tickets { get; set; } = new List<QueueTicket>();
    public ICollection<ServiceLog> ServiceLogs { get; set; } = new List<ServiceLog>();
}
