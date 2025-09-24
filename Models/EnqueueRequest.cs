namespace Matchmaking.Api.Models;

public sealed class EnqueueRequest
{
    public string? PlayerId { get; init; }
    public int? Elo { get; init; }
    public string? Region { get; init; }
    public int? ReportedPingMs { get; init; }
}