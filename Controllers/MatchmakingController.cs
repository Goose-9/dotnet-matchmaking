using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Matchmaking.Api.Models;
using Matchmaking.Api.Services;

namespace Matchmaking.Api.Controllers;

[ApiController]
[Route("matchmaking")]
public class MatchmakingController(MatchmakingEngine engine, ILogger<MatchmakingController> log) : ControllerBase
{
    // ===== Request/Response DTOs =====
    public sealed record JoinRequest(
        [Required] string PlayerId,
        int? Elo,
        string? Region,
        int? ReportedPingMs
    );
    public sealed record LeaveRequest([Required] string TicketId);

    public sealed record JoinResponse(string TicketId, string Status);
    public sealed record MatchResponse(string Status, Match? Match);
    // public sealed record HealthResponse(int InPool, int Waiting, int MatchesCount);

    // ===== POST /matchmaking/join =====
    [HttpPost("join")]
    [ProducesResponseType(typeof(JoinResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Join([FromBody] JoinRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // If already queued, return the existing ticketId
        if (engine.TryGetActiveTicketForPlayer(req.PlayerId, out var existing))
            return Ok(new JoinResponse(existing, "queued"));

        var ticketId = Guid.NewGuid().ToString("N");
        var ticket = new PlayerTicket(
            PlayerId: req.PlayerId,
            Elo: req.Elo,
            Region: req.Region,
            ReportedPingMs: req.ReportedPingMs,
            EnqueuedAtUtc: DateTime.UtcNow
        );

        // Reserve mapping first (prevents races); if reservation fails, someone slipped in -> return existing
        if (!engine.TryReserveTicket(ticketId, ticket))
        {
            engine.TryGetActiveTicketForPlayer(req.PlayerId, out var current);
            return Ok(new JoinResponse(current!, "queued"));
        }

        await engine.EnqueueAsync(ticketId, ticket, ct);
        log.LogInformation("Join: {Player} => {Ticket}", req.PlayerId, ticketId);
        Response.Headers["X-Ticket-Id"] = ticketId;
        return Ok(new JoinResponse(ticketId, "queued"));
    }

    // GET /matchmaking/match  (ticket via query)
    [HttpGet("match")]
    [ProducesResponseType(typeof(MatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GetMatch([FromQuery] string? ticketId)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
            return BadRequest(new { error = "ticketId query parameter is required" });

        if (engine.TryGetMatchByTicket(ticketId, out var match) && match is not null)
            return Ok(new MatchResponse("matched", match));

        return Ok(new MatchResponse("searching", null));
    }

    // POST /matchmaking/leave
    [HttpPost("leave")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Leave([FromBody] LeaveRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        await engine.RemoveTicketAsync(req.TicketId, ct);
        log.LogInformation("Leave: ticket={TicketId}", req.TicketId);
        return Ok(new { status = "removed", ticketId = req.TicketId });
    }
}
