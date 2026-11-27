<div align="center">

# Carbon

**A fast, good-looking browser with a free AI built in. Carbon AI reads the page you're on, types for you, makes documents and gets things done on any website, with no account and nothing to pay.**

[**Download for Windows**](https://github.com/K-I-X/Carbon-Browser/releases/latest/download/Carbon-Setup-x64.exe) · [What's inside](#whats-inside) · [Build it yourself](#build-it-yourself) · [Credits](#credits)

</div>

## Download

**[⬇ Carbon-Setup-x64.exe](https://github.com/K-I-X/Carbon-Browser/releases/latest/download/Carbon-Setup-x64.exe)** for Windows 10 and 11 (64-bit). No admin rights needed.

Carbon isn't code-signed yet, so Windows SmartScreen may warn about an unknown publisher: choose **More info**, then **Run anyway**. Carbon updates itself after that.

## What's inside

### Carbon AI: free, and ready on the first start
- **No plan, no account, no key.** Carbon AI runs on the free models OpenCode offers everyone. Carbon downloads OpenCode's program once in the background (checked against npm's checksum) and keeps it in its own folder.
- **Understands the page at once.** Every message comes with a quick look at the tab you're on: what you selected, the field your cursor is in, the headings and the text. Ask "what's this about?" or "answer question 3" and it already knows.
- **Types for you.** Ask it to write something and it types it straight into the box or document you're in, including Google Docs, Gmail and forms.
- **Makes and edits documents.** It opens a new Google Doc, Sheet, Slides or Form and fills it in, edits text in place, or hands you a real Word file (.docx) or web page.
- **Schoolwork.** It explains step by step, solves problems and shows the work, writes and edits essays and answers, and finds its way around Google Classroom, Forms, Canvas and Schoology. It stops before Turn in or Submit unless you tell it to submit.
- **Knows its way around.** It has know-how for the sites people use most, and a plan for any other site.
- **Streams as it writes.** Replies start right away.
- **Use another AI too.** Claude, ChatGPT, Grok, Gemini, OpenCode and any OpenAI- or Claude-compatible API work as well (from Still).

### Signal Run, when you're offline
The "You're offline" page has a game. Carby, a little carbon ring, runs through the dead zone: jump the static, duck dropped packets, double-jump firewalls and clear the gaps. It has electron combos, close calls, boosts and magnets, three new missions every day, skins you unlock, and a flag where your best run ended. The page also lists the pages you saved for offline reading, and says when you're back online.

### A much more accurate ad blocker
Still's blocker now reads **EasyList, EasyPrivacy and Peter Lowe's list** (refreshed every few days), with full support for their network rules and element hiding. It also **skips YouTube video ads** and hides ad slots. You can turn it off for one site from the Site button.

### Looks and feel
- **9 color themes** (Carbon, Midnight, Aurora, Ember, Sakura, Forest, Neon, Gold, Classic) that tint the whole window, plus any accent color you pick.
- **New tab backgrounds:** aurora, stars, waves, grid and bubbles, or any picture or video from the internet, with a softness slider.
- **New tab widgets** (from Bullet): clock, quick links, notes, to-do, a focus timer and a quote of the day.
- **Extensions, redone:** cards with each extension's icon, on/off, open, options and remove, search, and popular picks from the Chrome Web Store.
- **More alive:** ripples when you click, cards that light up under the pointer, springy hovers, and frosted menus. Reduced-motion settings are respected.
- **Save page for offline** (from Bullet's website saver).

### And everything Still has
Tabs, groups, split view, private windows and profiles, tabs on top or on the side, import from Chrome, Edge, Brave, Opera GX, Vivaldi and Firefox, a password manager, per-site permissions, Chrome Web Store extensions, companions with memory, skills, plugins, and MCP for other AI apps. See [FEATURES.md](FEATURES.md).

There's no VPN.

## Build it yourself

You need Windows, the .NET 10 SDK and Node.js 22.

```powershell
./build.ps1             # builds the interface (carbon-ui) and the app into artifacts/Carbon
./build.ps1 -Installer  # also makes the installer (needs Inno Setup 7)
```

The interface lives in `carbon-ui/` (React). `npm run build` there writes it into `carbon/Shell`, which the app embeds.

**Publishing a version:** raise `<Version>` in `carbon/Carbon.csproj`, commit with `[release]` in the message and push to `main`. The Windows workflow builds the installer and publishes it as release `v<Version>`.

## Credits

Carbon is built from **[Still](https://github.com/carbongotfound/still)** by carbongotfound (AGPL-3.0-or-later). Its themes, new tab widgets and backgrounds, and website saver come from **[Bullet](https://github.com/K-I-X/Bullet-Browser)**. Carbon is released under the same license, the GNU Affero General Public License v3.0 or later (see [LICENSE](LICENSE) and [NOTICE](NOTICE)).
