namespace TicketToRide.Shared.Models;

/// <summary>Kiểm tra dữ liệu bản đồ (map.json). Trả về danh sách lỗi; rỗng = hợp lệ.</summary>
public static class MapValidator
{
    static readonly HashSet<string> Colors = new() { "RED", "BLUE", "GREEN", "YELLOW", "BLACK", "WHITE", "ORANGE", "PINK" };
    static readonly Dictionary<int, int> Points = new() { [1] = 1, [2] = 2, [3] = 4, [4] = 7, [5] = 10, [6] = 15 };

    static bool CodeOk(string s) => s.Length > 0 && s.All(c => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_');

    public static int TotalPoints(MapData m) => m.Routes.Sum(r => Points.GetValueOrDefault(r.RouteLength));

    public static List<string> Validate(MapData m)
    {
        var err = new List<string>();
        var cities = m.Cities.GroupBy(c => c.CityCode).ToDictionary(g => g.Key, g => g.First());
        if (cities.Count != m.Cities.Count) err.Add("Trùng cityCode");
        if (cities.Count != 25) err.Add($"Cần 25 thành phố, đang có {cities.Count}");
        if (!cities.ContainsKey("QUANGNGAI")) err.Add("Thiếu Quảng Ngãi");
        foreach (var c in m.Cities)
        {
            if (!CodeOk(c.CityCode)) err.Add($"cityCode sai định dạng: {c.CityCode}");
            if (c.X < 0 || c.X > 1000 || c.Y < 0 || c.Y > 1200) err.Add($"{c.CityCode}: tọa độ ngoài 0..1000 x 0..1200");
        }

        var codes = new HashSet<string>();
        var keys = new HashSet<(string, string, int)>();
        var groups = new Dictionary<(string, string), List<Route>>();
        var adj = cities.Keys.ToDictionary(k => k, _ => new HashSet<string>());
        foreach (var r in m.Routes)
        {
            var rc = r.RouteCode;
            if (!codes.Add(rc)) err.Add($"Trùng mã tuyến {rc}");
            if (!CodeOk(rc)) err.Add($"{rc}: mã tuyến sai định dạng");
            if (!cities.ContainsKey(r.CityACode) || !cities.ContainsKey(r.CityBCode))
            { err.Add($"{rc}: thành phố không tồn tại ({r.CityACode},{r.CityBCode})"); continue; }
            if (string.CompareOrdinal(r.CityACode, r.CityBCode) >= 0) err.Add($"{rc}: phải CityA < CityB");
            if (r.ParallelIndex != 1 && r.ParallelIndex != 2) err.Add($"{rc}: parallelIndex phải là 1 hoặc 2");
            if (r.RouteLength < 1 || r.RouteLength > 6) err.Add($"{rc}: độ dài ngoài 1..6");
            if (r.ColorCode != null && !Colors.Contains(r.ColorCode)) err.Add($"{rc}: màu không hợp lệ '{r.ColorCode}'");
            if (!keys.Add((r.CityACode, r.CityBCode, r.ParallelIndex))) err.Add($"{rc}: trùng (A,B,ParallelIndex)");
            var k = (r.CityACode, r.CityBCode);
            if (!groups.TryGetValue(k, out var list)) groups[k] = list = new List<Route>();
            list.Add(r);
            adj[r.CityACode].Add(r.CityBCode);
            adj[r.CityBCode].Add(r.CityACode);
        }
        foreach (var (k, rs) in groups)
        {
            if (rs.Count > 2) err.Add($"{k.Item1}-{k.Item2}: quá 2 nhánh");
            if (rs.Count == 2)
            {
                if (rs[0].RouteLength != rs[1].RouteLength) err.Add($"{k.Item1}-{k.Item2}: 2 nhánh khác độ dài");
                if (rs[0].ColorCode != null && rs[0].ColorCode == rs[1].ColorCode) err.Add($"{k.Item1}-{k.Item2}: 2 nhánh trùng màu");
            }
        }
        if (cities.Count > 0)
        {
            var start = cities.Keys.First();
            var seen = new HashSet<string> { start };
            var st = new Stack<string>(); st.Push(start);
            while (st.Count > 0)
                foreach (var n in adj[st.Pop()]) if (seen.Add(n)) st.Push(n);
            if (seen.Count != cities.Count)
                err.Add("Đồ thị không liên thông, cô lập: " + string.Join(", ", cities.Keys.Except(seen)));
        }
        return err;
    }
}
