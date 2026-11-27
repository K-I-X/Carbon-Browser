using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
namespace Carbon;

/// Carbon AI: the free AI built into Carbon, for everyone, with no plan, no account, no key and no sign-in. It is OpenCode's
/// own program, run on the free models OpenCode offers everyone. Carbon downloads it once from npm (the same package
/// `npm install opencode-ai` installs, checked against npm's sha512 before it's used), keeps it in Carbon's folder with
/// settings of its own (so it never touches an OpenCode the user set up), and only ever runs it on a free model. It works
/// through Carbon's browser tools like every other AI here: it reads pages, clicks, types and fills forms.
static class CarbonAi
{
 public const string Id = "carbonai";
 public const string Name = "Carbon AI";
 const string Registry = "https://registry.npmjs.org/";
 // Until OpenCode lists today's free models: fast ones first, the slower Big Pickle last.
 static readonly string[] Fallbacks = ["opencode/deepseek-v4.1-flash-free", "opencode/mimo-v2.6-flash-free", "opencode/hy4-flash-free", "opencode/nemotron-3.5-lightning-free", "opencode/big-pickle"];
 public static string Home => Path.Combine(App.ProfileHome.Length > 0 ? App.ProfileHome : App.DataRoot, "carbon-ai");
 public static string Exe => Path.Combine(Home, "bin", "opencode.exe");
 static string Package => RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "opencode-windows-arm64" : "opencode-windows-x64-baseline";
 static CarbonAiAdapter? adapter;
 public static CarbonAiAdapter Adapter => adapter ??= new() { Id = Id, Name = Name, App = Name, Command = Exe, Maker = "Carbon", Site = "https://opencode.ai", Installer = Registry };
 public static string Progress { get; private set; } = ""; // "Getting Carbon AI ready… 42%" while it downloads
 public static string Error { get; private set; } = "";
 public static bool Ready => File.Exists(Exe);
 /// How long a free model may stay silent (no words, no step) before the next one answers instead.
 public static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);
 /// How long OpenCode may take to start talking to a model at all (a step starting); longer means it's stuck before the model.
 public static readonly TimeSpan Start = TimeSpan.FromSeconds(25);
 /// The free models OpenCode listed last, best first. Runs use the first unless the user picked another free one.
 public static List<string> Free { get; set; } = [];
 /// Free models that failed or went quiet lately: skipped for 10 minutes, so the next message goes to one that answers.
 static readonly Dictionary<string, DateTime> failed = [];
 static readonly Dictionary<string, string> used = []; // run folder → the model its run asked for
 public static void Failed(string model) { lock (failed) failed[model] = DateTime.UtcNow; }
 public static bool Resting(string model) { lock (failed) return failed.TryGetValue(model, out var at) && DateTime.UtcNow - at < TimeSpan.FromMinutes(10); }
 public static string? Used(string folder) { lock (used) return used.GetValueOrDefault(folder); }
 internal static void Using(string folder, string model) { lock (used) used[folder] = model; }
 public static string Best => (Free.Count > 0 ? Free : [.. Fallbacks]).FirstOrDefault(m => !Resting(m)) ?? (Free.Count > 0 ? Free[0] : Fallbacks[0]);
 /// Each model's thinking levels (OpenCode's variants), lowest first: Carbon AI thinks as little as it can, to answer fast.
 public static readonly Dictionary<string, string[]> Efforts = [];
 public static string Quick(string model) => Efforts.GetValueOrDefault(model, []).FirstOrDefault(v => v is "none" or "minimal" or "low") ?? "";
 public static bool IsFree(string model) => model.StartsWith("opencode/", StringComparison.Ordinal) && (model.EndsWith("-free", StringComparison.Ordinal) || model.Contains("big-pickle", StringComparison.Ordinal));
 /// Better at tools and quicker first: these name fragments go to the front, in this order.
 static readonly string[] Prefer = ["deepseek-v4.1-flash", "flash", "lightning", "deepseek", "mimo", "hy4", "nemotron", "kimi", "glm", "minimax", "qwen", "big-pickle"];
 public static int Rank(string id) { for (int i = 0; i < Prefer.Length; i++) if (id.Contains(Prefer[i], StringComparison.OrdinalIgnoreCase)) return i; return Prefer.Length; }

 /// Its own settings, data and cache in Carbon's folder: never the user's OpenCode, and never an update it didn't ask for.
 public static IReadOnlyDictionary<string, string> Env => new Dictionary<string, string> {
  ["XDG_CONFIG_HOME"] = Path.Combine(Home, "config"), ["XDG_DATA_HOME"] = Path.Combine(Home, "data"),
  ["XDG_CACHE_HOME"] = Path.Combine(Home, "cache"), ["XDG_STATE_HOME"] = Path.Combine(Home, "state"),
  ["OPENCODE_DISABLE_AUTOUPDATE"] = "1", ["OPENCODE_DISABLE_LSP_DOWNLOAD"] = "1", ["OPENCODE_DISABLE_FILEWATCHER"] = "1", ["OPENCODE_DISABLE_CLAUDE_CODE"] = "1",
 };

 static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };
 static (DateTime At, string? Why) probed;
 /// Why Carbon AI can't answer from this network right now, or null when it can: its free models live at opencode.ai, which
 /// some school and work networks block. Any answer from there counts; checked at most every 2 minutes, in 6 seconds at most.
 public static async Task<string?> Problem()
 {
  if (DateTime.UtcNow - probed.At < TimeSpan.FromMinutes(2)) return probed.Why;
  string? why = null;
  try {
   using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
   using var res = await Http.GetAsync("https://opencode.ai/zen/v1/models", HttpCompletionOption.ResponseHeadersRead, cts.Token);
  } catch (OperationCanceledException) { why = "opencode.ai, where its free models are, didn't answer. This network may block it"; }
  catch (HttpRequestException) { why = "it can't reach opencode.ai, where its free models are. This network may block it, or you're offline"; }
  probed = (DateTime.UtcNow, why);
  return why;
 }
 public static void Forget() => probed = default; // the next message checks again
 static Task<bool>? installing;
 /// Downloads and checks OpenCode's program once (about 60 MB). Says how far it got through changed. True when it's ready.
 public static Task<bool> Install(Action changed) => installing ??= Task.Run(async () => {
  try { return await Download(changed); }
  finally { installing = null; Progress = ""; changed(); }
 });
 static async Task<bool> Download(Action changed)
 {
  Error = ""; Progress = "Getting Carbon AI ready…"; changed();
  string part = Path.Combine(Home, "download.tgz.part");
  try {
   Directory.CreateDirectory(Path.Combine(Home, "bin"));
   using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
   var meta = JsonNode.Parse(await Http.GetStringAsync(Registry + Package + "/latest", timeout.Token));
   string tarball = meta?["dist"]?["tarball"]?.GetValue<string>() ?? "", integrity = meta?["dist"]?["integrity"]?.GetValue<string>() ?? "", version = meta?["version"]?.GetValue<string>() ?? "";
   // Only npm's own registry, and only with npm's sha512 to check it against.
   if (!tarball.StartsWith(Registry, StringComparison.Ordinal) || !integrity.StartsWith("sha512-", StringComparison.Ordinal)) throw new InvalidDataException("npm didn't say where the program is or how to check it");
   using (var res = await Http.GetAsync(tarball, HttpCompletionOption.ResponseHeadersRead, timeout.Token)) {
    res.EnsureSuccessStatusCode();
    long total = res.Content.Headers.ContentLength ?? 0, got = 0; int shown = -1;
    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
    await using (var input = await res.Content.ReadAsStreamAsync(timeout.Token))
    await using (var file = File.Create(part)) {
     var buffer = new byte[1 << 16];
     for (int n; (n = await input.ReadAsync(buffer, timeout.Token)) > 0;) {
      await file.WriteAsync(buffer.AsMemory(0, n), timeout.Token); hash.AppendData(buffer, 0, n); got += n;
      int pct = total > 0 ? (int)(got * 100 / total) : -1;
      if (pct != shown) { shown = pct; Progress = pct >= 0 ? $"Getting Carbon AI ready… {pct}%" : $"Getting Carbon AI ready… {got >> 20} MB"; changed(); }
     }
    }
    if (Convert.ToBase64String(hash.GetHashAndReset()) != integrity["sha512-".Length..]) throw new InvalidDataException("the download didn't match npm's checksum, so Carbon didn't use it");
   }
   Progress = "Getting Carbon AI ready… unpacking"; changed();
   bool found = false;
   await using (var gz = new GZipStream(File.OpenRead(part), CompressionMode.Decompress))
   using (var tar = new TarReader(gz)) {
    while (tar.GetNextEntry() is { } entry) {
     if (entry.Name.TrimStart('.', '/') is not ("package/bin/opencode.exe" or "package/bin/opencode") || entry.DataStream == null) continue;
     await using (var exe = File.Create(Exe + ".part")) await entry.DataStream.CopyToAsync(exe, timeout.Token);
     found = true; break;
    }
   }
   if (!found) throw new InvalidDataException("the program wasn't in the download");
   File.Move(Exe + ".part", Exe, true);
   await File.WriteAllTextAsync(Path.Combine(Home, "version.txt"), version);
   return true;
  } catch (Exception e) when (e is HttpRequestException or IOException or InvalidDataException or OperationCanceledException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException) {
   App.Log(e);
   Error = e is HttpRequestException or OperationCanceledException ? "Carbon AI couldn't download. Check your internet connection, then try again." : "Carbon AI couldn't be set up: " + e.Message + ".";
   return false;
  } finally {
   try { File.Delete(part); } catch (IOException) { }
  }
 }
}

