using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Web.WebView2.Core;
namespace Carbon;

// Full-page error screens shown in the tab when a page can't load, one per cause.
internal static class ErrorPages
{
 record Kind(string Icon, string Title, string Body, bool Search = false);

 // Feather-style line icons (inline, no network).
 const string Wifi = "<path d='M1 1l22 22'/><path d='M16.7 11.1A11 11 0 0 1 19 12.6'/><path d='M5 12.6a11 11 0 0 1 5.2-2.5'/><path d='M10.7 5A16 16 0 0 1 22.6 9'/><path d='M1.4 9a16 16 0 0 1 4.3-2.8'/><path d='M8.5 16.1a6 6 0 0 1 7 0'/><circle cx='12' cy='20' r='1'/>";
 const string Search = "<circle cx='11' cy='11' r='7'/><path d='M21 21l-4.3-4.3'/><path d='M8.5 11h5'/>";
 const string Plug = "<path d='M9 2v6'/><path d='M15 2v6'/><path d='M6 8h12v3a6 6 0 0 1-12 0z'/><path d='M12 17v5'/>";
 const string Clock = "<circle cx='12' cy='12' r='10'/><path d='M12 6v6l4 2'/>";
 const string Lock = "<rect x='4' y='11' width='16' height='10' rx='2'/><path d='M8 11V7a4 4 0 0 1 8 0v4'/><path d='M12 15v2'/>";
 const string Cycle = "<path d='M21 12a9 9 0 1 1-3-6.7L21 8'/><path d='M21 3v5h-5'/>";
 const string Key = "<circle cx='7.5' cy='15.5' r='4.5'/><path d='M10.7 12.3L21 2'/><path d='M16 7l3 3'/>";
 const string Alert = "<path d='M10.3 3.9L1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z'/><path d='M12 9v4'/><path d='M12 17h.01'/>";
 const string Broken = "<path d='M18.4 5.6a4 4 0 0 0-5.7 0l-1.4 1.4'/><path d='M5.6 18.4a4 4 0 0 0 5.7 0l1.4-1.4'/><path d='M8 16l-2 2'/><path d='M16 8l2-2'/><path d='M3 3l18 18'/>";

 static Kind Pick(CoreWebView2WebErrorStatus status, bool certificate)
 {
  if (certificate) return new(Lock, "Your connection isn't private", "This site's security certificate couldn't be verified, so Carbon stopped the connection to protect your data. Someone may be trying to impersonate the site, or its certificate has expired.");
  // Most "can't connect" failures while the PC has no network are really "you're offline".
  if (!NetworkInterface.GetIsNetworkAvailable() || status == CoreWebView2WebErrorStatus.Disconnected)
   return new(Wifi, "You're offline", "Carbon can't reach the internet. Check your Wi-Fi or cable, then try again.");
  return status switch {
   CoreWebView2WebErrorStatus.HostNameNotResolved => new(Search, "Site not found", "This address doesn't seem to exist. Check the spelling, or search for it instead.", true),
   CoreWebView2WebErrorStatus.CannotConnect or CoreWebView2WebErrorStatus.ServerUnreachable => new(Plug, "Can't reach this site", "The site refused to connect or isn't responding right now. It may be down, or blocked by a firewall."),
   CoreWebView2WebErrorStatus.ConnectionAborted or CoreWebView2WebErrorStatus.ConnectionReset => new(Broken, "The connection was lost", "The site closed the connection before the page finished loading."),
   CoreWebView2WebErrorStatus.Timeout => new(Clock, "This site took too long", "The site didn't answer in time. It may be busy, or your connection may be slow."),
   CoreWebView2WebErrorStatus.RedirectFailed => new(Cycle, "Too many redirects", "This page keeps redirecting in a loop. Clearing this site's cookies in Menu → Cookies often fixes it."),
   CoreWebView2WebErrorStatus.ValidAuthenticationCredentialsRequired or CoreWebView2WebErrorStatus.ValidProxyAuthenticationRequired => new(Key, "Sign-in required", "This site or your network's proxy needs a username and password that wasn't provided."),
   CoreWebView2WebErrorStatus.ErrorHttpInvalidServerResponse => new(Alert, "Unexpected response", "The site sent back something Carbon couldn't understand. Try again in a moment."),
   CoreWebView2WebErrorStatus.CertificateCommonNameIsIncorrect or CoreWebView2WebErrorStatus.CertificateExpired or CoreWebView2WebErrorStatus.ClientCertificateContainsErrors
    or CoreWebView2WebErrorStatus.CertificateRevoked or CoreWebView2WebErrorStatus.CertificateIsInvalid => Pick(status, true),
   _ => new(Alert, "This page couldn't load", "Something went wrong while loading this page."),
  };
 }

