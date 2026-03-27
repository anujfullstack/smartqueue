using Microsoft.AspNetCore.Mvc;
using WaitWise.Dal.Repositories;
using WaitWise.Services.Queues;
using WaitWise.Services.Tickets;

namespace WaitWise.Api.Endpoints;

public static class PublicEndpoints
{
    public static void MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        group.MapGet("/locations/{slug}", async (
            string slug,
            ILocationRepository locationRepo,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PublicEndpoints");
            try
            {
                var location = await locationRepo.GetBySlugAsync(slug);
                if (location is null)
                    return Results.NotFound(new { code = "NOT_FOUND", message = "Location not found.", details = Array.Empty<string>() });

                var result = new
                {
                    location.Id,
                    location.Name,
                    location.Slug,
                    location.Description,
                    location.Address,
                    Queues = location.Queues.Select(q => new
                    {
                        q.Id,
                        q.Name,
                        Status = q.Status.ToString()
                    })
                };
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching location {Slug}", slug);
                return Results.Problem(statusCode: 500, title: "An unexpected error occurred.");
            }
        });

        group.MapGet("/queues/{queueId:guid}/status", async (
            Guid queueId,
            IQueueService queueService,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PublicEndpoints");
            try
            {
                var status = await queueService.GetQueueStatusAsync(queueId);
                return Results.Ok(status);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { code = "NOT_FOUND", message = "Queue not found.", details = Array.Empty<string>() });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching queue status {QueueId}", queueId);
                return Results.Problem(statusCode: 500, title: "An unexpected error occurred.");
            }
        });

        group.MapPost("/queues/{queueId:guid}/join", async (
            Guid queueId,
            [FromBody] JoinQueueRequest request,
            IQueueService queueService,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PublicEndpoints");
            try
            {
                if (string.IsNullOrWhiteSpace(request.GuestName))
                    return Results.BadRequest(new { code = "VALIDATION_ERROR", message = "Guest name is required.", details = Array.Empty<string>() });

                if (request.PartySize < 1)
                    return Results.BadRequest(new { code = "VALIDATION_ERROR", message = "Party size must be at least 1.", details = Array.Empty<string>() });

                var result = await queueService.JoinQueueAsync(queueId, request.GuestName, request.PartySize);
                return Results.Created($"/api/tickets/{result.TicketId}/{result.GuestToken}", result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { code = "NOT_FOUND", message = "Queue not found.", details = Array.Empty<string>() });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { code = "QUEUE_NOT_OPEN", message = ex.Message, details = Array.Empty<string>() });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error joining queue {QueueId}", queueId);
                return Results.Problem(statusCode: 500, title: "An unexpected error occurred.");
            }
        });

        group.MapGet("/tickets/{ticketId:guid}/{guestToken}", async (
            Guid ticketId,
            string guestToken,
            ITicketService ticketService,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PublicEndpoints");
            try
            {
                var status = await ticketService.GetTicketStatusAsync(ticketId, guestToken);
                return Results.Ok(status);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { code = "NOT_FOUND", message = "Ticket not found.", details = Array.Empty<string>() });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.StatusCode(403);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching ticket status {TicketId}", ticketId);
                return Results.Problem(statusCode: 500, title: "An unexpected error occurred.");
            }
        });

        group.MapPost("/tickets/{ticketId:guid}/{guestToken}/cancel", async (
            Guid ticketId,
            string guestToken,
            ITicketService ticketService,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PublicEndpoints");
            try
            {
                await ticketService.CancelTicketAsync(ticketId, guestToken);
                return Results.Ok();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { code = "NOT_FOUND", message = "Ticket not found.", details = Array.Empty<string>() });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.StatusCode(403);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { code = "INVALID_STATE", message = ex.Message, details = Array.Empty<string>() });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error cancelling ticket {TicketId}", ticketId);
                return Results.Problem(statusCode: 500, title: "An unexpected error occurred.");
            }
        });
    }
}

public record JoinQueueRequest(string GuestName, int PartySize = 1);
