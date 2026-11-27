using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
namespace Carbon;

/// Carbon's ad and tracker blocker: Still's blocker (requests answered with 204, per-site off switch, a count on the Site
/// button), made far more accurate. It reads real filter lists, the same ones uBlock Origin and Adblock Plus start from:
/// EasyList (ads), EasyPrivacy (trackers) and Peter Lowe's list, downloaded every few days and kept in Carbon's folder,
/// with a built-in list until they arrive. It understands their network rules (whole domains, URL patterns with * ^ |
/// and ||, $third-party, resource types, $domain=, and @@ exceptions) and their element hiding (##, domain##, #@#), and it
/// skips YouTube's video ads. Rules it can't follow exactly are left out, so it never blocks more than a list means to.
static partial class AdBlock
{
 // ---------- built in: used until the lists arrive, and kept after ----------
 static readonly string[] BuiltInDomains = [
  "doubleclick.net", "googlesyndication.com", "googleadservices.com", "adservice.google.com", "pagead2.googlesyndication.com", "tpc.googlesyndication.com",
  "google-analytics.com", "googletagmanager.com", "googletagservices.com", "imasdk.googleapis.com", "2mdn.net", "adnxs.com", "adnxs-simple.com", "adsrvr.org",
  "taboola.com", "taboolasyndication.com", "outbrain.com", "outbrainimg.com", "scorecardresearch.com", "quantserve.com", "quantcount.com", "criteo.com", "criteo.net",
  "pubmatic.com", "rubiconproject.com", "openx.net", "casalemedia.com", "advertising.com", "amazon-adsystem.com", "aax.amazon-adsystem.com", "hotjar.com", "hotjar.io",
  "clarity.ms", "connect.facebook.net", "analytics.tiktok.com", "ads.linkedin.com", "snap.licdn.com", "bat.bing.com", "ads.twitter.com", "static.ads-twitter.com",
  "moatads.com", "moatpixel.com", "adform.net", "adformdsp.net", "smartadserver.com", "3lift.com", "sharethrough.com", "media.net", "yieldmo.com", "teads.tv", "zemanta.com",
  "mgid.com", "revcontent.com", "adcolony.com", "applovin.com", "unityads.unity3d.com", "ads.yahoo.com", "advertising.yahoo.com", "adsafeprotected.com",
  "doubleverify.com", "chartbeat.com", "chartbeat.net", "mixpanel.com", "segment.io", "fullstory.com", "nr-data.net", "bluekai.com", "krxd.net", "exelator.com",
  "demdex.net", "everesttech.net", "omtrdc.net", "2o7.net", "rlcdn.com", "rfihub.com", "adroll.com", "agkn.com", "mathtag.com", "turn.com", "bidswitch.net",
  "contextweb.com", "sovrn.com", "lijit.com", "gumgum.com", "indexww.com", "districtm.io", "emxdgt.com", "smaato.net", "inmobi.com", "adsymptotic.com",
  "33across.com", "tribalfusion.com", "conversantmedia.com", "dotomi.com", "yieldlab.net", "adition.com", "adtech.de", "serving-sys.com", "flashtalking.com",
  "sizmek.com", "spotxchange.com", "spotx.tv", "springserve.com", "tremorhub.com", "undertone.com", "zedo.com", "propellerads.com", "popads.net", "popcash.net",
  "adcash.com", "exoclick.com", "exosrv.com", "juicyads.com", "trafficjunky.net", "hilltopads.net", "adsterra.com", "a-ads.com", "bidvertiser.com", "infolinks.com",
  "vibrantmedia.com", "kargo.com", "triplelift.com", "nativo.com", "plista.com", "ligatus.com", "adblade.com", "content.ad", "lockerdome.com", "zergnet.com",
  "yandexadexchange.net", "an.yandex.ru", "mc.yandex.ru", "adriver.ru", "adfox.ru", "begun.ru", "top-fwz1.mail.ru", "ad.mail.ru", "counter.yadro.ru",
  "adskeeper.co.uk", "adsco.re", "onetag-sys.com", "seedtag.com", "richaudience.com", "adyoulike.com", "improvedigital.com", "360yield.com", "betweendigital.com",
  "adhese.com", "mookie1.com", "eyeota.net", "tapad.com", "liadm.com", "crwdcntrl.net", "lotame.com", "id5-sync.com", "33across.com", "permutive.com",
  "permutive.app", "adlightning.com", "confiant-integrations.net", "cdn.confiant-integrations.net", "brandmetrics.com", "parsely.com", "newrelic.com",
  "branch.io", "app.link", "appsflyer.com", "adjust.com", "kochava.com", "singular.net", "amplitude.com", "heapanalytics.com", "kissmetrics.com",
  "mouseflow.com", "crazyegg.com", "luckyorange.com", "luckyorange.net", "inspectlet.com", "smartlook.com", "statcounter.com", "histats.com", "clicky.com",
  "optimizely.com", "cdn.optimizely.com", "doubleclick.com", "adzerk.net", "carbonads.net", "buysellads.com", "srv.buysellads.com", "pub.network",
  "ad.plus", "adnium.com", "trafficstars.com", "tsyndicate.com", "clickadu.com", "admaven.com", "pushwhy.com", "onesignal-ads.com", "realsrv.com",
  "googleads.g.doubleclick.net", "securepubads.g.doubleclick.net", "stats.g.doubleclick.net", "cm.g.doubleclick.net", "static.doubleclick.net",
  "ads.pubmatic.com", "ads.yieldmo.com", "ads.stickyadstv.com", "stickyadstv.com", "smartclip.net", "teads.com", "innovid.com", "eyeviewads.com",
  "admob.com", "adsensecustomsearchads.com", "adsense.com", "pixel.facebook.com", "an.facebook.com", "ads.reddit.com", "events.redditmedia.com",
  "ads.pinterest.com", "ct.pinterest.com", "analytics.pinterest.com", "sc-static.net", "tr.snapchat.com", "ads-api.tiktok.com", "business-api.tiktok.com",
  "ads.youtube.com", "s0.2mdn.net", "imrworldwide.com", "comscore.com", "nielsen.com", "adobedtm.com", "dpm.demdex.net", "tags.tiqcdn.com",
  "trackcmp.net", "pardot.com", "hs-analytics.net", "hs-banner.com", "hsadspixel.net", "hubspot.net", "track.hubspot.com", "marketo.net", "mktoresp.com",
  "zemanta.com", "ad.atdmt.com", "atdmt.com", "rubiconproject.net", "pubmine.com", "adthrive.com", "ads.adthrive.com", "mediavine.com", "scripts.mediavine.com",
  "ezoic.net", "ezojs.com", "ezodn.com", "monumetric.com", "freestar.com", "freestar.io", "venatusmedia.com", "playwire.com", "nitropay.com", "snigelweb.com",
  "adinplay.com", "aniview.com", "vidazoo.com", "primis.tech", "connatix.com", "ex.co", "minute.ly", "jwpltx.com", "ads.jwpsrv.com", "springserve.net",
 ];
 // In Adblock Plus syntax, run through the same parser as the downloaded lists.
 static readonly string[] BuiltInRules = [
  "||youtube.com/pagead/", "||youtube.com/api/stats/ads", "||youtube.com/ptracking", "||youtube.com/get_midroll_", "||youtube.com/pcs/activeview",
  "||youtube.com/youtubei/v1/log_event$xmlhttprequest", "||googlevideo.com/initplayback?*&oad=", "||google.com/pagead/", "||google.com/adsense/",
  "/pagead/js/adsbygoogle.js", "/gpt.js$script,third-party", "/pubads_impl$script", "/prebid.js$script", "/prebid-$script", "/adsbygoogle.$script",
  "/ads.js$script,third-party", "/adserver/*", "/adframe.", "/ad_banner.", "/banner-ad.", "-ad-banner.", "/advertisement.", "/adsrv.", "/ad-server/",
  "##ins.adsbygoogle", "##.adsbygoogle", "##[id^=\"div-gpt-ad\"]", "##[id^=\"google_ads_iframe\"]", "##.ad-slot", "##.ad-container", "##.ad-banner",
  "##.advertisement", "##[data-ad-slot]", "##[data-google-query-id]", "##.taboola-widget", "##[id^=\"taboola-\"]", "##.OUTBRAIN", "##.ob-widget",
  "##.trc_related_container", "##.sponsored-content", "##.ad-wrapper", "##div[aria-label=\"advertisement\"]", "##div[aria-label=\"Advertisement\"]",
  "youtube.com##ytd-ad-slot-renderer", "youtube.com##ytd-in-feed-ad-layout-renderer", "youtube.com##ytd-promoted-sparkles-web-renderer",
  "youtube.com##ytd-display-ad-renderer", "youtube.com##ytd-companion-slot-renderer", "youtube.com##ytd-action-companion-ad-renderer",
  "youtube.com##ytd-banner-promo-renderer", "youtube.com###masthead-ad", "youtube.com###player-ads", "youtube.com##.ytp-ad-overlay-container",
  "youtube.com##ytd-rich-item-renderer:has(ytd-ad-slot-renderer)", "youtube.com##ytd-merch-shelf-renderer", "youtube.com##ytd-promoted-video-renderer",
  "youtube.com##ytd-search-pyv-renderer", "youtube.com##ytd-video-masthead-ad-v3-renderer", "youtube.com##ytd-statement-banner-renderer",
  "youtube.com##ytd-engagement-panel-section-list-renderer[target-id=\"engagement-panel-ads\"]", "youtube.com##.ytd-player-legacy-desktop-watch-ads-renderer",
 ];

