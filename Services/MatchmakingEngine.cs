using System.Collections.Concurrent;
using System.Threading.Channels;
using Matchmaking.Api.Models;

namespace Matchmaking.Api.Services;

public sealed class MatchmakingEngine(IMatchmakingStrategy strategy, ILogger<MatchmakingEngine> log)
    : BackgroundService
{
    private enum Op
    {
        Add,
        Remove,
    }

    private readonly Channel<(Op op, PlayerTicket? ticket, string? playerId)> _ops =
        Channel.CreateUnbounded<(Op op, PlayerTicket? ticket, string? playerId)>();
    private readonly ConcurrentDictionary<string, Match> _playerToMatch = new();

    // Prevents duplicate enqueues
    private readonly ConcurrentDictionary<string, byte> _inPool = new();

    public ValueTask EnqueueAsync(PlayerTicket t, CancellationToken ct = default) =>
        _ops.Writer.WriteAsync((Op.Add, t, null), ct);

    public ValueTask RemoveTicketAsync(string id, CancellationToken ct = default) =>
        _ops.Writer.WriteAsync((Op.Remove, null, id), ct);

    public bool TryGetMatch(string playerId, out Match? match) =>
        _playerToMatch.TryGetValue(playerId, out match);

    public bool IsInPool(string playerId) => _inPool.ContainsKey(playerId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _ops.Reader;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1) Drain newly enqueued tickets quickly
                while (reader.TryRead(out var cmd))
                {
                    switch (cmd.op)
                    {
                        case Op.Add:
                            var t = cmd.ticket!;
                            if (!_playerToMatch.ContainsKey(t.PlayerId) && _inPool.TryAdd(t.PlayerId, 1))
                                strategy.AddTicket(t with { EnqueuedAtUtc = DateTime.UtcNow });
                            break;

                        case Op.Remove:
                            var id = cmd.playerId!;
                            _inPool.TryRemove(id, out _);
                            strategy.RemoveTicket(id);
                            break;
                    }
                }

                // 2) Ask strategy to form matches
                foreach (var m in strategy.TryMakeMatches(stoppingToken))
                {
                    _playerToMatch[m.PlayerA] = m;
                    _playerToMatch[m.PlayerB] = m;
                    _inPool.TryRemove(m.PlayerA, out _);
                    _inPool.TryRemove(m.PlayerB, out _);
                    log.LogInformation(
                        "Matched {A} vs {B} => {Id}",
                        m.PlayerA,
                        m.PlayerB,
                        m.MatchId
                    );
                }

                // 3) If nothing left to do, block until thhe next ticket arrives
                if (!reader.TryRead(out var next))
                {
                    try { next = await reader.ReadAsync(stoppingToken); } // blocks until new ticket arrives
                    catch (OperationCanceledException) { break; }

                    switch (next.op)
                    {
                        case Op.Add:
                            var t = next.ticket!;
                            if (!_playerToMatch.ContainsKey(t.PlayerId) && _inPool.TryAdd(t.PlayerId, 1))
                                strategy.AddTicket(t with { EnqueuedAtUtc = DateTime.UtcNow });
                            break;

                        case Op.Remove:
                            var id = next.playerId!;
                            _inPool.TryRemove(id, out _);
                            strategy.RemoveTicket(id);
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in matchmaking loop");
                await Task.Delay(250, stoppingToken);
            }
        }
    }
}
