import { useEffect, useMemo, useRef, useState, type CSSProperties, type ReactNode } from "react"
import { Check, Pause, Play, Plus, RotateCcw, Settings2, X } from "lucide-react"
import { useLook, WIDGETS, type Look, type WidgetId } from "./theme"

// The new tab page's backdrop and widgets (from Bullet): a clock with a greeting, quick links, notes, a to-do list, a
// focus timer and a quote of the day. What you write in them stays in this profile on this computer.

type Send = (op: string, payload?: Record<string, unknown>) => void
const read = <T,>(key: string, fallback: T): T => { try { const v = localStorage.getItem("carbon.w." + key); return v ? JSON.parse(v) as T : fallback } catch { return fallback } }
const write = (key: string, value: unknown) => { try { localStorage.setItem("carbon.w." + key, JSON.stringify(value)) } catch { /* no storage: kept until the window closes */ } }
function useStored<T>(key: string, fallback: T): [T, (v: T) => void] {
 const [v, setV] = useState<T>(() => read(key, fallback))
 return [v, (next: T) => { setV(next); write(key, next) }]
}

/// Behind the new tab page: moving light, stars, waves, a grid, bubbles, or the user's own picture or video.
export function Backdrop({ look }: { look?: Look }) {
 const [own] = useLook(), l = look ?? own
 if (l.backdrop === "none") return null
 const style = { "--dim": l.dim / 100 } as CSSProperties
 if (l.backdrop === "image") {
  if (!/^https:\/\//i.test(l.image)) return null
  const video = /\.(mp4|webm|ogv)(\?|#|$)/i.test(l.image)
  return <div className="carbon-backdrop is-image" style={style} aria-hidden>
   {video ? <video src={l.image} autoPlay muted loop playsInline /> : <img src={l.image} alt="" referrerPolicy="no-referrer" />}
   <i className="carbon-backdrop-dim" />
  </div>
 }
 return <div className={"carbon-backdrop is-" + l.backdrop} style={style} aria-hidden>
  {l.backdrop === "aurora" && <><b /><b /><b /></>}
  {l.backdrop === "stars" && <Stars />}
  {l.backdrop === "waves" && <svg viewBox="0 0 1440 320" preserveAspectRatio="none">{[0, 1, 2].map(i => <path key={i} d="M0,160 C240,220 480,100 720,160 C960,220 1200,100 1440,160 L1440,320 L0,320 Z" />)}</svg>}
  {l.backdrop === "bubbles" && Array.from({ length: 14 }, (_, i) => <span key={i} style={{ "--i": i } as CSSProperties} />)}
  <i className="carbon-backdrop-dim" />
 </div>
}
function Stars() {
 const stars = useMemo(() => Array.from({ length: 90 }, (_, i) => ({ x: (i * 97.3) % 100, y: (i * 53.7 + (i % 7) * 11) % 100, s: (i % 3) + 1, d: (i % 9) * .7 })), [])
 return <>{stars.map((s, i) => <span key={i} style={{ left: s.x + "%", top: s.y + "%", width: s.s, height: s.s, animationDelay: s.d + "s" } as CSSProperties} />)}</>
}

function Card({ id, title, children, wide }: { id: WidgetId; title: string; children: ReactNode; wide?: boolean }) {
 return <section className="widget" data-widget={id} data-wide={wide || undefined} aria-label={title}><h3>{title}</h3>{children}</section>
}

function Clock() {
 const [now, setNow] = useState(() => new Date())
 useEffect(() => { const t = setInterval(() => setNow(new Date()), 1000); return () => clearInterval(t) }, [])
 return <section className="widget widget-clock" data-widget="clock" aria-label="Clock">
  <time dateTime={now.toISOString()}>{now.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" })}</time>
  <p>{now.toLocaleDateString([], { weekday: "long", month: "long", day: "numeric" })}</p>
 </section>
}

type Link = { title: string; url: string }
const DEFAULT_LINKS: Link[] = [
 { title: "Classroom", url: "https://classroom.google.com" }, { title: "Docs", url: "https://docs.google.com" },
 { title: "Gmail", url: "https://mail.google.com" }, { title: "YouTube", url: "https://www.youtube.com" },
 { title: "Drive", url: "https://drive.google.com" }, { title: "Wikipedia", url: "https://www.wikipedia.org" },
]
function Links({ send }: { send: Send }) {
 const [links, setLinks] = useStored<Link[]>("links", DEFAULT_LINKS)
 const [adding, setAdding] = useState(false), [url, setUrl] = useState("")
 const add = () => {
  let u = url.trim(); if (!u) return; if (!/^https?:\/\//i.test(u)) u = "https://" + u
  try { const host = new URL(u).hostname.replace(/^www\./, ""); setLinks([...links, { title: host.split(".")[0].replace(/^./, c => c.toUpperCase()), url: u }].slice(0, 12)) } catch { return }
  setUrl(""); setAdding(false)
 }
 return <Card id="links" title="Quick links" wide>
  <div className="widget-links">
   {links.map((l, i) => <div key={l.url + i} className="widget-link">
    <button onClick={() => send("navigate", { url: l.url })} title={l.url}><span style={{ "--hue": (l.title.charCodeAt(0) * 37) % 360 } as CSSProperties}>{l.title[0]}</span>{l.title}</button>
    <button className="widget-x" aria-label={"Remove " + l.title} onClick={() => setLinks(links.filter((_, j) => j !== i))}><X /></button>
   </div>)}
   {adding ? <form className="widget-add" onSubmit={e => { e.preventDefault(); add() }}><input autoFocus placeholder="Address, like nytimes.com" value={url} onChange={e => setUrl(e.target.value)} onBlur={() => !url && setAdding(false)} /></form>
    : links.length < 12 && <button className="widget-link-add" onClick={() => setAdding(true)}><Plus />Add</button>}
  </div>
 </Card>
}

function Notes() {
 const [text, setText] = useStored("notes", "")
 return <Card id="notes" title="Notes"><textarea className="widget-notes" placeholder="Jot something down…" value={text} onChange={e => setText(e.target.value)} spellCheck /></Card>
}

type Todo = { id: number; text: string; done: boolean }
function Todos() {
 const [items, setItems] = useStored<Todo[]>("todo", [])
 const [text, setText] = useState("")
 const left = items.filter(i => !i.done).length
 return <Card id="todo" title={items.length ? `To-do · ${left} left` : "To-do"}>
  <form onSubmit={e => { e.preventDefault(); if (text.trim()) { setItems([...items, { id: Date.now(), text: text.trim(), done: false }]); setText("") } }}><input className="widget-input" placeholder="Add a task and press Enter" value={text} onChange={e => setText(e.target.value)} /></form>
  <ul className="widget-todos">{items.map(i => <li key={i.id} data-done={i.done || undefined}>
   <button className="widget-check" aria-label={i.done ? "Mark not done" : "Mark done"} onClick={() => setItems(items.map(x => x.id === i.id ? { ...x, done: !x.done } : x))}>{i.done && <Check />}</button>
   <span>{i.text}</span>
   <button className="widget-x" aria-label={"Delete " + i.text} onClick={() => setItems(items.filter(x => x.id !== i.id))}><X /></button>
  </li>)}</ul>
  {items.some(i => i.done) && <button className="widget-clear" onClick={() => setItems(items.filter(i => !i.done))}>Clear done</button>}
 </Card>
}

function Timer() {
 const [mode, setMode] = useState<"focus" | "break">("focus")
 const total = (mode === "focus" ? 25 : 5) * 60
 const [left, setLeft] = useState(total), [running, setRunning] = useState(false)
 const end = useRef(0)
 useEffect(() => { if (!running) return; end.current = Date.now() + left * 1000
  const t = setInterval(() => {
   const l = Math.max(0, Math.round((end.current - Date.now()) / 1000)); setLeft(l)
   if (l === 0) { setRunning(false); chime(); const next = mode === "focus" ? "break" : "focus"; setMode(next); setLeft((next === "focus" ? 25 : 5) * 60) }
  }, 250)
  return () => clearInterval(t) }, [running]) // eslint-disable-line react-hooks/exhaustive-deps
 const pct = 1 - left / total, r = 38, c = 2 * Math.PI * r
 return <Card id="timer" title={mode === "focus" ? "Focus timer" : "Break"}>
  <div className="widget-timer">
   <svg viewBox="0 0 90 90"><circle cx="45" cy="45" r={r} className="track" /><circle cx="45" cy="45" r={r} className="fill" strokeDasharray={c} strokeDashoffset={c * (1 - pct)} /></svg>
   <b>{String(Math.floor(left / 60)).padStart(2, "0")}:{String(left % 60).padStart(2, "0")}</b>
  </div>
  <div className="widget-row">
   <button className="widget-btn is-main" onClick={() => setRunning(!running)}>{running ? <><Pause />Pause</> : <><Play />{left < total ? "Resume" : "Start"}</>}</button>
   <button className="widget-btn" aria-label="Reset" onClick={() => { setRunning(false); setLeft(total) }}><RotateCcw /></button>
   <button className="widget-btn" onClick={() => { setRunning(false); const m = mode === "focus" ? "break" : "focus"; setMode(m); setLeft((m === "focus" ? 25 : 5) * 60) }}>{mode === "focus" ? "Break" : "Focus"}</button>
  </div>
 </Card>
}
function chime() { try { const a = new AudioContext(); [660, 880, 1320].forEach((f, i) => { const o = a.createOscillator(), g = a.createGain(), t = a.currentTime + i * .18; o.frequency.value = f; g.gain.setValueAtTime(.08, t); g.gain.exponentialRampToValueAtTime(.0001, t + .5); o.connect(g); g.connect(a.destination); o.start(t); o.stop(t + .55) }) } catch { /* no sound */ } }

const QUOTES: [string, string][] = [
 ["The secret of getting ahead is getting started.", "Mark Twain"], ["It always seems impossible until it's done.", "Nelson Mandela"],
 ["Well done is better than well said.", "Benjamin Franklin"], ["Simplicity is the ultimate sophistication.", "Leonardo da Vinci"],
 ["What we know is a drop, what we don't know is an ocean.", "Isaac Newton"], ["Do what you can, with what you have, where you are.", "Theodore Roosevelt"],
 ["The best way out is always through.", "Robert Frost"], ["Quality is not an act, it is a habit.", "Will Durant"],
 ["Act as if what you do makes a difference. It does.", "William James"], ["Little by little, one travels far.", "J. R. R. Tolkien"],
 ["Energy and persistence conquer all things.", "Benjamin Franklin"], ["Whatever you are, be a good one.", "Abraham Lincoln"],
 ["The only way to do great work is to love what you do.", "Steve Jobs"], ["Learning never exhausts the mind.", "Leonardo da Vinci"],
 ["Start where you are. Use what you have. Do what you can.", "Arthur Ashe"], ["You miss 100% of the shots you don't take.", "Wayne Gretzky"],
 ["Nothing will work unless you do.", "Maya Angelou"], ["Courage is grace under pressure.", "Ernest Hemingway"],
 ["The future depends on what you do today.", "Mahatma Gandhi"], ["Small deeds done are better than great deeds planned.", "Peter Marshall"],
 ["Curiosity is the wick in the candle of learning.", "William Arthur Ward"], ["Dream big and dare to fail.", "Norman Vaughan"],
 ["If you can dream it, you can do it.", "Walt Disney"], ["Be curious, not judgmental.", "Walt Whitman"],
]
function Quote() {
 const [day] = useState(() => Math.floor(Date.now() / 864e5)), [q, who] = QUOTES[day % QUOTES.length]
 return <Card id="quote" title="Quote of the day"><blockquote className="widget-quote">“{q}”<cite>{who}</cite></blockquote></Card>
}

/// The widgets the user turned on, in a row under the new tab page's main box. Customize turns them on and off.
export function Widgets({ send }: { send: Send }) {
 const [look, setLook] = useLook()
 const [editing, setEditing] = useState(false)
 const on = (id: WidgetId) => look.widgets.includes(id)
 const toggle = (id: WidgetId) => setLook({ widgets: on(id) ? look.widgets.filter(w => w !== id) : [...look.widgets, id] })
 const shown: Record<WidgetId, ReactNode> = { clock: <Clock key="clock" />, links: <Links key="links" send={send} />, notes: <Notes key="notes" />, todo: <Todos key="todo" />, timer: <Timer key="timer" />, quote: <Quote key="quote" /> }
 return <div className="widgets">
  {on("clock") && shown.clock}
  <div className="widget-grid">{WIDGETS.filter(w => w.id !== "clock" && on(w.id)).map(w => shown[w.id])}</div>
  <div className="widgets-foot">
   <button className="widgets-customize" aria-expanded={editing} onClick={() => setEditing(!editing)}><Settings2 />Customize</button>
   {editing && <div className="widgets-picker" role="group" aria-label="Widgets">{WIDGETS.map(w => <button key={w.id} aria-pressed={on(w.id)} onClick={() => toggle(w.id)}>{on(w.id) && <Check />}{w.name}</button>)}</div>}
  </div>
 </div>
}