/// Carbon AI as one of the AIs: OpenCode's program in Carbon's folder, on free models only.
sealed class CarbonAiAdapter : OpenCodeAdapter
{
 public override string[] Gaps => [];
 public override IReadOnlyDictionary<string, string> Env => CarbonAi.Env;
 public override Task<bool?> Detect(string exe) => Task.FromResult<bool?>(File.Exists(exe));
 /// Only OpenCode's free models, best first, under "Automatic", which picks the best free one.
 public override async Task<AgentModel[]> Models(string exe)
 {
  var all = await base.Models(exe);
  var free = all.Where(m => CarbonAi.IsFree(m.Id)).OrderBy(m => CarbonAi.Rank(m.Id)).ThenBy(m => m.Id, StringComparer.Ordinal).ToList();
  if (free.Count > 0) CarbonAi.Free = [.. free.Select(m => m.Id)];
  foreach (var m in free) CarbonAi.Efforts[m.Id] = m.Efforts;
  return [new("", "Automatic (best free model)", []), .. free.Select(m => m with { Name = m.Name.Replace(" · opencode", "") + " · free" })];
 }
 /// Through Carbon AI's server when it's running (fast, streamed); otherwise OpenCode once per message, as before.
 public override async Task<AgentRun> Start(string exe, RunRequest r)
 {
  var args = await Args(exe, r); // writes the chat folder's settings (Carbon's tools) and picks the model
  if (await CarbonAiServer.Ensure() != null) {
   string Arg(string flag) => args.IndexOf(flag) is var i and >= 0 && i + 1 < args.Count ? args[i + 1] : "";
   return ServerRun.Begin(this, r, Arg("-m") is { Length: > 0 } m ? m : CarbonAi.Best, Arg("--variant"));
  }
  return await base.Start(exe, r);
 }
 /// Never anything but a free model: no model, one that isn't free or one that just failed runs on the best free one.
 /// With no effort picked, it thinks as little as the model allows, so replies come fast.
 public override async Task<List<string>> Args(string exe, RunRequest r)
 {
  string model = CarbonAi.IsFree(r.Model) && !CarbonAi.Resting(r.Model) ? r.Model : CarbonAi.Best;
  string effort = r.Effort.Length > 0 && model == r.Model ? r.Effort : CarbonAi.Quick(model);
  CarbonAi.Using(r.Folder, model);
  var args = await base.Args(exe, r with { Model = model, Effort = effort });
  args.Add("--thinking"); // says when it's thinking, so the chat shows it working before the words come
  return args;
 }
}
