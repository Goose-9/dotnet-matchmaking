namespace Matchmaking.Api.Models;

public sealed record PlayerTicket(
    string PlayerId,
    int? Elo = null,
    string? Region = null,
    int? ReportedPingMs = null,
    DateTime EnqueuedAtUtc = default
);