 // ---------- the filter lists ----------
 public static readonly (string Name, string Url)[] Lists = [
  ("EasyList", "https://easylist.to/easylist/easylist.txt"),
  ("EasyPrivacy", "https://easylist.to/easylist/easyprivacy.txt"),
  ("Peter Lowe's list", "https://pgl.yoyo.org/adservers/serverlist.php?hostformat=adblockplus&showintro=0&mimetype=plaintext"),
 ];
 static string Folder => Path.Combine(App.ProfileHome.Length > 0 ? App.ProfileHome : App.DataRoot, "blocklists");
 public static DateTime Updated { get; private set; }
 public static int RuleCount => current.Count;

 [Flags] enum Types { None = 0, Script = 1, Image = 2, Style = 4, Xhr = 8, Sub = 16, Media = 32, Font = 64, Socket = 128, Ping = 256, Other = 512, All = 1023 }

 sealed class Pattern
 {
  public required string Source;            // the rule's pattern, lower case, without its anchors and options
  public string? Domain;                    // ||domain… : the host the pattern is anchored to
  public bool Start, End;                   // | at the start or the end
  public bool? ThirdParty;                  // $third-party (true), $~third-party / $first-party (false)
  public Types Types = Types.All;
  public string[] Only = [], Not = [];      // $domain=a.com|~b.com: the page's site
  Regex? regex;
  public bool Matches(string url, string host, int pathAt)
  {
   if (Domain != null) {
    if (!(host == Domain || host.EndsWith("." + Domain, StringComparison.Ordinal))) return false;
    if (Source.Length == 0) return true;
    return (regex ??= Build("^" + Wild(Source))).IsMatch(url.AsSpan(pathAt));
   }
   return (regex ??= Build((Start ? "^" : "") + Wild(Source) + (End ? "$" : ""))).IsMatch(url);
  }
  static string Wild(string s) => Regex.Escape(s).Replace("\\*", ".*").Replace("\\^", "(?:[^a-z0-9_.%-]|$)");
  static Regex Build(string p) => new(p, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
 }

