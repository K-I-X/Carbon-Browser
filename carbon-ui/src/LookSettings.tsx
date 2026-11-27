import type { CSSProperties } from "react"
import { Check, RotateCcw } from "lucide-react"
import { Switch } from "@/components/ui/switch"
import { Backdrop } from "./Widgets"
import { BACKDROPS, THEMES, WIDGETS, useLook } from "./theme"

// Settings > Appearance, Carbon's part: the color theme, an accent of your own, frosted menus, the new tab's background
// and its widgets. Every change shows at once, in every window.
export function LookSettings() {
 const [look, setLook] = useLook()
 return <>
  <section className="set-group" aria-label="Color theme"><h3>Color theme</h3><div className="set-card"><div className="set-pad" id="set-colors">
   <div className="theme-swatches" role="radiogroup" aria-label="Color theme">
    {THEMES.map(t => <button key={t.id} role="radio" className="theme-swatch" aria-pressed={look.theme === t.id && !look.accent} aria-checked={look.theme === t.id && !look.accent}
     style={{ "--sw-a": t.acc, "--sw-b": t.acc2 } as CSSProperties} onClick={() => setLook({ theme: t.id, accent: "" })}>
     <i />{t.name}{look.theme === t.id && !look.accent && <Check />}
    </button>)}
   </div>
   <div className="look-row">
    <label htmlFor="carbon-accent">Your own accent</label>
    <input id="carbon-accent" type="color" value={look.accent || THEMES.find(t => t.id === look.theme)?.acc || "#7c6cff"} onChange={e => setLook({ accent: e.target.value, theme: look.theme === "classic" ? "carbon" : look.theme })} />
    {look.accent && <button className="backdrop-chip" onClick={() => setLook({ accent: "" })}><RotateCcw size={12} /> Theme's own</button>}
    <small>Tints the whole window to match.</small>
   </div>
  </div>
  <div className="setting-row" id="set-glass"><div><strong>Frosted menus and dialogs</strong><p>Menus and dialogs let a soft blur of what's behind them show through.</p></div><Switch aria-label="Frosted menus and dialogs" checked={look.glass} onCheckedChange={v => setLook({ glass: v })} /></div>
  </div></section>

  <section className="set-group" aria-label="New tab"><h3>New tab</h3><div className="set-card"><div className="set-pad" id="set-backdrop">
   <strong>Background</strong>
   <div className="backdrop-chips" role="radiogroup" aria-label="New tab background">
    {BACKDROPS.map(b => <button key={b.id} role="radio" className="backdrop-chip" aria-pressed={look.backdrop === b.id} aria-checked={look.backdrop === b.id} onClick={() => setLook({ backdrop: b.id })}>{b.name}</button>)}
   </div>
   {look.backdrop === "image" && <div className="look-row">
    <input type="url" aria-label="Picture or video address" placeholder="https://… a picture (.jpg, .png, .gif, .webp) or a video (.mp4, .webm)" value={look.image} onChange={e => setLook({ image: e.target.value.trim() })} />
    <small>Paste the address of any picture or video on the internet (https).</small>
   </div>}
   {look.backdrop !== "none" && <div className="look-row"><label htmlFor="carbon-dim">Soften</label><input id="carbon-dim" type="range" min={0} max={85} value={look.dim} onChange={e => setLook({ dim: +e.target.value })} /><small>{look.dim}%</small></div>}
   {look.backdrop !== "none" && <div className="backdrop-preview"><Backdrop look={look} /></div>}
  </div>
  <div className="set-pad" id="set-widgets">
   <strong>Widgets</strong>
   <div className="backdrop-chips" role="group" aria-label="New tab widgets">
    {WIDGETS.map(w => { const on = look.widgets.includes(w.id); return <button key={w.id} className="backdrop-chip" aria-pressed={on} onClick={() => setLook({ widgets: on ? look.widgets.filter(x => x !== w.id) : [...look.widgets, w.id] })}>{on && <Check size={12} />} {w.name}</button> })}
   </div>
  </div></div></section>
 </>
}
