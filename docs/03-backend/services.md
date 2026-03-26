# Service Layer Design

## Principles

- Every service has a corresponding interface
- Services contain **all** business logic — no logic lives in API endpoints or repositories
- Services depend only on DAL interfaces — never on infrastructure directly
- Services never call agents
- State transition validation lives in the service layer, not the repository

---

## Service Interfaces

### `ILocationService`

```csharp
Task<Location> GetBySlugAsync(string slug);
Task<Location> GetByIdAsync(Guid locationId);
Task<bool> IsActiveAsync(Guid locationId);
```

---

### `IQueueService`

```csharp
Task<QueueStatusResult> GetStatusAsync(Guid queueId);
Task OpenQueueAsync(Guid queueId, Guid operatorUserId);
Task PauseQueueAsync(Guid queueId, Guid operatorUserId);
Task CloseQueueAsync(Guid queueId, Guid operatorUserId);
Task<AdvanceQueueResult> AdvanceAsync(Guid queueId, TicketAdvanceAction action, Guid operatorUserId);
Task<QueueTicket> AddWalkInAsync(Guid queueId, string displayName, string? phone, Guid operatorUserId);
```

---

### `ITicketService`

```csharp
Task<QueueTicket> JoinQueueAsync(Guid queueId, string displayName, string? phone);
Task<QueueTicket> GetByGuestTokenAsync(string guestToken);
Task<LiveTicketStatus> GetLiveStatusAsync(string guestToken);
Task CancelAsync(string guestToken);
Task CompleteAsync(Guid ticketId, Guid operatorUserId);
Task SkipAsync(Guid ticketId, Guid operatorUserId);
Task MarkNoShowAsync(Guid ticketId);
Task RemoveAsync(Guid ticketId, Guid operatorUserId);
Task<IReadOnlyList<TicketTransition>> GetHistoryAsync(Guid ticketId);
Task RegisterPushSubscriptionAsync(string guestToken, PushSubscriptionDto subscription);
Task ExpireNoShowAsync(Guid ticketId);  // called by BackgroundService on timer expiry
```

---

### `IWaitTimeEstimatorService`

```csharp
Task<int> CalculateEstimatedWaitSecondsAsync(Guid queueId, int position);
Task RecordServiceCompletionAsync(Guid queueId, Guid ticketId, int durationSeconds);
```

**ETA calculation logic:**

```
SELECT AVG(service_duration_seconds)
FROM service_log
WHERE queue_id = :queueId
ORDER BY recorded_at DESC
LIMIT 10

If fewer than 3 records exist → use queues.avg_service_seconds as fallback

estimated_wait_seconds = position × avg_service_seconds
```

---

### `INotificationService`

```csharp
Task SendPushAsync(Guid ticketId, NotificationTrigger trigger);
Task SendSmsAsync(Guid ticketId, NotificationTrigger trigger);
Task ScheduleNoShowExpiryAsync(Guid ticketId, int windowSeconds);
```

**Notification flow:**
1. `SendPushAsync` is attempted first — writes `notification_events` row with status `Pending`
2. If push delivery is not confirmed within 30 seconds, `SendSmsAsync` fires automatically
3. Both calls update the `notification_events` row to `Sent` or `Failed`

---

### `IAccessControlService`

```csharp
Task<bool> CanAccessLocationAsync(Guid userId, Guid locationId);
Task<bool> HasRoleAsync(Guid userId, Guid locationId, LocationRole role);
Task AssignStaffAsync(Guid locationId, Guid userId, LocationRole role, Guid assignedBy);
Task RemoveStaffAsync(Guid locationId, Guid userId);
Task<IReadOnlyList<StaffAssignment>> GetStaffAsync(Guid locationId);
```

> Called at the **start** of every protected service method before any DAL interaction. Authentication proves identity; this service proves access to the location.

---

### `IFeedbackService`