 sealed class Rules
 {
  public readonly HashSet<string> Domains = new(StringComparer.Ordinal);            // ||domain^ with no options: the fast path
  public readonly HashSet<string> AllowDomains = new(StringComparer.Ordinal);       // @@||domain^ with no options
  public readonly Dictionary<string, List<Pattern>> Block = new(StringComparer.Ordinal), Allow = new(StringComparer.Ordinal); // by token
  public readonly List<Pattern> BlockLoose = [], AllowLoose = [];                   // patterns with no token to index them by
  public readonly List<string> Generic = [];                                        // ##selector
  public readonly Dictionary<string, List<string>> Hide = new(StringComparer.Ordinal); // domain##selector
  public readonly Dictionary<string, HashSet<string>> Unhide = new(StringComparer.Ordinal); // domain#@#selector
  public readonly HashSet<string> NoCosmetics = new(StringComparer.Ordinal);       // @@||domain^$elemhide / $generichide
  public int Count;
  public string? GenericCss;
 }

 // ---------- parsing ----------
 static readonly HashSet<string> Unsupported = ["redirect", "redirect-rule", "csp", "removeparam", "rewrite", "replace", "header", "popup", "document", "doc", "badfilter",
  "important", "permissions", "urlskip", "uritransform", "to", "denyallow", "method", "ipaddress", "match-case", "cname", "popunder", "inline-script", "inline-font", "empty", "mp4"];
 static readonly Dictionary<string, Types> TypeNames = new() { ["script"] = Types.Script, ["image"] = Types.Image, ["stylesheet"] = Types.Style, ["css"] = Types.Style,
  ["xmlhttprequest"] = Types.Xhr, ["xhr"] = Types.Xhr, ["subdocument"] = Types.Sub, ["frame"] = Types.Sub, ["media"] = Types.Media, ["font"] = Types.Font,
  ["websocket"] = Types.Socket, ["ping"] = Types.Ping, ["beacon"] = Types.Ping, ["other"] = Types.Other, ["object"] = Types.Other };
 // Element hiding that isn't plain CSS (procedural, scriptlets): left out.
 [GeneratedRegex(@":(-abp-|has-text|contains|xpath|upward|remove|style|matches-|min-text|watch-attr|nth-ancestor|others|if|if-not|spath|matches-path|matches-attr|matches-prop)|^\+js|^\^|#\+js|\{", RegexOptions.CultureInvariant)] private static partial Regex Procedural();
 [GeneratedRegex(@"[a-z0-9%]{3,}", RegexOptions.CultureInvariant)] private static partial Regex Token();

