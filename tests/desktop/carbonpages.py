"""carbon:// addresses open Carbon's own pages: typed in the address bar, carbon://chats (and Carbon://Chat History, any case,
spaces as dashes) opens the chat history on the home page, carbon://companions the companions, carbon://settings the
Settings panel; typing carbon:// suggests the pages; an unknown one says so instead of searching the web.

Run: python tests/desktop/carbonpages.py artifacts/Carbon-QA/Carbon.exe
Out: artifacts/carbonpages-*/result.json
"""
import json
from pathlib import Path
import sys
import time
import uuid

sys.path.insert(0, str(Path(__file__).parent))
from qa import ROOT, js, launch, rpc, until  # noqa: E402

EXE = Path(sys.argv[1]).resolve()
RUN = ROOT / "artifacts" / ("carbonpages-" + uuid.uuid4().hex[:8])
CHECKS = []
# Types into the address bar as a person would: focus, then the text a moment later, so the interface's own state sees it.
# The QA window isn't the focused one, so the focus event is sent as well.
FOCUS = "(el => { el.focus(); el.dispatchEvent(new FocusEvent('focusin', { bubbles: true })); return 1 })(document.querySelector('.address-input'))"
TYPE = "(el => { Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(el, %s); el.dispatchEvent(new Event('input', { bubbles: true })); return 1 })(document.querySelector('.address-input'))"
ENTER = "document.querySelector('.address-input').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true })), 1"


def check(ok, name):
    CHECKS.append({"name": name, "passed": bool(ok)}); print(("PASS " if ok else "FAIL ") + name)


def type_in(process, text):
    js(process, FOCUS)
    time.sleep(.4)
    js(process, TYPE % json.dumps(text))


def go(process, text):
    type_in(process, text)
    time.sleep(.6)
    js(process, ENTER)
    time.sleep(1)


profile = RUN / "profile"; profile.mkdir(parents=True)
(profile / "state.json").write_text(json.dumps({"UiVersion": 6, "Welcome": False, "Preferences": {"Width": 1280, "Height": 800}}))
p = launch(EXE, profile)
try:
    shown = lambda label: js(p, f"!!document.querySelector('section.home-page[aria-label=\"{label}\"]')")
    tabs = lambda: len(rpc(p).get("tabs", []))
    before = tabs()
    type_in(p, "carbon://")
    listed = lambda: js(p, "JSON.stringify([...document.querySelectorAll('.address-option')].map(o => o.innerText))")
    check(until(lambda: all(f"carbon://{n}" in listed() for n in ("chats", "companions", "settings")), 5), "typing carbon:// suggests Carbon's pages")
    js(p, "document.querySelector('.address-input').blur()"); time.sleep(.4)
    go(p, "carbon://chats")
    check(until(lambda: shown("Chat History"), 5), "carbon://chats opens the chat history")
    go(p, "Carbon://Chat History")
    check(until(lambda: shown("Chat History"), 5) and tabs() == before, "Carbon://Chat History does too, in the same new tab")
    go(p, "carbon://companions")
    check(until(lambda: shown("Companions"), 5), "carbon://companions opens the companions")
    for url, label in (("carbon://skills", "Skills"), ("carbon://plugins", "Plugins"), ("carbon://files", "Files"), ("carbon://library", "Files")):
        go(p, url)
        check(until(lambda: shown(label), 5), f"{url} opens {label}")
    go(p, "carbon://settings")
    check(until(lambda: "Changes save as you make them." in js(p, "document.body.innerText"), 5), "carbon://settings opens Settings")
    js(p, "document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true })), 1"); rpc(p, "key", key="Escape"); time.sleep(.6)
    go(p, "carbon://nowhere")
    toasts = lambda: js(p, "JSON.stringify([...document.querySelectorAll('[data-sonner-toast]')].map(t => t.innerText))")
    check(until(lambda: "Carbon has no page called carbon://nowhere" in toasts(), 5)
          and not any("duckduckgo" in (t.get("url") or "") or "google" in (t.get("url") or "") for t in rpc(p).get("tabs", [])), "an unknown carbon:// page says so instead of searching")
finally:
    p.kill()
(RUN / "result.json").write_text(json.dumps(CHECKS, indent=1))
print(f"{sum(c['passed'] for c in CHECKS)}/{len(CHECKS)} passed")
sys.exit(0 if all(c["passed"] for c in CHECKS) else 1)