```csharp
Task SubmitAsync(string guestToken, int rating, string? comment);
Task<PagedResult<CustomerFeedback>> GetForLocationAsync(Guid locationId, int page, int pageSize);
```

---

### `IBillService`

```csharp
Task<Bill> CreateAsync(string guestToken, CreateBillRequest request);
Task<Bill> GetByShareTokenAsync(string shareToken);
```

---

### `IVenueProfileService`

```csharp
Task<VenueProfile?> GetAsync(Guid locationId);
Task UpsertAsync(Guid locationId, UpsertVenueProfileRequest request);
```

---

### `IInviteService`

```csharp
Task<InviteToken> GenerateAsync(string guestToken);
Task RecordUseAsync(string token);
```

---

## Business Rules by Service

### Queue Rules (`IQueueService`)

- A queue may only be **opened** if its current status is `Closed`
- A queue may only be **paused** if its current status is `Open`
- A queue may only be **closed** if its current status is `Open` or `Paused`
- Closing a queue transitions all `Waiting` and `Notified` tickets to `Expired`
- `AdvanceAsync` marks the current `Called` ticket and calls the next `Waiting` ticket **atomically** in a single transaction

### Ticket State Machine (`ITicketService`)

Valid transitions:

| From | To | Trigger |
|---|---|---|
| *(none)* | `Waiting` | Customer joins queue |
| `Waiting` | `Notified` | 2–3 ahead notification sent |
| `Notified` | `Called` | Staff advances queue |
| `Called` | `InService` | Customer arrives at counter |
| `Called` | `NoShow` | No-show window expires |
| `InService` | `Completed` | Staff marks as served |
| `Waiting` / `Notified` | `Cancelled` | Customer or staff cancels |
| `Waiting` / `Notified` | `Expired` | Queue closed while ticket is active |

Any other transition raises `INVALID_TICKET_TRANSITION` → HTTP 409.

### Ticket Join Rules (`ITicketService`)

- Queue must be `Open` — otherwise returns `QUEUE_ALREADY_CLOSED`
- If `max_capacity` is set and `queue_depth >= max_capacity` — returns `QUEUE_FULL`
- `guest_token` is a cryptographically random 64-character string
- `ticket_number` is assigned as `MAX(ticket_number) + 1` within the queue session, using a row-level lock for concurrency safety
- Position is set and written to Redis immediately after insert

### No-Show Rule

- When a ticket moves to `Called`, `no_show_deadline = NOW() + queue.no_show_window_seconds`
- A Redis key `notify:noshow:{ticketId}` is created with a matching TTL
- A `BackgroundService` monitors Redis expiry events and calls `ITicketService.ExpireNoShowAsync`
- The operator may override by manually completing, skipping, or removing the ticket before the deadline

### ETA Recalculation

`IWaitTimeEstimatorService.CalculateEstimatedWaitSecondsAsync` is called:
- On every ticket join (to give the customer their initial ETA)
- On every ticket transition (to recalculate ETA for remaining tickets in the queue)

Result is written to `queue_tickets.estimated_wait_seconds` and to the Redis position key.

---

## DI Registration

```csharp
// WaitWise.Services/ServiceCollectionExtensions.cs
public static IServiceCollection AddDomainServices(this IServiceCollection services)
{
    services.AddScoped<ILocationService, LocationService>();
    services.AddScoped<IQueueService, QueueService>();
    services.AddScoped<ITicketService, TicketService>();
    services.AddScoped<IWaitTimeEstimatorService, WaitTimeEstimatorService>();
    services.AddScoped<INotificationService, NotificationService>();
    services.AddScoped<IAccessControlService, AccessControlService>();
    services.AddScoped<IFeedbackService, FeedbackService>();
    services.AddScoped<IBillService, BillService>();
    services.AddScoped<IVenueProfileService, VenueProfileService>();
    services.AddScoped<IInviteService, InviteService>();
    return services;
}
```