 static Rules Parse(IEnumerable<string> lines)
 {
  var r = new Rules();
  foreach (var raw in lines) {
   string line = raw.Trim();
   if (line.Length < 3 || line[0] == '!' || line[0] == '[') continue;
   try { if (ParseLine(r, line)) r.Count++; } catch (Exception e) when (e is ArgumentException or RegexParseException or IndexOutOfRangeException) { }
  }
  return r;
 }
 static bool ParseLine(Rules r, string line)
 {
  // Element hiding: [domains]##selector, [domains]#@#selector
  int hash = line.IndexOf("##", StringComparison.Ordinal), unhash = line.IndexOf("#@#", StringComparison.Ordinal);
  if (unhash > 0 || unhash == 0) {
   string sel = line[(unhash + 3)..].Trim(); if (sel.Length == 0 || Procedural().IsMatch(sel)) return false;
   foreach (var d in Sites(line[..unhash]).Where(d => !d.StartsWith('~'))) { if (!r.Unhide.TryGetValue(d, out var set)) r.Unhide[d] = set = []; set.Add(sel); }
   return true;
  }
  if (hash >= 0) {
   string sel = line[(hash + 2)..].Trim(); if (sel.Length == 0 || Procedural().IsMatch(sel) || line.Contains("#?#") || line.Contains("#$#")) return false;
   var sites = Sites(line[..hash]).ToList();
   if (sites.Count == 0) { if (r.Generic.Count >= 15000) return false; r.Generic.Add(sel); return true; }
   if (sites.All(s => s.StartsWith('~'))) return false; // "everywhere but" is generic hiding with exceptions: left out
   foreach (var d in sites.Where(s => !s.StartsWith('~'))) { if (!r.Hide.TryGetValue(d, out var list)) r.Hide[d] = list = []; list.Add(sel); }
   return true;
  }
  if (line.Contains("#?#") || line.Contains("#$#") || line.Contains("#%#")) return false;
  // Hosts-file lines (0.0.0.0 example.com) count as whole domains.
  if (line.StartsWith("0.0.0.0 ", StringComparison.Ordinal) || line.StartsWith("127.0.0.1 ", StringComparison.Ordinal)) {
   string d = line[(line.IndexOf(' ') + 1)..].Trim().ToLowerInvariant(); if (d.Length > 3 && d.Contains('.') && d != "localhost") { r.Domains.Add(d); return true; } return false;
  }
  // Network rules
  bool allow = line.StartsWith("@@", StringComparison.Ordinal); if (allow) line = line[2..];
  string pattern = line, options = "";
  int dollar = line.LastIndexOf('$');
  if (dollar >= 0 && !line.StartsWith('/') || dollar > 0 && line.StartsWith('/') && !line.EndsWith('/')) { pattern = line[..dollar]; options = line[(dollar + 1)..]; }
  if (pattern.StartsWith('/') && pattern.EndsWith('/') && pattern.Length > 2) return false; // regular-expression rules: left out
  pattern = pattern.ToLowerInvariant();
  var p = new Pattern { Source = "" };
  bool cosmeticOnly = false;
  foreach (var opt in options.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
   string o = opt.ToLowerInvariant(); bool not = o.StartsWith('~'); string name = not ? o[1..] : o; int eq = name.IndexOf('=');
   string key = eq >= 0 ? name[..eq] : name;
   if (key is "third-party" or "3p") p.ThirdParty = !not;
   else if (key is "first-party" or "1p") p.ThirdParty = not;
   else if (key is "domain" or "from" && eq >= 0) { var ds = name[(eq + 1)..].Split('|'); p.Only = [.. ds.Where(d => !d.StartsWith('~'))]; p.Not = [.. ds.Where(d => d.StartsWith('~')).Select(d => d[1..])]; }
   else if (TypeNames.TryGetValue(key, out var t)) p.Types = not ? (p.Types == Types.All ? Types.All : p.Types) & ~t : (p.Types == Types.All ? t : p.Types | t);
   else if (key is "elemhide" or "ehide" or "generichide" or "ghide" && allow) cosmeticOnly = true;
   else if (Unsupported.Contains(key) || key.Length > 0) return false; // an option Carbon can't honor: leave the rule out
  }
  if (cosmeticOnly) {
   if (pattern.StartsWith("||") && pattern.TrimEnd('^', '/') is var host && host.IndexOfAny(['/', '*'], 2) < 0) { r.NoCosmetics.Add(host[2..]); return true; }
   return false;
  }
  if (pattern.StartsWith("||")) {
   string rest = pattern[2..]; int cut = rest.IndexOfAny(['/', '^', '*', '?', ':']);
   p.Domain = cut < 0 ? rest : rest[..cut];
   if (p.Domain.Length == 0 || p.Domain.Contains('*')) return false;
   string tail = cut < 0 ? "" : rest[cut..];
   if (tail is "" or "^" or "^|" or "/" ) {
    if (options.Length == 0) { (allow ? r.AllowDomains : r.Domains).Add(p.Domain); return true; }
    tail = "";
   }
   if (tail.EndsWith('|')) { p.End = true; tail = tail[..^1]; }
   p.Source = tail;
  } else {
   if (pattern.StartsWith('|')) { p.Start = true; pattern = pattern[1..]; }
   if (pattern.EndsWith('|')) { p.End = true; pattern = pattern[..^1]; }
   pattern = pattern.Trim('*');
   if (pattern.Length < 4) return false; // too short to be safe
   p.Source = pattern;
  }
  // Index by a token the URL is sure to contain as a whole word.
  string? token = null;
  string src = (p.Domain ?? "") + "\u0001" + p.Source;
  foreach (Match m in Token().Matches(src)) {
   bool leftOk = m.Index == 0 || src[m.Index - 1] is not '*', rightOk = m.Index + m.Length >= src.Length || src[m.Index + m.Length] is not '*';
   if (leftOk && rightOk && (token == null || m.Length > token.Length)) token = m.Value;
  }
  var index = allow ? r.Allow : r.Block;
  if (token == null) (allow ? r.AllowLoose : r.BlockLoose).Add(p);
  else { if (!index.TryGetValue(token, out var list)) index[token] = list = []; list.Add(p); }
  return true;
 }
 static IEnumerable<string> Sites(string list) => list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s => s.ToLowerInvariant());

