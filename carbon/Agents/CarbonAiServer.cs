using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Channels;
namespace Carbon;

/// Carbon AI's fast path: one OpenCode server (`opencode serve`) kept running in the background, instead of starting
/// OpenCode for every message. Replies stream in word by word as the model writes them, and nothing starts up per message
/// (OpenCode, its settings, Carbon's tools: all already running). Local only (127.0.0.1, a random port), with a password
/// made fresh each start, inside a Job Object so it ends with Carbon. If it can't start, Carbon AI runs OpenCode once per
/// message as before. Built from OpenCode 1.19.6's server: /session, /session/:id/prompt_async, /event and /permission.
static partial class CarbonAiServer
{
 static readonly HttpClient Http = new(new SocketsHttpHandler { UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(5) }) { Timeout = Timeout.InfiniteTimeSpan };
 static Process? process;
 static CliRun.Job? job;
 static Uri? root;
 static string auth = "";
 static Task<Uri?>? starting;
 static int failures; // starts that failed this session: after two, Carbon AI stays on the one-run-per-message way
 internal static readonly List<string> Log = [];
 static void Keep(string line) { lock (Log) { Log.Add(line); if (Log.Count > 60) Log.RemoveAt(0); } }

 /// The server's address once it's listening, starting it if needed; null when it can't run (then Carbon AI runs per message).
 public static Task<Uri?> Ensure()
 {
  if (root != null && process is { HasExited: false }) return Task.FromResult<Uri?>(root);
  if (failures >= 2 || !CarbonAi.Ready) return Task.FromResult<Uri?>(null);
  lock (Log) return starting ??= Task.Run(Launch);
 }
 /// Something went wrong talking to it: the next message starts a fresh one.
 public static void Broken() { try { job?.Kill(); } catch (Exception) { } root = null; process = null; }

 [GeneratedRegex(@"https?://[\w.\-]+:\d+")] private static partial Regex Address();
 static async Task<Uri?> Launch()
 {
  try {
   job?.Dispose();
   string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
   var env = new Dictionary<string, string>(CarbonAi.Env) { ["OPENCODE_SERVER_PASSWORD"] = password };
   Directory.CreateDirectory(CarbonAi.Home);
   var start = AgentAdapter.Launch(CarbonAi.Exe, CarbonAi.Home, ["serve", "--hostname", "127.0.0.1", "--port", "0"], env);
   start.RedirectStandardOutput = start.RedirectStandardError = start.RedirectStandardInput = true;
   start.StandardOutputEncoding = start.StandardErrorEncoding = new UTF8Encoding(false);
   var p = Process.Start(start) ?? throw new InvalidOperationException("OpenCode didn't start");
   job = new CliRun.Job(); job.Add(p); process = p;
   var listening = new TaskCompletionSource<Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);
   _ = Task.Run(async () => {
    while (await p.StandardOutput.ReadLineAsync() is { } line) { Keep(line); if (Address().Match(line) is { Success: true } m) listening.TrySetResult(new Uri(m.Value)); }
    listening.TrySetResult(null);
   });
   _ = Task.Run(async () => { while (await p.StandardError.ReadLineAsync() is { } line) Keep(line); });
   var at = await listening.Task.WaitAsync(TimeSpan.FromSeconds(45));
   if (at == null || p.HasExited) throw new InvalidOperationException("OpenCode's server didn't start");
   auth = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("opencode:" + password));
   root = at; failures = 0;
   return at;
  } catch (Exception e) when (e is IOException or InvalidOperationException or TimeoutException or System.ComponentModel.Win32Exception) {
   App.Log(e); Keep("server: " + e.Message); failures++; Broken();
   return null;
  } finally { starting = null; }
 }

 // ---------- HTTP ----------
 static Uri At(string path, string folder) => new(root! + path.TrimStart('/') + (path.Contains('?') ? "&" : "?") + "directory=" + Uri.EscapeDataString(folder));
 static HttpRequestMessage Request(HttpMethod method, string path, string folder, JsonNode? body = null)
 {
  var req = new HttpRequestMessage(method, At(path, folder));
  req.Headers.TryAddWithoutValidation("Authorization", auth);
  if (body != null) req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
  return req;
 }
 internal static async Task<JsonNode?> Send(HttpMethod method, string path, string folder, JsonNode? body, CancellationToken token)
 {
  using var req = Request(method, path, folder, body);
  using var res = await Http.SendAsync(req, token);
  string text = await res.Content.ReadAsStringAsync(token);
  if (res.StatusCode == HttpStatusCode.NotFound && path.StartsWith("/session/", StringComparison.Ordinal)) throw new ServerFailure("Session not found");
  if (!res.IsSuccessStatusCode) throw new ServerFailure($"OpenCode answered {(int)res.StatusCode}: {(text.Length > 300 ? text[..300] : text)}");
  return text.Trim().Length > 0 && (text.TrimStart().StartsWith('{') || text.TrimStart().StartsWith('[')) ? JsonNode.Parse(text) : null;
 }
 /// The folder's events, as they happen: one JSON object per "data:" line.
 internal static async Task<StreamReader> Events(string folder, CancellationToken token)
 {
  var req = Request(HttpMethod.Get, "/event", folder);
  req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
  var res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);
  if (!res.IsSuccessStatusCode) { res.Dispose(); throw new ServerFailure($"OpenCode's events answered {(int)res.StatusCode}"); }
  return new StreamReader(await res.Content.ReadAsStreamAsync(token), Encoding.UTF8);
 }
}

