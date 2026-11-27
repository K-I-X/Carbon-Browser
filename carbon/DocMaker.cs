using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
namespace Carbon;

/// Makes real documents from what an AI writes in Markdown: a Word document (.docx, which Word, Google Docs and LibreOffice
/// open) or a web page (.html). Headings, paragraphs, bullet and numbered lists, quotes, tables, **bold**, *italic* and
/// `code` come through; anything else stays as its text. No outside library: a .docx is a zip of a few XML files.
static partial class DocMaker
{
 enum Kind { Para, H1, H2, H3, Bullet, Number, Quote, Code, Table }
 sealed record Block(Kind Kind, string Text, List<string[]>? Rows = null);

 static List<Block> Parse(string markdown)
 {
  var blocks = new List<Block>();
  var lines = markdown.Replace("\r\n", "\n").Split('\n');
  var para = new List<string>();
  void Flush() { if (para.Count > 0) { blocks.Add(new(Kind.Para, string.Join(" ", para).Trim())); para.Clear(); } }
  for (int i = 0; i < lines.Length; i++) {
   string line = lines[i], t = line.Trim();
   if (t.StartsWith("```")) {
    Flush(); var code = new List<string>();
    for (i++; i < lines.Length && !lines[i].Trim().StartsWith("```"); i++) code.Add(lines[i]);
    blocks.Add(new(Kind.Code, string.Join("\n", code))); continue;
   }
   if (t.Length == 0) { Flush(); continue; }
   if (t.StartsWith('|') && t.EndsWith('|')) {
    Flush(); var rows = new List<string[]>();
    for (; i < lines.Length && lines[i].Trim() is var r && r.StartsWith('|'); i++)
     if (!Divider().IsMatch(r)) rows.Add([.. r.Trim('|').Split('|').Select(c => c.Trim())]);
    i--; blocks.Add(new(Kind.Table, "", rows)); continue;
   }
   var h = Heading().Match(t);
   if (h.Success) { Flush(); blocks.Add(new(h.Groups[1].Length switch { 1 => Kind.H1, 2 => Kind.H2, _ => Kind.H3 }, h.Groups[2].Value)); continue; }
   var b = BulletLine().Match(line);
   if (b.Success) { Flush(); blocks.Add(new(Kind.Bullet, b.Groups[1].Value)); continue; }
   var n = NumberLine().Match(line);
   if (n.Success) { Flush(); blocks.Add(new(Kind.Number, n.Groups[1].Value)); continue; }
   if (t.StartsWith('>')) { Flush(); blocks.Add(new(Kind.Quote, t.TrimStart('>').Trim())); continue; }
   para.Add(t);
  }
  Flush();
  return blocks;
 }

 // Inline: **bold**, *italic* / _italic_, `code`, [text](link) keeps its text.
 static IEnumerable<(string Text, bool Bold, bool Italic, bool Code)> Runs(string text)
 {
  text = Link().Replace(text, "$1");
  int at = 0;
  foreach (Match m in Inline().Matches(text)) {
   if (m.Index > at) yield return (text[at..m.Index], false, false, false);
   if (m.Groups[1].Success) yield return (m.Groups[1].Value, true, false, false);
   else if (m.Groups[2].Success) yield return (m.Groups[2].Value, false, true, false);
   else if (m.Groups[3].Success) yield return (m.Groups[3].Value, false, true, false);
   else yield return (m.Groups[4].Value, false, false, true);
   at = m.Index + m.Length;
  }
  if (at < text.Length) yield return (text[at..], false, false, false);
 }