 // After the tables above: static fields start in the order they're written.
 static volatile Rules current = Parse([.. BuiltInDomains.Select(d => "||" + d + "^"), .. BuiltInRules]);

 // ---------- matching ----------
 static string Site(string host) // the registrable part, near enough: example.co.uk, example.com
 {
  var parts = host.Split('.'); if (parts.Length <= 2) return host;
  bool shortTld = parts[^2].Length <= 3 && parts[^1].Length == 2 && parts[^2] is "co" or "com" or "org" or "net" or "gov" or "ac" or "edu" or "ne" or "or";
  return string.Join('.', parts[^(shortTld ? 3 : 2)..]);
 }
 static List<string> Words(string url) { var list = new List<string>(); foreach (var m in Token().EnumerateMatches(url)) list.Add(url.Substring(m.Index, m.Length)); return list; }
 static bool Under(string host, string domain) => host == domain || host.EndsWith("." + domain, StringComparison.Ordinal);
 static bool InSet(HashSet<string> set, string host)
 {
  for (string h = host; ; ) { if (set.Contains(h)) return true; int dot = h.IndexOf('.'); if (dot < 0) return false; h = h[(dot + 1)..]; }
 }
 static Types TypeOf(Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext c) => c switch {
  Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.Script => Types.Script,
  Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.Image => Types.Image,
  Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.Stylesheet => Types.Style,
  Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.XmlHttpRequest or Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.Fetch or Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.EventSource => Types.Xhr,
  Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.Document => Types.Sub,
  Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.Media => Types.Media,
  Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.Font => Types.Font,
  Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.Websocket => Types.Socket,
  Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.Ping => Types.Ping,
  _ => Types.Other };

