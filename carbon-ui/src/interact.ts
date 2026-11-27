// Carbon's feel: buttons answer a click with a ripple from where you pressed, and cards light up where the pointer is.
// Pure decoration: nothing here changes what a click does. Off with reduced motion.

const reduce = matchMedia("(prefers-reduced-motion: reduce)")
const RIPPLE = "[data-slot=button], .home-chips button, .home-idea, .speed-dial-tile, .widget-btn, .widget-link > button:first-child, .theme-swatch, .backdrop-chip, .widgets-customize"
const GLOW = ".home-idea, .widget, .mate-card, .theme-swatch, .speed-dial-tile"

// Only with a color theme on: the Classic look (the default) is exactly Still's, effects and all.
const themed = () => !!document.documentElement.dataset.carbon

addEventListener("pointerdown", e => {
 if (reduce.matches || e.button !== 0 || !themed()) return
 const el = (e.target as Element | null)?.closest<HTMLElement>(RIPPLE)
 if (!el || el.matches(":disabled, [aria-disabled=true]")) return
 const r = el.getBoundingClientRect(), size = Math.max(r.width, r.height) * 2.2
 const dot = document.createElement("span")
 dot.className = "carbon-ripple"
 dot.style.cssText = `width:${size}px;height:${size}px;left:${e.clientX - r.left - size / 2}px;top:${e.clientY - r.top - size / 2}px`
 if (getComputedStyle(el).position === "static") el.style.position = "relative"
 el.classList.add("has-ripple")
 el.appendChild(dot)
 dot.addEventListener("animationend", () => dot.remove(), { once: true })
}, { passive: true })

let frame = 0, last: PointerEvent | null = null
addEventListener("pointermove", e => {
 if (reduce.matches || !themed()) return
 last = e
 if (frame) return
 frame = requestAnimationFrame(() => {
  frame = 0
  const el = (last?.target as Element | null)?.closest<HTMLElement>(GLOW)
  if (!el || !last) return
  const r = el.getBoundingClientRect()
  el.style.setProperty("--mx", `${last.clientX - r.left}px`)
  el.style.setProperty("--my", `${last.clientY - r.top}px`)
 })
}, { passive: true })
