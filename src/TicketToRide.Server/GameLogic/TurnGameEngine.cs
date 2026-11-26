using TicketToRide.Shared;
using TicketToRide.Shared.Net;

namespace TicketToRide.Server;

/// <summary>
/// Bộ máy lượt cơ bản cho Giai đoạn 5 khi chưa có bộ luật đầy đủ từ nhánh GameLogic:
/// xác thực lượt, cập nhật StateVersion, chuyển lượt và sinh snapshot để lưu DB/broadcast.
/// </summary>
public sealed class TurnGameEngine : IGameEngine
{
    readonly object _sync = new();
    int _turnIndex;
    int _version;
    string _currentTurnPlayerId = "";

    public int StateVersion => Volatile.Read(ref _version);
    public string CurrentTurnPlayerId => _currentTurnPlayerId;
    public bool IsSetupTicketsPhase => false;

    public void EnsureInitialized(IReadOnlyList<Member> members)
    {
        lock (_sync)
        {
            if (string.IsNullOrEmpty(_currentTurnPlayerId) && members.Count > 0)
            {
                _turnIndex = 0;
                _currentTurnPlayerId = members[0].Session.PlayerId;
            }
        }
    }

    public object CreateSnapshot()
    {
        lock (_sync)
        {
            return new
            {
                stateVersion = _version,
                currentTurnPlayerId = _currentTurnPlayerId,
                updatedAtUtc = DateTime.UtcNow,
            };
        }
    }

    public void AdvanceTurnSkippingLeftMatch(IReadOnlyList<Member> members)
    {
        lock (_sync)
        {
            if (members.Count == 0) return;
            for (int offset = 1; offset <= members.Count; offset++)
            {
                var idx = (_turnIndex + offset) % members.Count;
                if (members[idx].Session.State != ConnState.LeftMatch)
                {
                    _turnIndex = idx;
                    _currentTurnPlayerId = members[idx].Session.PlayerId;
                    _version++;
                    return;
                }
            }
        }
    }

    public (bool Ok, string ErrorCode, string ErrorText, object? StateDelta, bool GameEnded) ApplyAction(
        string playerId, Message msg, IReadOnlyList<Member> members)
    {
        lock (_sync)
        {
            EnsureInitialized(members);

            if (msg.Type is "CLAIM_ROUTE" or "DRAW_CARD" or "DRAW_TICKETS" or "KEEP_TICKETS")
            {
                if (_currentTurnPlayerId != playerId)
                    return (false, "NOT_YOUR_TURN", "Chưa tới lượt của bạn", null, false);
            }

            _version++;
            AdvanceTurnSkippingLeftMatchInternal(members);

            var delta = new
            {
                stateVersion = _version,
                actorPlayerId = playerId,
                actionType = msg.Type,
                nextTurnPlayerId = _currentTurnPlayerId,
            };

            return (true, "", "", delta, false);
        }
    }

    void AdvanceTurnSkippingLeftMatchInternal(IReadOnlyList<Member> members)
    {
        if (members.Count == 0) return;
        for (int offset = 1; offset <= members.Count; offset++)
        {
            var idx = (_turnIndex + offset) % members.Count;
            if (members[idx].Session.State != ConnState.LeftMatch)
            {
                _turnIndex = idx;
                _currentTurnPlayerId = members[idx].Session.PlayerId;
                return;
            }
        }
    }
}
