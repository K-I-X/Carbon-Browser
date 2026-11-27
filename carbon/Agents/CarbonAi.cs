using System.Reflection;
namespace Carbon;

/// Carbon AI: the free AI built into Carbon, with no account, no key and no sign-in. It talks to Carbon AI's own server
/// (server/worker.js, a Cloudflare Worker), which holds the AI keys and answers in the OpenAI Chat Completions format.
/// Carbon runs it with its own agent loop (ApiRun), the one API providers use: replies stream as they're written, and it
/// reads pages, clicks, types and fills forms through Carbon's tools like every other AI here.
/// The server's address comes with the build (CarbonAiUrl, from the repository's CARBON_AI_URL variable); the
/// CARBON_AI_URL environment variable overrides it, for trying a server out.
static class CarbonAi
{
 public const string Id = "carbonai";
 public const string Name = "Carbon AI";
 public const string Token = "carbon"; // not a secret: the server holds the keys; this only tells it the request is Carbon's
 public static string Url { get; } = Pick(Environment.GetEnvironmentVariable("CARBON_AI_URL"))
  ?? Pick(Assembly.GetEntryAssembly()?.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "CarbonAiUrl")?.Value) ?? "";
 static string? Pick(string? url) => Uri.TryCreate(url?.Trim().TrimEnd('/'), UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps ? u.AbsoluteUri.TrimEnd('/') : null;
 /// Connected to its server: a Carbon built with the server's address.
 public static bool Ready => Url.Length > 0;
 public const string NotConnected = "Carbon AI isn't connected to its server yet. Set up the server (server/README.md in Carbon's repository) and put its address in the repository's CARBON_AI_URL variable.";

 public static readonly ApiProvider Provider = new() {
  Id = Id, Kind = "openai", Name = Name, Url = Ready ? Url + "/v1" : "", Context = 128_000, BuiltIn = true,
  Models = [new ApiModel { Id = "carbon" }],
 };
 static CarbonAiAdapter? adapter;
 public static CarbonAiAdapter Adapter => adapter ??= new() { Provider = Provider, Id = Id, Name = Name, App = Name, Command = "", Maker = "Carbon" };
}

/// Carbon AI as one of the AIs: Carbon's own server, run like an API provider with nothing to add.
sealed class CarbonAiAdapter : ApiAdapter
{
 public override string[] Gaps => [];
 public override Task<bool?> Detect(string exe) => Task.FromResult<bool?>(CarbonAi.Ready);
 public override Task<AgentModel[]> Models(string exe) => Task.FromResult<AgentModel[]>([new("", "Carbon AI", [])]);
}
