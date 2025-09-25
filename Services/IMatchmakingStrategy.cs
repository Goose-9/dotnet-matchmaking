using Matchmaking.Api.Models;

namespace Matchmaking.Api.Services;

/// <summary>
///  Defines how players are paired. Implementations decide when two (or more) tickets form a valid match.
/// </summary>
public interface IMatchmakingStrategy
{
    /// <summary>Add a new ticket into the strategy’s pool.</summary>
    void AddTicket(PlayerTicket ticket);

    /// <summary>
    /// Try to produce zero or more matches. The engine will call this in a loop.
    /// Implementations should be fast and non-blocking; return what’s available.
    /// </summary>
    IEnumerable<Match> TryMakeMatches(CancellationToken ct);

    /// <summary>Clean-up API (kicks, cancels, expiry).</summary>
    void RemoveTicket(string playerId);
}
