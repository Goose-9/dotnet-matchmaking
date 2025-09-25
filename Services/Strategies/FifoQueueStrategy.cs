using System.Collections.Concurrent;
using System.Diagnostics;
using Matchmaking.Api.Models;

namespace Matchmaking.Api.Services;

/// <summary>
/// Fifo queue implemented using linked list and Dictionary for O(1) random removals
/// </summary>
public sealed class FifoQueueStrategy : IMatchmakingStrategy
{
    private readonly LinkedList<PlayerTicket> _list = new();
    private readonly Dictionary<string, LinkedListNode<PlayerTicket>> _index = new(StringComparer.Ordinal);

    public void AddTicket(PlayerTicket t)
    {
        if (_index.TryGetValue(t.PlayerId, out var stale))
        {
            _list.Remove(stale);
            _index.Remove(t.PlayerId);
        }

        var node = _list.AddLast(t);
        _index[t.PlayerId] = node;
    }

    public void RemoveTicket(string playerId)
    {
        if (_index.TryGetValue(playerId, out var node))
        {
            _list.Remove(node);
            _index.Remove(playerId);
        }
    }

    public IEnumerable<Match> TryMakeMatches(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _list.Count >= 2)
        {
            // Dequeue two ids
            var aNode = _list.First!;
            _list.RemoveFirst();
            var bNode = _list.First!;
            _list.RemoveFirst();

            var aId = aNode.Value.PlayerId;
            var bId = bNode.Value.PlayerId;

            _index.Remove(aId);
            _index.Remove(bId);

            yield return new Match(
                MatchId: Guid.NewGuid().ToString("N"),
                PlayerA: aId,
                PlayerB: bId,
                CreatedAtUtc: DateTime.UtcNow
            );
        }
    }
}