 /// Whether a request from a page should be blocked. Never the page itself, never the page's own site.
 public static bool ShouldBlock(Uri request, Uri page, Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext context) =>
  ShouldBlock(request.AbsoluteUri.ToLowerInvariant(), request.Host.ToLowerInvariant(), request.AbsoluteUri.IndexOf('/', request.Scheme.Length + 3) is var i && i > 0 ? i : request.AbsoluteUri.Length, page.Host.ToLowerInvariant(), TypeOf(context));
 static bool ShouldBlock(string url, string host, int pathAt, string pageHost, Types type)
 {
  var r = current;
  bool third = Site(host) != Site(pageHost);
  // A site's own requests: only rules anchored to a domain (||youtube.com/pagead/) or marked first-party apply.
  bool Fits(Pattern p) => (p.Types & type) != 0 && (p.ThirdParty is not { } tp || tp == third) && (third || p.Domain != null || p.ThirdParty == false)
   && (p.Only.Length == 0 || p.Only.Any(d => Under(pageHost, d))) && !p.Not.Any(d => Under(pageHost, d)) && p.Matches(url, host, pathAt);
  List<string>? tokens = null; // the URL's words, found once for both lookups
  bool Any(Dictionary<string, List<Pattern>> index, List<Pattern> loose) {
   if (index.Count > 0) foreach (var w in tokens ??= Words(url)) if (index.TryGetValue(w, out var list)) foreach (var p in list) if (Fits(p)) return true;
   foreach (var p in loose) if (Fits(p)) return true;
   return false;
  }
  bool blocked = (third && InSet(r.Domains, host)) || Any(r.Block, r.BlockLoose);
  return blocked && !InSet(r.AllowDomains, host) && !Any(r.Allow, r.AllowLoose);
 }

 /// The element-hiding CSS for a page's host: the lists' generic rules (unless the site has exceptions) and its own.
 public static string Css(string host)
 {
  var r = current; host = host.ToLowerInvariant();
  if (InSet(r.NoCosmetics, host)) return "";
  var unhide = new HashSet<string>(StringComparer.Ordinal); var own = new List<string>();
  for (string h = host; ; ) {
   if (r.Unhide.TryGetValue(h, out var u)) unhide.UnionWith(u);
   if (r.Hide.TryGetValue(h, out var l)) own.AddRange(l);
   int dot = h.IndexOf('.'); if (dot < 0) break; h = h[(dot + 1)..];
  }
  string generic = unhide.Count == 0 ? r.GenericCss ??= Rule(r.Generic) : Rule(r.Generic.Where(s => !unhide.Contains(s)));
  return generic + Rule(own.Where(s => !unhide.Contains(s)));
 }
 // In groups, so one selector this engine doesn't know only drops its own group.
 static string Rule(IEnumerable<string> selectors) => string.Concat(selectors.Distinct().Chunk(60).Select(c => string.Join(",", c) + "{display:none!important}\n"));

