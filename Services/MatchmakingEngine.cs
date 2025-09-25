using System.Collections.Concurrent;
using System.Numerics;
using System.Threading.Channels;
using Matchmaking.Api.Models;

namespace Matchmaking.Api.Services;

public sealed class MatchmakingEngine(IMatchmakingStrategy strategy, ILogger<MatchmakingEngine> log)
    : BackgroundService
{
    private enum Op { Add, Remove }

    private readonly Channel<(Op op, string ticketId, PlayerTicket? ticket)> _ops =
        Channel.CreateUnbounded<(Op, string, PlayerTicket?)>();
    private readonly ConcurrentDictionary<string, Match> _playerToMatch = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, string> _ticketToPlayer = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _playerToTicket = new(StringComparer.Ordinal);

    // Prevents duplicate enqueues
    private readonly ConcurrentDictionary<string, byte> _inPool = new(StringComparer.Ordinal);

    private int _matchesCount;
    private readonly IMatchmakingStrategy _strategy = strategy;

    // ===== Public API (controller uses these) =====
    public ValueTask EnqueueAsync(string ticketId, PlayerTicket ticket, CancellationToken ct = default)
         => _ops.Writer.WriteAsync((Op.Add, ticketId, ticket), ct);

    public ValueTask RemoveTicketAsync(string ticketId, CancellationToken ct = default)
         => _ops.Writer.WriteAsync((Op.Remove, ticketId, null), ct);

    public bool TryGetMatchByTicket(string ticketId, out Match? match)
    {
        match = null;
        return _ticketToPlayer.TryGetValue(ticketId, out var playerId)
            && _playerToMatch.TryGetValue(playerId, out match);
    }

    // Helpers for Controllers to use
    public bool TryGetActiveTicketForPlayer(string playerId, out string ticketId)
         => _playerToTicket.TryGetValue(playerId, out ticketId!);

    public bool TryReserveTicket(string ticketId, PlayerTicket t)
    {
        // Reserve atomically: one active ticket per player
        if (!_playerToTicket.TryAdd(t.PlayerId, ticketId))
            return false;
        _ticketToPlayer[ticketId] = t.PlayerId;
        return true;
    }

    // public readonly record struct EngineMetrics(int InPool, int Waiting, int MatchesCount);

    // private int WaitingCount => (_strategy as IQueueCountProvider)?.Count ?? 0;
    // public EngineMetrics GetMetrics()
    //     => new(InPool: _inPool.Count, Waiting: WaitingCount, MatchesCount: Volatile.Read(ref _matchesCount));

    public bool IsInPool(string playerId) => _inPool.ContainsKey(playerId);

    // ===== Background loop =====
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _ops.Reader;

        Console.WriteLine("Looping Engine!");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1) Drain newly enqueued tickets quickly
                while (reader.TryRead(out var cmd))
                {
                    ProcessCommand(cmd);
                }

                // 2) Ask strategy to form matches
                foreach (var m in _strategy.TryMakeMatches(stoppingToken))
                {
                    _playerToMatch[m.PlayerA] = m;
                    _playerToMatch[m.PlayerB] = m;
                    _inPool.TryRemove(m.PlayerA, out _);
                    _inPool.TryRemove(m.PlayerB, out _);
                    Interlocked.Increment(ref _matchesCount);
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
                    var awaited = await reader.ReadAsync(stoppingToken);
                    ProcessCommand(awaited);
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

    private void ProcessCommand((Op op, string ticketId, PlayerTicket? ticket) cmd)
    {
        var (op, ticketId, ticket) = cmd;
        switch (op)
        {
            case Op.Add:
                if (ticket is null)
                    return;
                var t = ticket with { EnqueuedAtUtc = DateTime.UtcNow };

                if (_playerToTicket.TryGetValue(t.PlayerId, out var existingTid))
                {
                    if (!StringComparer.Ordinal.Equals(existingTid, ticketId))
                        return;
                }
                else
                {
                    _ticketToPlayer[ticketId] = t.PlayerId;
                    _playerToTicket[t.PlayerId] = ticketId;
                }

                if (!_playerToMatch.ContainsKey(t.PlayerId) && _inPool.TryAdd(t.PlayerId, 1))
                    _strategy.AddTicket(t);
                break;

            case Op.Remove:
                if (_ticketToPlayer.TryRemove(ticketId, out var pid))
                {
                    _playerToTicket.TryRemove(pid, out _);
                    _inPool.TryRemove(pid, out _);
                    _strategy.RemoveTicket(pid);
                }
                break;
        }
    }
}
