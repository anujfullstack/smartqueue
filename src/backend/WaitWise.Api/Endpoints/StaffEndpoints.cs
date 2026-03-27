using WaitWise.Dal.Repositories;
using WaitWise.Services.Queues;
using WaitWise.Services.Tickets;

namespace WaitWise.Api.Endpoints;

public static class StaffEndpoints
{
    public static void MapStaffEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").AddEndpointFilter<StaffApiKeyFilter>();

        group.MapPost("/queues/{queueId:guid}/advance", async (
            Guid queueId,
            IQueueService queueService,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("StaffEndpoints");
            try
            {
                await queueService.AdvanceQueueAsync(queueId);
                return Results.Ok();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { code = "NOT_FOUND", message = "Queue not found.", details = Array.Empty<string>() });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error advancing queue {QueueId}", queueId);
                return Results.Problem(statusCode: 500, title: "An unexpected error occurred.");
            }
        });

        group.MapPost("/queues/{queueId:guid}/pause", async (
            Guid queueId,
            IQueueService queueService) =>
        {
            await queueService.PauseQueueAsync(queueId);
            return Results.Ok();
        });

        group.MapPost("/queues/{queueId:guid}/close", async (
            Guid queueId,
            IQueueService queueService) =>
        {
            await queueService.CloseQueueAsync(queueId);
            return Results.Ok();
        });

        group.MapPost("/queues/{queueId:guid}/reopen", async (
            Guid queueId,
            IQueueService queueService) =>
        {
            await queueService.ReopenQueueAsync(queueId);
            return Results.Ok();
        });

        group.MapGet("/admin/queues/{queueId:guid}/tickets", async (
            Guid queueId,
            IQueueTicketRepository ticketRepo) =>
        {
            var tickets = await ticketRepo.GetActiveTicketsForQueueAsync(queueId);
            var result = tickets.Select(t => new
            {
                t.Id,
                t.TicketNumber,
                t.GuestName,
                t.PartySize,
                Status = t.Status.ToString(),
                t.Position,
                t.JoinedAt,
                WaitingMinutes = (int)(DateTime.UtcNow - t.JoinedAt).TotalMinutes
            });
            return Results.Ok(result);
        });

        group.MapPost("/tickets/{ticketId:guid}/no-show", async (
            Guid ticketId,
            ITicketService ticketService) =>
        {
            try
            {
                await ticketService.MarkNoShowAsync(ticketId);
                return Results.Ok();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { code = "NOT_FOUND", message = "Ticket not found.", details = Array.Empty<string>() });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { code = "INVALID_STATE", message = ex.Message, details = Array.Empty<string>() });
            }
        });

        group.MapPost("/tickets/{ticketId:guid}/complete", async (
            Guid ticketId,
            ITicketService ticketService) =>
        {
            try
            {
                await ticketService.CompleteTicketAsync(ticketId);
                return Results.Ok();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { code = "NOT_FOUND", message = "Ticket not found.", details = Array.Empty<string>() });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { code = "INVALID_STATE", message = ex.Message, details = Array.Empty<string>() });
            }
        });
    }
}

// Demo-only API key guard. Replace with JWT auth (Issue #8) post-demo.
public class StaffApiKeyFilter(IConfiguration configuration) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var expectedKey = configuration["Staff:ApiKey"];
        if (string.IsNullOrEmpty(expectedKey))
            return await next(ctx);

        ctx.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var providedKey);
        if (providedKey != expectedKey)
            return Results.StatusCode(401);

        return await next(ctx);
    }
}