 public static string Crashed(string url, bool dark) => Render(new(Alert, "This page crashed", "Something on this page made it stop working. Your other tabs are fine."), url, dark, "", false, "RenderProcessExited", null, null);

 public static string Html(CoreWebView2WebErrorStatus status, bool certificate, string url, bool dark, string searchEngine, OfflineGame? game = null, IReadOnlyList<SavedPage>? saved = null)
 {
  var kind = Pick(status, certificate);
  bool offline = kind.Icon == Wifi;
  return Render(kind, url, dark, searchEngine, certificate, status.ToString(), offline ? game ?? new() : null, offline ? saved : null);
 }

 /// Signal Run (Assets/offline-game.js), with the player's saved best, electrons and skin. Read once.
 static string? gameJs;
 static string GameJs => gameJs ??= AppContent.ReadAsset("offline-game.js").GetAwaiter().GetResult();

 static string Render(Kind k, string url, bool dark, string searchEngine, bool certificate, string code, OfflineGame? game, IReadOnlyList<SavedPage>? saved)
 {
  string host = Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : url;
  string e(string s) => WebUtility.HtmlEncode(s);
  // Only link back to web URLs; never echo other schemes into an href.
  string retry = u != null && u.Scheme is "http" or "https" or "file" ? u.AbsoluteUri : "";
  string search = (searchEngine == "Bing" ? "https://www.bing.com/search?q=" : searchEngine == "DuckDuckGo" ? "https://duckduckgo.com/?q=" : "https://www.google.com/search?q=") + Uri.EscapeDataString(host);
  // Still's colors, exactly as Still draws this page.
  string bg = dark ? "#1C1C1E" : "#FCFBF8", ink = dark ? "#EFEFEA" : "#1B1B1A", muted = dark ? "#8E8F89" : "#6B6C67", line = dark ? "#262626" : "#E3E3DF", btnBg = dark ? "#EFEFEA" : "#1B1B1A", btnInk = dark ? "#000" : "#FAFAF9";
  string script = "";
  if (game != null) {
   string save = System.Text.Json.JsonSerializer.Serialize(new { best = game.Best, bestDist = game.BestDistance, electrons = game.Electrons, skin = game.Skin, mute = game.Mute, day = game.Day, done = game.Done, dark });
   script = "<script>window.CARBON_OFFLINE=" + save.Replace("<", "\\u003c") + ";</script><script>" + GameJs.Replace("</script", "<\\/script") + "</script>"
    + "<script>document.querySelectorAll('.saved button').forEach(x=>x.addEventListener('click',()=>{try{chrome.webview.postMessage({kind:'carbon-open-saved',id:x.dataset.id})}catch(e){}}));</script>"
    + "<script>(()=>{const b=document.getElementById('back');let was=navigator.onLine;const on=()=>{if(navigator.onLine&&!was)b.hidden=false;was=navigator.onLine};addEventListener('online',()=>{was=false;on()});addEventListener('offline',()=>{was=false});setInterval(on,3000);})();</script>";
  }
  string gameHtml = game == null ? "" : $"""
   <section class='game' aria-label='Signal Run, a game to play while you're offline'>
    <canvas id='signal-run' aria-label='Signal Run. Press Space to start.'></canvas>
    <p class='hint'>Signal Run · Space or tap to jump, twice to double-jump, ↓ to duck · M sound · P pause</p>
   </section>
   {(saved is { Count: > 0 } ? "<section class='saved' aria-label='Pages you saved'><h2>Read offline</h2><div>" + string.Concat(saved.Take(12).Select(p => $"<button data-id='{e(p.Id)}' title='{e(p.Url)}'><b>{e(p.Title)}</b><small>{e(Uri.TryCreate(p.Url, UriKind.Absolute, out var su) ? su.Host : "")} · saved {p.Saved:MMM d}</small></button>")) + "</div></section>" : "")}
   <div id='back' class='back' hidden role='status'><b>You're back online.</b>{(retry.Length > 0 ? $"<a class='btn primary' href='{e(retry)}'>Open {e(host)}</a>" : "")}</div>
   """;
  return $@"<!doctype html><html><head><meta charset='utf-8'><meta name='color-scheme' content='{(dark ? "dark" : "light")}'><title>{e(k.Title)}</title><style>
html,body{{margin:0;height:100%;background:{bg};color:{ink};font:15px/1.55 'Segoe UI Variable Text','Segoe UI',system-ui,sans-serif}}
main{{min-height:100%;display:flex;flex-direction:column;gap:28px;align-items:center;justify-content:center;padding:40px;box-sizing:border-box}}
.card{{max-width:520px;animation:in .45s cubic-bezier(.22,1,.36,1) both}}
@keyframes in{{from{{opacity:0;transform:translateY(10px)}}to{{opacity:1;transform:none}}}}
@media (prefers-reduced-motion:reduce){{.card,.game{{animation:none}}}}
svg{{width:44px;height:44px;stroke:{ink};fill:none;stroke-width:1.6;stroke-linecap:round;stroke-linejoin:round;opacity:.9}}
h1{{font-size:30px;font-weight:650;letter-spacing:-.02em;margin:22px 0 8px}}
p{{color:{muted};margin:0 0 6px}}
.host{{color:{ink};font-weight:600}}
.row{{display:flex;gap:10px;margin-top:26px;flex-wrap:wrap}}
a.btn{{display:inline-flex;align-items:center;height:38px;padding:0 18px;border-radius:10px;text-decoration:none;font-weight:600;font-size:14px}}
a.primary{{background:{btnBg};color:{btnInk}}} a.ghost{{color:{ink};border:1px solid {line}}}
a.btn:hover{{opacity:.85}}
code{{display:block;margin-top:28px;color:{muted};font:12px ui-monospace,Consolas,monospace;opacity:.8}}
.game{{width:min(760px,100%);animation:in .45s .1s cubic-bezier(.22,1,.36,1) both}}
canvas{{display:block;width:100%;height:clamp(220px,38vh,320px);border-radius:12px;border:1px solid {line};outline:none;cursor:pointer;touch-action:none}}
canvas:focus-visible{{box-shadow:0 0 0 2px {muted}}}
.hint{{text-align:center;font-size:12px;margin-top:10px}}
.back{{position:fixed;left:50%;bottom:24px;transform:translateX(-50%);display:flex;align-items:center;gap:14px;padding:8px 8px 8px 16px;border-radius:12px;background:{bg};border:1px solid {line};box-shadow:0 8px 30px rgba(0,0,0,.25);animation:in .4s both}}
.back[hidden]{{display:none}}
.saved{{width:min(760px,100%)}} .saved h2{{font-size:13px;font-weight:600;color:{muted};margin:0 0 10px}}
.saved div{{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:10px}}
.saved button{{text-align:left;padding:10px 14px;border-radius:10px;border:1px solid {line};background:transparent;color:{ink};font:inherit;cursor:pointer}}
.saved button:hover{{opacity:.85}} .saved b{{display:block;font-size:14px;font-weight:600;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}} .saved small{{color:{muted};font-size:12px}}
</style></head><body><main><div class='card'>
<svg viewBox='0 0 24 24'>{k.Icon}</svg>
<h1>{e(k.Title)}</h1>
<p><span class='host'>{e(host)}</span></p>
<p>{e(k.Body)}</p>
<div class='row'>{(retry.Length > 0 && !certificate ? $"<a class='btn primary' href='{e(retry)}'>Try again</a>" : "")}{(k.Search ? $"<a class='btn ghost' href='{e(search)}'>Search for {e(host)}</a>" : "")}</div>
<code>{e(code)}</code>
</div>{gameHtml}</main>{script}</body></html>";
 }
}

/// A page saved for offline reading (Menu > Save page for offline): an MHTML file in the profile's saved-pages folder.
public sealed class SavedPage
{
 public string Id { get; set; } = "";
 public string Title { get; set; } = "";
 public string Url { get; set; } = "";
 public string File { get; set; } = "";
 public DateTime Saved { get; set; }
}

/// Signal Run's saved progress (the offline page's game): the best score and where that run ended, electrons collected over
/// all runs (they unlock skins), the skin worn, sound, and today's finished missions.
public sealed class OfflineGame
{
 public int Best { get; set; }
 public int BestDistance { get; set; }
 public int Electrons { get; set; }
 public int Skin { get; set; }
 public bool Mute { get; set; }
 public string Day { get; set; } = "";
 public List<string> Done { get; set; } = [];
}
