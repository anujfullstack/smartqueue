namespace WaitWise.Services.Queues;

public record QueueStatusResult(
    Guid QueueId,
    string QueueName,
    string Status,
    int ActiveCount,
    int EstimatedWaitMinutes
);

public record JoinQueueResult(
    Guid TicketId,
    string TicketNumber,
    string GuestToken,
    int Position,
    int EstimatedWaitMinutes
);

public interface IQueueService
{
    Task<JoinQueueResult> JoinQueueAsync(Guid queueId, string guestName, int partySize);
    Task<QueueStatusResult> GetQueueStatusAsync(Guid queueId);
    Task AdvanceQueueAsync(Guid queueId);
    Task PauseQueueAsync(Guid queueId);
    Task CloseQueueAsync(Guid queueId);
    Task ReopenQueueAsync(Guid queueId);
}
