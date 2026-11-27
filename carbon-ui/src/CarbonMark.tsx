// Carbon's mark: a rounded tile with a bold, flat C cut out of it, like carbon's square on the periodic table. One color
// (the text's), so it sits in the interface the way Still's two bars did. The live one turns its C while a task runs.
export function CarbonMark({ className = "", live = false }: { className?: string; live?: boolean }) {
 return <span className={"carbon-mark " + className + (live ? " is-live" : "")} aria-hidden>
  <svg viewBox="0 0 24 24" focusable="false">
   <rect x="1.5" y="1.5" width="21" height="21" rx="5.5" fill="currentColor" />
   <path d="M16.1 9.1A5.3 5.3 0 1 0 16.1 15.9" fill="none" stroke="var(--background)" strokeWidth="3.1" />
  </svg>
 </span>
}
