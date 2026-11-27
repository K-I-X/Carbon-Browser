using System.Diagnostics;

namespace Carbon;

internal static class StartupMetrics
{
#if CARBON_QA
 static readonly Stopwatch Clock = Stopwatch.StartNew();
 internal static readonly Dictionary<string, double> Timings = [];
#endif
 [Conditional("CARBON_QA")]
 internal static void Mark(string name)
 {
#if CARBON_QA
  Timings.TryAdd(name, Math.Round(Clock.Elapsed.TotalSeconds, 3));
#endif
 }
}
