using System.IO;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
namespace Carbon;

// Pages saved for offline reading (from Bullet's website saver): Menu > Save page for offline keeps the page as one MHTML
// file in the profile, and the "You're offline" page lists them so they can still be read with no internet.
public partial class MainWindow
{
 static string SavedFolder => Path.Combine(App.DataRoot, "saved-pages");
 static string SavedIndex => Path.Combine(SavedFolder, "index.json");
 static List<SavedPage> SavedPages()
 {
  try { return File.Exists(SavedIndex) ? JsonSerializer.Deserialize<List<SavedPage>>(File.ReadAllText(SavedIndex)) ?? [] : []; }
  catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { App.Log(ex); return []; }
 }
 async Task SavePageOffline()
 {
  if (active is not { Private: false } tab || tab.View?.CoreWebView2 is not { } core || !Uri.TryCreate(tab.Url, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https")) { Toast("Open a web page to save it."); return; }
  try {
   string json = await core.CallDevToolsProtocolMethodAsync("Page.captureSnapshot", "{\"format\":\"mhtml\"}");
   using var doc = JsonDocument.Parse(json);
   string mhtml = doc.RootElement.GetProperty("data").GetString() ?? "";
   if (mhtml.Length == 0) { Toast("Carbon couldn't save this page."); return; }
   Directory.CreateDirectory(SavedFolder);
   var list = SavedPages();
   var old = list.FirstOrDefault(p => p.Url == tab.Url);
   string id = old?.Id ?? Guid.NewGuid().ToString("N")[..12], file = Path.Combine(SavedFolder, id + ".mhtml");
   await File.WriteAllTextAsync(file, mhtml);
   list.RemoveAll(p => p.Id == id);
   list.Insert(0, new SavedPage { Id = id, Title = tab.Title.Length > 0 ? tab.Title : url.Host, Url = tab.Url, File = file, Saved = DateTime.Now });
   foreach (var gone in list.Skip(200)) try { File.Delete(gone.File); } catch (IOException) { }
   await File.WriteAllTextAsync(SavedIndex, JsonSerializer.Serialize(list.Take(200).ToList()));
   Toast("Saved for offline. When you're offline, it's on the You're offline page.");
  } catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException) {
   App.Log(ex); Toast("Carbon couldn't save this page.");
  }
 }
 void OpenSavedPage(BrowserTab tab, CoreWebView2 core, string json)
 {
  try {
   using var doc = JsonDocument.Parse(json);
   string id = doc.RootElement.GetProperty("id").GetString() ?? "";
   if (SavedPages().FirstOrDefault(p => p.Id == id) is not { } page || !File.Exists(page.File)) { Toast("That saved page is gone."); return; }
   tab.ShowingError = false;
   core.Navigate(new Uri(page.File).AbsoluteUri);
  } catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException) { App.Log(ex); }
 }
}
