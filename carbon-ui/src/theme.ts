import { useEffect, useState } from "react"

// Carbon's look: a color theme (which tints every surface, not just buttons), an accent you can change, a new tab
// background (moving, a picture or a video) and the new tab widgets. Kept in this profile's interface storage and
// shared by every Carbon window, which follow each other's changes.

export type ThemeId = "carbon" | "midnight" | "aurora" | "ember" | "sakura" | "forest" | "neon" | "gold" | "classic"
export type Backdrop = "none" | "aurora" | "stars" | "waves" | "grid" | "bubbles" | "image"
export type WidgetId = "clock" | "links" | "notes" | "todo" | "timer" | "quote"
export type Look = { theme: ThemeId; accent: string; backdrop: Backdrop; image: string; dim: number; widgets: WidgetId[]; glass: boolean }

// acc: the accent; acc2: the second color of its gradients; h: the hue that tints the surfaces.
export const THEMES: { id: ThemeId; name: string; acc: string; acc2: string; h: number }[] = [
 { id: "carbon", name: "Carbon", acc: "#7c6cff", acc2: "#22d3ee", h: 275 },
 { id: "midnight", name: "Midnight", acc: "#4f8cff", acc2: "#a78bfa", h: 245 },
 { id: "aurora", name: "Aurora", acc: "#2dd4bf", acc2: "#a3e635", h: 185 },
 { id: "ember", name: "Ember", acc: "#ff7a45", acc2: "#ffcc33", h: 35 },
 { id: "sakura", name: "Sakura", acc: "#ff6fa8", acc2: "#c084fc", h: 340 },
 { id: "forest", name: "Forest", acc: "#34c77b", acc2: "#b5e655", h: 150 },
 { id: "neon", name: "Neon", acc: "#ff2bd6", acc2: "#00e5ff", h: 305 },
 { id: "gold", name: "Gold", acc: "#f5b83d", acc2: "#ff7a45", h: 70 },
 { id: "classic", name: "Classic", acc: "#8e8e95", acc2: "#c9c9cf", h: 0 },
]
export const BACKDROPS: { id: Backdrop; name: string }[] = [
 { id: "aurora", name: "Aurora" }, { id: "stars", name: "Stars" }, { id: "waves", name: "Waves" }, { id: "grid", name: "Grid" },
 { id: "bubbles", name: "Bubbles" }, { id: "image", name: "Your picture or video" }, { id: "none", name: "None" },
]
export const WIDGETS: { id: WidgetId; name: string }[] = [
 { id: "clock", name: "Clock" }, { id: "links", name: "Quick links" }, { id: "notes", name: "Notes" },
 { id: "todo", name: "To-do" }, { id: "timer", name: "Focus timer" }, { id: "quote", name: "Quote of the day" },
]
export const DEFAULT_LOOK: Look = { theme: "carbon", accent: "", backdrop: "aurora", image: "", dim: 35, widgets: ["clock", "links", "todo", "timer"], glass: true }

const KEY = "carbon.look"
export function readLook(): Look {
 try { const saved = JSON.parse(localStorage.getItem(KEY) ?? "null"); if (saved && typeof saved === "object") return { ...DEFAULT_LOOK, ...saved } } catch { /* no storage, or a broken value: the default look */ }
 return DEFAULT_LOOK
}
export function saveLook(look: Look) {
 try { localStorage.setItem(KEY, JSON.stringify(look)) } catch { /* no storage: this window only */ }
 applyLook(look); dispatchEvent(new CustomEvent("carbon-look", { detail: look }))
}

/// The hue of a color, for tinting the surfaces to match a custom accent.
export function hueOf(hex: string): number {
 const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim()); if (!m) return 275
 const n = parseInt(m[1], 16), r = (n >> 16 & 255) / 255, g = (n >> 8 & 255) / 255, b = (n & 255) / 255
 const max = Math.max(r, g, b), min = Math.min(r, g, b), d = max - min
 if (!d) return 0
 const h = max === r ? ((g - b) / d) % 6 : max === g ? (b - r) / d + 2 : (r - g) / d + 4
 return Math.round((h * 60 + 360) % 360)
}

export function applyLook(look: Look) {
 const root = document.documentElement, t = THEMES.find(x => x.id === look.theme) ?? THEMES[0]
 if (t.id === "classic" && !look.accent) { delete root.dataset.carbon; root.style.removeProperty("--acc"); root.style.removeProperty("--acc2"); root.style.removeProperty("--h") }
 else {
  root.dataset.carbon = t.id
  const acc = /^#[0-9a-f]{6}$/i.test(look.accent) ? look.accent : t.acc
  root.style.setProperty("--acc", acc)
  root.style.setProperty("--acc2", look.accent ? `oklch(from ${acc} l c calc(h + 55))` : t.acc2)
  root.style.setProperty("--h", String(look.accent ? hueOf(acc) : t.h))
 }
 root.dataset.glass = look.glass ? "on" : "off"
}

export function useLook(): [Look, (change: Partial<Look>) => void] {
 const [look, setLook] = useState(readLook)
 useEffect(() => {
  const mine = (e: Event) => setLook((e as CustomEvent<Look>).detail)
  const other = (e: StorageEvent) => { if (e.key === KEY) { const l = readLook(); applyLook(l); setLook(l) } } // another Carbon window changed it
  addEventListener("carbon-look", mine); addEventListener("storage", other)
  return () => { removeEventListener("carbon-look", mine); removeEventListener("storage", other) }
 }, [])
 return [look, change => saveLook({ ...readLook(), ...change })]
}

applyLook(readLook())
