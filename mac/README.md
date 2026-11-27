# Carbon for Mac

Carbon for Mac runs the same interface as Windows on Apple's WebKit, in a native AppKit window. It needs macOS 12 or later and runs natively on Apple silicon and Intel Macs.

## Install

1. Open **Carbon-&lt;version&gt;.dmg** and drag **Carbon** onto **Applications**.
2. Carbon isn't notarized yet, so macOS asks once the first time you open it:
   - Right-click Carbon in Applications and choose **Open**, then **Open** again.
   - If macOS only offers **Done** or **Move to Trash** (macOS 15 and later), choose **Done**, go to **System Settings > Privacy & Security**, scroll to the message about Carbon and click **Open Anyway**.

After that, Carbon opens like any other app.

## Connect your AI

MCP and the command line work just as they do on Windows: the same tool names, the same approval prompts and the same safety rules. Your AI never sees private tabs, passwords or cookies, and it asks you before it submits a form or buys anything.

Keep Carbon open, then add it to your AI app. In Claude Code:

```sh
claude mcp add still -- /Applications/Carbon.app/Contents/MacOS/Carbon --mcp
```

In Claude Desktop, Cursor or another MCP app, add this to its MCP settings:

```json
{ "mcpServers": { "carbon": { "command": "/Applications/Carbon.app/Contents/MacOS/Carbon", "args": ["--mcp"] } } }
```

From a terminal or a script, call a tool directly. Say who you are with `as=`, so Carbon can show you who is using it:

```sh
carbon=/Applications/Carbon.app/Contents/MacOS/Carbon
"$carbon" --cli open_tab as=Claude url=example.com
"$carbon" --cli read_page as=Claude
"$carbon" --cli click as=Claude id=7
"$carbon" --cli help        # every tool
```

## Where your data lives

- `~/Library/Application Support/Carbon`: your profile, with bookmarks, history, settings, your AI chats and companions, and `agent.sock`, the private socket MCP and the command line talk through.
- `~/Library/WebKit/com.kix.carbon` and `~/Library/Caches/com.kix.carbon`: website data (cookies, site storage) and caches.

To remove Carbon completely, quit it, move it to the Trash and delete those folders.

## Build from source

On a Mac with Xcode 15 or newer, from the repository:

```sh
sh mac/build.sh            # mac/build/Carbon.app, Carbon-<version>.dmg and Carbon-mac.zip (universal)
ARCH=arm64 sh mac/build.sh # one architecture, faster
```

The build is ad-hoc signed, so the first launch asks the same way as above. The shell, the page scripts and the blocklist come straight from the Windows sources (`carbon/`), so both apps stay in step.

## Tests

`QA=1 sh mac/build.sh` makes a test build in `mac/build/qa/` with the QA channel: a private socket in the test profile (`qa-pipe.txt` holds its path) that the desktop tests use in place of the Windows pipe. It has its own bundle id, so it never touches your real data. Never hand it to anyone.

```sh
sh mac/test.sh             # builds the test app, runs every Mac suite, prints a table
sh mac/test.sh polish      # just one suite
python3 tests/desktop/baseline.py mac/build/qa/Carbon.app/Contents/MacOS/Carbon 1   # start time, idle memory, tab switching
```

Run them while you're logged in to the Mac (in Terminal or over SSH): they open real Carbon windows. Logs go to `artifacts/mac-test/`. `baseline.py` measures memory as the physical footprint (Activity Monitor's Memory column) of Carbon plus the WebKit helpers macOS counts as Carbon's.