 public static string Html(string content, string title)
 {
  if (content.TrimStart().StartsWith('<')) return content; // already a page
  string E(string s) => WebUtility.HtmlEncode(s);
  string Inl(string s) => string.Concat(Runs(s).Select(r => r.Bold ? $"<strong>{E(r.Text)}</strong>" : r.Italic ? $"<em>{E(r.Text)}</em>" : r.Code ? $"<code>{E(r.Text)}</code>" : E(r.Text)));
  var body = new StringBuilder();
  string? list = null;
  foreach (var b in Parse(content)) {
   string want = b.Kind == Kind.Bullet ? "ul" : b.Kind == Kind.Number ? "ol" : "";
   if (list != null && list != want) { body.Append($"</{list}>\n"); list = null; }
   if (want.Length > 0 && list == null) { body.Append($"<{want}>\n"); list = want; }
   body.Append(b.Kind switch {
    Kind.H1 => $"<h1>{Inl(b.Text)}</h1>\n", Kind.H2 => $"<h2>{Inl(b.Text)}</h2>\n", Kind.H3 => $"<h3>{Inl(b.Text)}</h3>\n",
    Kind.Bullet or Kind.Number => $"<li>{Inl(b.Text)}</li>\n", Kind.Quote => $"<blockquote>{Inl(b.Text)}</blockquote>\n",
    Kind.Code => $"<pre><code>{E(b.Text)}</code></pre>\n",
    Kind.Table => "<table>\n" + string.Concat(b.Rows!.Select((r, i) => "<tr>" + string.Concat(r.Select(c => i == 0 ? $"<th>{Inl(c)}</th>" : $"<td>{Inl(c)}</td>")) + "</tr>\n")) + "</table>\n",
    _ => $"<p>{Inl(b.Text)}</p>\n" });
  }
  if (list != null) body.Append($"</{list}>\n");
  return $$"""
   <!doctype html>
   <html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>{{E(title)}}</title>
   <style>body{font:16px/1.6 system-ui,Segoe UI,sans-serif;max-width:760px;margin:40px auto;padding:0 20px;color:#1d1d1f}h1,h2,h3{line-height:1.25}
   table{border-collapse:collapse;width:100%}td,th{border:1px solid #ccc;padding:6px 10px;text-align:left}th{background:#f3f3f3}
   blockquote{border-left:3px solid #ccc;margin:0;padding-left:14px;color:#555}pre{background:#f6f6f6;padding:12px;overflow:auto}</style></head>
   <body>
   {{body}}</body></html>
   """;
 }