sealed class ServerFailure(string message) : Exception(message);

/// One Carbon AI reply through the server: a session for the chat (the same one OpenCode keeps on disk, so chats carry on
/// across both ways of running), the message, and the reply streamed back as events.
public sealed class ServerRun : AgentRun
{
 readonly Channel<AgentEvent> events = Channel.CreateUnbounded<AgentEvent>();
 readonly CancellationTokenSource stop = new();
 readonly RunRequest r;
 readonly string model, variant;
 string? session;
 int exit;
 public override int ExitCode => exit;

 ServerRun(AgentAdapter a, RunRequest r, string model, string variant) : base(a) { this.r = r; this.model = model; this.variant = variant; }
 internal static ServerRun Begin(AgentAdapter a, RunRequest r, string model, string variant)
 {
  var run = new ServerRun(a, r, model, variant);
  Interlocked.Increment(ref Launched);
  _ = Task.Run(run.Loop);
  return run;
 }
 public override async IAsyncEnumerable<AgentEvent> Events([EnumeratorCancellation] CancellationToken token = default)
 {
  await foreach (var e in events.Reader.ReadAllAsync(token)) yield return e;
 }
 void Say(AgentEvent e) => events.Writer.TryWrite(e);
 public override void Cancel()
 {
  Cancelled = true;
  if (session is { } id) _ = Task.Run(async () => { try { await CarbonAiServer.Send(HttpMethod.Post, $"/session/{id}/abort", r.Folder, new JsonObject(), CancellationToken.None); } catch (Exception) { } });
  stop.Cancel();
 }
 public override void Dispose() { if (!stop.IsCancellationRequested) stop.Cancel(); }

 static string? Str(JsonNode? n) { try { return n?.GetValue<string>(); } catch (InvalidOperationException) { return null; } }
 static long Long(JsonNode? n) { try { return n?.GetValue<long>() ?? 0; } catch (InvalidOperationException) { return 0; } }

