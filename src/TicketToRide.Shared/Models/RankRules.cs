using System;
using System.Collections.Generic;
using System.Text;

namespace TicketToRide.Shared.Models;

/// <summary>
/// Quy tắc xác định bậc Rank của người chơi.
/// Điểm Rank tối thiểu là 0, không giới hạn điểm tối đa.
/// </summary>
public static class RankRules
{
    public sealed record RankTier(int MinPoints, string Name);

    public static readonly IReadOnlyList<RankTier> Tiers =
        Array.AsReadOnly(new[]
        {
            new RankTier(0,     "Học việc đường ray"),
            new RankTier(300,   "Phụ toa"),
            new RankTier(600,   "Soát vé"),
            new RankTier(1000,  "Trưởng toa"),
            new RankTier(1500,  "Phụ lái"),
            new RankTier(2000,  "Lái tàu"),
            new RankTier(3000,  "Trưởng tàu"),
            new RankTier(4000,  "Tổng điều độ"),
            new RankTier(5000,  "Huyền thoại đường ray"),
            new RankTier(10000, "Vua tàu lửa")
        });

    /// <summary>
    /// Trả về bậc Rank tương ứng với điểm hiện tại.
    /// </summary>
    public static RankTier GetTier(int rankPoints)
    {
        if (rankPoints < 0)
            rankPoints = 0;

        for (int i = Tiers.Count - 1; i >= 0; i--)
        {
            if (rankPoints >= Tiers[i].MinPoints)
                return Tiers[i];
        }

        return Tiers[0];
    }

    /// <summary>
    /// Trả về tên bậc Rank.
    /// </summary>
    public static string GetTierName(int rankPoints)
    {
        return GetTier(rankPoints).Name;
    }

    /// <summary>
    /// Trả về điểm tối thiểu để đạt bậc Rank hiện tại.
    /// </summary>
    public static int GetTierMinPoints(int rankPoints)
    {
        return GetTier(rankPoints).MinPoints;
    }

    /// <summary>
    /// Trả về điểm tối thiểu của bậc Rank kế tiếp.
    /// Nếu đã đạt bậc cao nhất thì trả về null.
    /// </summary>
    public static int? GetNextTierMinPoints(int rankPoints)
    {
        var currentTier = GetTier(rankPoints);

        for (int i = 0; i < Tiers.Count - 1; i++)
        {
            if (Tiers[i].MinPoints == currentTier.MinPoints)
                return Tiers[i + 1].MinPoints;
        }

        return null;
    }
}