 public static byte[] Docx(string content, string title)
 {
  string X(string s) => SecurityElement(s);
  string Run((string Text, bool Bold, bool Italic, bool Code) r) =>
   "<w:r><w:rPr>" + (r.Bold ? "<w:b/>" : "") + (r.Italic ? "<w:i/>" : "") + (r.Code ? "<w:rFonts w:ascii=\"Consolas\" w:hAnsi=\"Consolas\"/>" : "") + "</w:rPr><w:t xml:space=\"preserve\">" + X(r.Text) + "</w:t></w:r>";
  string P(string style, string text, string prefix = "") =>
   "<w:p>" + (style.Length > 0 ? $"<w:pPr><w:pStyle w:val=\"{style}\"/></w:pPr>" : "") + (prefix.Length > 0 ? Run((prefix, false, false, false)) : "") + string.Concat(Runs(text).Select(Run)) + "</w:p>";
  var body = new StringBuilder();
  int number = 0;
  foreach (var b in Parse(content)) {
   number = b.Kind == Kind.Number ? number + 1 : 0;
   body.Append(b.Kind switch {
    Kind.H1 => P("Heading1", b.Text), Kind.H2 => P("Heading2", b.Text), Kind.H3 => P("Heading3", b.Text),
    Kind.Bullet => P("ListParagraph", b.Text, "•\t"), Kind.Number => P("ListParagraph", b.Text, number + ".\t"),
    Kind.Quote => P("Quote", b.Text),
    Kind.Code => string.Concat(b.Text.Split('\n').Select(l => "<w:p><w:pPr><w:pStyle w:val=\"Code\"/></w:pPr>" + Run((l, false, false, true)) + "</w:p>")),
    Kind.Table => "<w:tbl><w:tblPr><w:tblStyle w:val=\"TableGrid\"/><w:tblW w:w=\"0\" w:type=\"auto\"/></w:tblPr>"
     + "<w:tblGrid>" + string.Concat(Enumerable.Repeat($"<w:gridCol w:w=\"{9360 / Math.Max(1, b.Rows!.Max(r => r.Length))}\"/>", Math.Max(1, b.Rows!.Max(r => r.Length)))) + "</w:tblGrid>"
     + string.Concat(b.Rows!.Select((r, i) => "<w:tr>" + string.Concat(r.Select(c => "<w:tc><w:p>" + string.Concat(Runs(c).Select(x => Run(i == 0 ? (x.Text, true, x.Italic, x.Code) : x))) + "</w:p></w:tc>")) + "</w:tr>")) + "</w:tbl><w:p/>",
    _ => P("", b.Text) });
  }
  const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
  string document = $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:document xmlns:w=\"{W}\"><w:body>{body}<w:sectPr><w:pgSz w:w=\"12240\" w:h=\"15840\"/><w:pgMar w:top=\"1440\" w:right=\"1440\" w:bottom=\"1440\" w:left=\"1440\" w:header=\"720\" w:footer=\"720\" w:gutter=\"0\"/></w:sectPr></w:body></w:document>";
  string Style(string id, string name, string run, string para = "") => $"<w:style w:type=\"paragraph\" w:styleId=\"{id}\"><w:name w:val=\"{name}\"/><w:basedOn w:val=\"Normal\"/><w:qFormat/><w:pPr>{para}</w:pPr><w:rPr>{run}</w:rPr></w:style>";
  string styles = $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:styles xmlns:w=\"{W}\">"
   + "<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Calibri\" w:hAnsi=\"Calibri\" w:cs=\"Calibri\"/><w:sz w:val=\"24\"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after=\"160\" w:line=\"276\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>"
   + "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/><w:qFormat/></w:style>"
   + Style("Heading1", "heading 1", "<w:b/><w:sz w:val=\"36\"/><w:color w:val=\"1F2937\"/>", "<w:keepNext/><w:spacing w:before=\"360\" w:after=\"120\"/><w:outlineLvl w:val=\"0\"/>")
   + Style("Heading2", "heading 2", "<w:b/><w:sz w:val=\"30\"/><w:color w:val=\"1F2937\"/>", "<w:keepNext/><w:spacing w:before=\"280\" w:after=\"100\"/><w:outlineLvl w:val=\"1\"/>")
   + Style("Heading3", "heading 3", "<w:b/><w:sz w:val=\"26\"/>", "<w:keepNext/><w:spacing w:before=\"220\" w:after=\"80\"/><w:outlineLvl w:val=\"2\"/>")
   + Style("ListParagraph", "List Paragraph", "", "<w:spacing w:after=\"60\"/><w:ind w:left=\"720\" w:hanging=\"360\"/>")
   + Style("Quote", "Quote", "<w:i/><w:color w:val=\"555555\"/>", "<w:ind w:left=\"720\"/>")
   + Style("Code", "Code", "<w:rFonts w:ascii=\"Consolas\" w:hAnsi=\"Consolas\"/><w:sz w:val=\"20\"/>", "<w:spacing w:after=\"0\"/>")
   + "<w:style w:type=\"table\" w:styleId=\"TableGrid\"><w:name w:val=\"Table Grid\"/><w:tblPr><w:tblBorders><w:top w:val=\"single\" w:sz=\"4\" w:color=\"BFBFBF\"/><w:left w:val=\"single\" w:sz=\"4\" w:color=\"BFBFBF\"/><w:bottom w:val=\"single\" w:sz=\"4\" w:color=\"BFBFBF\"/><w:right w:val=\"single\" w:sz=\"4\" w:color=\"BFBFBF\"/><w:insideH w:val=\"single\" w:sz=\"4\" w:color=\"BFBFBF\"/><w:insideV w:val=\"single\" w:sz=\"4\" w:color=\"BFBFBF\"/></w:tblBorders><w:tblCellMar><w:left w:w=\"108\" w:type=\"dxa\"/><w:right w:w=\"108\" w:type=\"dxa\"/></w:tblCellMar></w:tblPr></w:style>"
   + "</w:styles>";
  string core = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:title>" + X(title) + "</dc:title><dc:creator>Carbon AI</dc:creator></cp:coreProperties>";
  using var ms = new MemoryStream();
  using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true)) {
   void Add(string name, string text) { using var w = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false)); w.Write(text); }
   Add("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/><Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/><Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/></Types>");
   Add("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/></Relationships>");
   Add("word/_rels/document.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
   Add("word/document.xml", document);
   Add("word/styles.xml", styles);
   Add("docProps/core.xml", core);
  }
  return ms.ToArray();
 }
 static string SecurityElement(string s) => System.Security.SecurityElement.Escape(InvalidXml().Replace(s, "")) ?? "";

 [GeneratedRegex(@"^(#{1,6})\s+(.*)$")] private static partial Regex Heading();
 [GeneratedRegex(@"^\s*[-*+]\s+(.*)$")] private static partial Regex BulletLine();
 [GeneratedRegex(@"^\s*\d+[.)]\s+(.*)$")] private static partial Regex NumberLine();
 [GeneratedRegex(@"^\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?$")] private static partial Regex Divider();
 [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)")] private static partial Regex Link();
 [GeneratedRegex(@"\*\*(.+?)\*\*|(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])|(?<!\w)_(?!\s)(.+?)(?<!\s)_(?!\w)|`([^`]+)`")] private static partial Regex Inline();
 [GeneratedRegex(@"[\x00-\x08\x0B\x0C\x0E-\x1F]")] private static partial Regex InvalidXml();
}