 async Task Loop()
 {
  using var limit = new Timer(_ => { TimedOut = true; Cancel(); }, null, Limit, Timeout.InfiniteTimeSpan);
  var token = stop.Token;
  try {
   // Settings first (the chat folder's opencode.json, with Carbon's tools), then the session: an earlier one, or a new one.
   if (r.Resume && r.Session is { Length: > 0 } earlier) session = earlier;
   else {
    var rules = new JsonArray(Rule("question"), Rule("plan_enter"), Rule("plan_exit")); // nothing that waits on a person: Carbon asks the user itself
    session = Str((await CarbonAiServer.Send(HttpMethod.Post, "/session", r.Folder, new JsonObject { ["title"] = "Carbon", ["permission"] = rules }, token))?["id"])
     ?? throw new ServerFailure("OpenCode didn't make a session");
   }
   using var reader = await CarbonAiServer.Events(r.Folder, token);
   int slash = model.IndexOf('/');
   var body = new JsonObject {
    ["model"] = new JsonObject { ["providerID"] = slash > 0 ? model[..slash] : "opencode", ["modelID"] = slash > 0 ? model[(slash + 1)..] : model },
    ["parts"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = r.Prompt }),
   };
   if (variant.Length > 0) body["variant"] = variant;
   await CarbonAiServer.Send(HttpMethod.Post, $"/session/{session}/prompt_async", r.Folder, body, token);
   await Read(reader, token);
  } catch (OperationCanceledException) when (token.IsCancellationRequested) { }
  catch (Exception e) when (e is ServerFailure or HttpRequestException or IOException or System.Text.Json.JsonException or InvalidOperationException) {
   exit = 1; Keep(e.Message);
   if (e is HttpRequestException or IOException) CarbonAiServer.Broken(); // the server itself is gone: the next message starts it again
   Say(new AgentEvent.Error(e.Message));
  } finally {
   if (session != null && !said) Say(new AgentEvent.Session(session));
   events.Writer.TryComplete();
  }
 }
 static JsonObject Rule(string permission) => new() { ["permission"] = permission, ["action"] = "deny", ["pattern"] = "*" };

 bool said; // the session id went to the chat (when the model starts, so a stuck start stays visible as one)
 async Task Read(StreamReader reader, CancellationToken token)
 {
  var roles = new Dictionary<string, string>();      // message id → user or assistant
  var kinds = new Dictionary<string, string>();      // part id → text, reasoning, tool…
  var streaming = new HashSet<string>();             // text parts that came word by word
  var thought = new HashSet<string>();               // reasoning parts already shown as thinking
  var called = new HashSet<string>(); var done = new HashSet<string>(); // tool calls shown, and finished
  bool busy = false;
  while (await reader.ReadLineAsync(token) is { } line) {
   if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
   JsonNode? e; try { e = JsonNode.Parse(line[5..].Trim()); } catch (System.Text.Json.JsonException) { continue; }
   string type = Str(e?["type"]) ?? ""; var p = e?["properties"];
   string? sid = Str(p?["sessionID"]) ?? Str(p?["part"]?["sessionID"]) ?? Str(p?["info"]?["sessionID"]);
   if (sid != null && sid != session) continue;
   switch (type) {
    case "message.updated":
     if (Str(p?["info"]?["id"]) is { } mid && Str(p?["info"]?["role"]) is { } role) roles[mid] = role;
     break;
    case "message.part.updated": {
     var part = p?["part"]; string id = Str(part?["id"]) ?? "", kind = Str(part?["type"]) ?? "", msg = Str(part?["messageID"]) ?? "";
     kinds[id] = kind;
     if (roles.GetValueOrDefault(msg) == "user") break;
     if (roles.GetValueOrDefault(msg) == "assistant") busy = true;
     switch (kind) {
      case "step-start": if (!said && session != null) { said = true; Say(new AgentEvent.Session(session)); } break;
      case "reasoning": if (thought.Add(id)) Say(new AgentEvent.Thinking()); break;
      case "text":
       // Usually it came word by word already; a text that arrives whole (or only at the end) is said here, once.
       if (!streaming.Contains(id) && part?["time"]?["end"] != null && roles.GetValueOrDefault(msg) == "assistant" && Str(part?["text"]) is { Length: > 0 } whole) { streaming.Add(id); Say(new AgentEvent.Message(whole)); }
       break;
      case "tool": {
       string call = Str(part?["callID"]) ?? id, status = Str(part?["state"]?["status"]) ?? "";
       if (status is "running" or "completed" or "error" && called.Add(call)) {
        string tool = Str(part?["tool"]) ?? "";
        Say(new AgentEvent.ToolCall(call, tool.StartsWith("carbon_", StringComparison.Ordinal) ? tool[7..] : tool, part?["state"]?["input"]?.DeepClone()));
       }
       if (status is "completed" or "error" && done.Add(call)) Say(new AgentEvent.ToolResult(call, status == "completed"));
       break;
      }
      case "step-finish": {
       var t = part?["tokens"];
       long input = Long(t?["input"]), output = Long(t?["output"]), cached = Long(t?["cache"]?["read"]);
       if (input + output > 0) Say(new AgentEvent.Usage($"Last reply: {AgentStream.Tokens(input + cached)} in ({AgentStream.Tokens(cached)} cached), {AgentStream.Tokens(output)} out"));
       break;
      }
     }
     break;
    }
    case "message.part.delta": {
     if (Str(p?["field"]) != "text" || Str(p?["delta"]) is not { Length: > 0 } delta) break;
     string id = Str(p?["partID"]) ?? "", msg = Str(p?["messageID"]) ?? "";
     if (roles.GetValueOrDefault(msg) == "user") break;
     string kind = kinds.GetValueOrDefault(id, "");
     if (kind == "reasoning") { if (thought.Add(id)) Say(new AgentEvent.Thinking()); break; }
     if (kind != "text") break;
     busy = true;
     if (!said && session != null) { said = true; Say(new AgentEvent.Session(session)); }
     Say(new AgentEvent.Message(delta, streaming.Add(id) ? null : "")); // the first words start the reply, the rest join it
     break;
    }
    case "permission.asked" when Str(p?["id"]) is { } ask:
     // OpenCode's own approval for one of Carbon's tools: Carbon's tools ask the user themselves where it matters.
     _ = CarbonAiServer.Send(HttpMethod.Post, $"/permission/{ask}/reply", r.Folder, new JsonObject { ["reply"] = "once" }, token).ContinueWith(t => { if (t.Exception != null) Keep(t.Exception.InnerException?.Message ?? ""); }, TaskScheduler.Default);
     break;
    case "session.error": {
     string message = Str(p?["error"]?["data"]?["message"]) ?? Str(p?["error"]?["name"]) ?? "failed";
     exit = 1; Keep(message); Say(new AgentEvent.Error(message));
     break;
    }
    case "session.status": {
     string state = Str(p?["status"]?["type"]) ?? "";
     if (state is "busy" or "retry") busy = true;
     if (state == "retry" && Str(p?["status"]?["message"]) is { Length: > 0 } why) {
      Keep("retry: " + why);
      // OpenCode's free allowance is used up: it would keep retrying for minutes, so the reply stops here and says so.
      if (CarbonAi.FreeLimit(why)) { exit = 1; Say(new AgentEvent.Error(CarbonAi.FreeLimitMessage)); Cancel(); Cancelled = false; return; }
     }
     if (state == "idle" && busy) return; // the reply is done
     break;
    }
    case "session.idle": if (busy) return; break;
    case "server.instance.disposed": throw new ServerFailure("OpenCode closed this chat's workspace");
   }
  }
  if (!token.IsCancellationRequested) throw new IOException("OpenCode's server stopped");
 }
}
