using TicketToRide.Shared;
using TicketToRide.Shared.Net;

namespace TicketToRide.Server;

/// <summary>Các lệnh đi vào hàng đợi của phòng (mục 5.1).</summary>
public abstract record RoomCommand;
public sealed record ClientMessage(Session Session, Message Msg) : RoomCommand;
public sealed record ConnectionLost(string PlayerId) : RoomCommand;
public sealed record ConnectionRestored(string PlayerId) : RoomCommand;
public sealed record MemberLoggedOut(string PlayerId) : RoomCommand;             // người chơi đăng xuất (AUTH_LOGOUT) hoặc token hết hạn
public sealed record TimerFired(int Seq, string PlayerId) : RoomCommand;          // Seq để bỏ qua đồng hồ đã hủy
public sealed record RecoveryConfirm(string PlayerId, bool Accept) : RoomCommand; // TODO (dòng 23+)
