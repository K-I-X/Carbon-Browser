namespace Carbon;

// Signal Run, the offline page's game (ErrorPages.cs, Assets/offline-game.js): what the page reports when a run ends.
public partial class MainWindow
{
 void SaveOfflineGame(string json)
 {
  try {
   using var doc = System.Text.Json.JsonDocument.Parse(json);
   var r = doc.RootElement; var g = Prefs.OfflineGame;
   int N(string k, int max) => r.TryGetProperty(k, out var v) && v.TryGetInt32(out int n) ? Math.Clamp(n, 0, max) : 0;
   g.Best = Math.Max(g.Best, N("best", 50_000_000)); g.BestDistance = Math.Max(g.BestDistance, N("bestDist", 500_000_000));
   g.Electrons = Math.Max(g.Electrons, N("electrons", 50_000_000)); g.Skin = N("skin", 6);
   g.Mute = r.TryGetProperty("mute", out var m) && m.ValueKind == System.Text.Json.JsonValueKind.True;
   if (r.TryGetProperty("day", out var d) && d.GetString() is { Length: 10 } day) g.Day = day;
   if (r.TryGetProperty("done", out var done) && done.ValueKind == System.Text.Json.JsonValueKind.Array)
    g.Done = [.. done.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length is > 0 and < 12).Take(10)];
   SaveLater();
  } catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException) { }
 }
}
