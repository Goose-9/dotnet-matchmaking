namespace Matchmaking.Api.Models;

public record Match(string MatchId, string PlayerA, string PlayerB, DateTime CreatedAtUtc);