 /// The script that puts the CSS in a page (top frame only) and, on YouTube, skips video ads and closes ad overlays.
 public static string PageScript(string host)
 {
  string css = Css(host);
  bool youtube = Under(host.ToLowerInvariant(), "youtube.com");
  if (css.Length == 0 && !youtube) return "";
  var js = new StringBuilder("(() => { if (window !== top || document.getElementById('carbon-adblock')) return; const s = document.createElement('style'); s.id = 'carbon-adblock'; s.textContent = ");
  js.Append(System.Text.Json.JsonSerializer.Serialize(css)).Append("; (document.head || document.documentElement).appendChild(s);");
  if (youtube) js.Append("""
    let muted = null;
    const skip = () => {
     const player = document.querySelector('.html5-video-player'), video = player && player.querySelector('video');
     if (player && video && player.classList.contains('ad-showing')) {
      if (muted === null) muted = video.muted;
      video.muted = true;
      if (isFinite(video.duration) && video.duration > 0) video.currentTime = Math.max(0, video.duration - 0.05);
      try { video.playbackRate = 16; } catch (e) { }
     } else if (video && muted !== null) { video.muted = muted; muted = null; if (video.playbackRate === 16) video.playbackRate = 1; }
     document.querySelectorAll('.ytp-skip-ad-button, .ytp-ad-skip-button, .ytp-ad-skip-button-modern, .ytp-skip-ad button, .ytp-ad-overlay-close-button').forEach(b => b.click());
    };
    new MutationObserver(skip).observe(document.documentElement, { subtree: true, childList: true, attributes: true, attributeFilter: ['class'] });
    setInterval(skip, 400);
    """);
  return js.Append(" })();").ToString();
 }

 // ---------- updating the lists ----------
 static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(2) };
 static bool updating;
 /// Loads the saved lists, then downloads fresh ones when they're more than 4 days old. Runs in the background.
 public static async Task Update()
 {
  if (updating) return; updating = true;
  try {
   Directory.CreateDirectory(Folder);
   var files = Lists.Select((l, i) => Path.Combine(Folder, $"list{i}.txt")).ToArray();
   if (files.Any(File.Exists)) Load(files);
   bool stale = files.Any(f => !File.Exists(f) || File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-4));
   if (!stale) return;
   bool got = false;
   for (int i = 0; i < Lists.Length; i++) {
    try {
     string text = await Http.GetStringAsync(Lists[i].Url);
     if (text.Length < 1000) continue; // not a list: an error page
     await File.WriteAllTextAsync(files[i] + ".tmp", text); File.Move(files[i] + ".tmp", files[i], true); got = true;
    } catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException) { App.Log(e); }
   }
   if (got) Load(files);
  } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { App.Log(e); }
  finally { updating = false; }
 }
 static void Load(string[] files)
 {
  IEnumerable<string> Lines() {
   foreach (var d in BuiltInDomains) yield return "||" + d + "^";
   foreach (var l in BuiltInRules) yield return l;
   foreach (var f in files.Where(File.Exists)) foreach (var l in File.ReadLines(f)) yield return l;
  }
  current = Parse(Lines());
  Updated = files.Where(File.Exists).Select(File.GetLastWriteTime).DefaultIfEmpty().Max();
 }

 /// For tests: whether url would be blocked on a page at pageUrl, for a request of this kind.
 internal static bool Test(string url, string pageUrl, string kind = "script") =>
  ShouldBlock(new Uri(url), new Uri(pageUrl), kind switch { "image" => Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.Image, "xhr" => Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.XmlHttpRequest, "frame" => Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.Document, _ => Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.Script });
 internal static void UseForTest(IEnumerable<string> lines) => current = Parse(lines);
}
