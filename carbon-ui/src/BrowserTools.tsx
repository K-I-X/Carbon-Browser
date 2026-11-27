import { useState } from "react"
import { Trash2, Puzzle, FolderOpen, ExternalLink, RotateCw, Settings2, ShieldCheck, Cookie, Search, PanelTop } from "lucide-react"
import type { CSSProperties } from "react"
import { Button } from "@/components/ui/button"
import { MAC } from "@/lib/utils"
import { Input } from "@/components/ui/input"
import { Switch } from "@/components/ui/switch"
import { ScrollArea } from "@/components/ui/scroll-area"
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "@/components/ui/select"
import { Separator } from "@/components/ui/separator"
import { AlertDialog, AlertDialogContent, AlertDialogHeader, AlertDialogTitle, AlertDialogDescription, AlertDialogFooter, AlertDialogCancel, AlertDialogAction } from "@/components/ui/alert-dialog"
import { DataTools } from "./DataTools"
import { PasswordsPane, type LoginOffer } from "./Passwords"
import { destroy, isGone, useGone } from "./Undo"
type Send = (op:string,payload?:Record<string,unknown>)=>void
export type ToolData={site?:string;origin?:string;states?:Record<string,string>;inUse?:string;off?:string[];external?:{busy:boolean;error:string;done:string;profile:string;blocked?:string;sources:{id:string;name:string}[];preview?:{id:string;name:string;bookmarks:number;history:number;passwords:number;cookies:number;cookiesLocked:boolean;skipped:number;warnings:string[];picked?:boolean;sites?:string[]}};error?:string;vault?:{id:string;origin:string;username:string;weak:boolean;reused:boolean}[];never?:string[];cookies?:{id:string;name:string;domain:string;path:string;secure:boolean;httpOnly:boolean;session:boolean}[];extensions?:{id:string;name:string;enabled:boolean;hasPage:boolean;hasPopup?:boolean;icon?:string|null;}[];candidate?:{name:string;permissions:string[];desktopCompanion?:boolean};permissions?:{origin:string;kind:string;state:string}[];engine?:string;isPrivate?:boolean;profiles?:{id:string;name:string;current:boolean;launch:boolean;permanent:boolean;running:boolean}[];preview?:{id:string;file:string;kind:string;logins:number;bookmarks:number;history:number;tabs:number;skipped:number};offer?:LoginOffer;passwordOptions?:{save:boolean;fill:boolean};downloading?:boolean}
export function BrowserTools({pane,data,send,url,tracking,memory,autofill}:{pane:string;data:ToolData;send:Send;url:string;tracking:string;memory:boolean;autofill:boolean}){
 const [filter,setFilter]=useState("")
 const [confirmation,setConfirmation]=useState<{title:string;body:string;run:()=>void}|null>(null)
 const pref=(key:string,value:string)=>send("preference",{key,value})
 const refresh=()=>send("refreshTools",{name:pane})
 const ask=(title:string,body:string,run:()=>void)=>setConfirmation({title,body,run})
 useGone()
 return <div className="browser-tools">
  {data.error&&<p className="tool-error" role="alert">{data.error}</p>}
  {(pane==="profiles"||pane==="import")&&<DataTools pane={pane} data={data} send={send}/>}
  {pane==="passwords"&&<PasswordsPane data={data} send={send} ask={ask} url={url}/>}
  {pane==="extensions"&&<Extensions data={data} send={send} refresh={refresh}/>}
  {pane==="cookies"&&<>
   <div className="tool-intro"><Cookie/><p>{data.isPrivate?"Cookies in your private session.":"Cookies saved by websites in your regular profile."} Cookie values stay inside the browser engine.</p></div>
   <div className="tool-toolbar"><Input aria-label="Search cookies" placeholder="Find a website or cookie" value={filter} onChange={e=>setFilter(e.target.value)}/><Button variant="ghost" size="icon-sm" aria-label="Refresh cookies" onClick={refresh}><RotateCw/></Button></div>
   <ScrollArea className="tools-list">{data.cookies?.filter(c=>!isGone("cookie:"+c.id)&&(c.domain+c.name).toLowerCase().includes(filter.toLowerCase())).map(c=><div className="tool-row" key={c.id}><Cookie/><div className="tool-copy"><strong>{c.domain}</strong><small>{c.name} · {c.httpOnly?"HTTP only · ":""}{c.secure?"Secure":"HTTP"}{c.session?" · Session":""}</small></div><Button variant="ghost" size="icon-sm" aria-label={"Delete cookie "+c.name} onClick={()=>destroy({key:"cookie:"+c.id,title:"Delete this cookie?",body:`${c.name} from ${c.domain} is removed. You may be signed out of that site.`,action:"Delete cookie",done:"Cookie deleted",run:()=>send("cookieDelete",{id:c.id})})}><Trash2/></Button></div>)}{data.cookies?.length===0&&<p className="empty-state">No cookies in this profile.</p>}</ScrollArea>
  </>}
  {pane==="security"&&<>
   <div className="tool-intro"><ShieldCheck/><p>Invalid certificates are blocked. {MAC ? "WebKit warns you before fraudulent websites, and tracking protection is always on." : "Microsoft reputation checks are requested; Windows policy can override them."}</p></div>
   <div className="setting-row"><div><strong>Tracking prevention</strong><p>Strict protection can affect some websites.</p></div><Select value={tracking} onValueChange={v=>pref("tracking",v)}><SelectTrigger className="w-32"><SelectValue/></SelectTrigger><SelectContent>{["Basic","Balanced","Strict"].map(v=><SelectItem key={v} value={v}>{v}</SelectItem>)}</SelectContent></Select></div>
   <Separator/><div className="setting-row"><div><strong>Reduce background memory use</strong><p>Sleep inactive tabs after a minute while keeping page state. Media, calls and downloads stay awake.</p></div><Switch aria-label="Memory saver" checked={memory} onCheckedChange={v=>pref("memory",String(v))}/></div>
   <Separator/><div className="setting-row"><div><strong>Form autofill</strong><p>Names, addresses, and other non-password fields.</p></div><Switch aria-label="Form autofill" checked={autofill} onCheckedChange={v=>pref("autofill",String(v))}/></div>
   <Separator/><div className="tool-section-label">Website permissions</div><ScrollArea className="permissions-scroll">{data.permissions?.map(p=><div className="tool-row" key={p.origin+p.kind}><div className="tool-copy"><strong>{p.origin}</strong><small>{p.kind} · {p.state}</small></div><Button variant="outline" size="sm" onClick={()=>send("permissionReset",{origin:p.origin,kind:p.kind})}>Reset</Button></div>)}{data.permissions?.length===0&&<p className="tool-caption">No saved permission decisions.</p>}</ScrollArea><p className="tool-caption">WebView2 {data.engine}</p>
  </>}
  <AlertDialog open={!!confirmation} onOpenChange={v=>{if(!v)setConfirmation(null)}}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>{confirmation?.title}</AlertDialogTitle><AlertDialogDescription>{confirmation?.body}</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>Cancel</AlertDialogCancel><AlertDialogAction onClick={()=>{confirmation?.run();setConfirmation(null)}}>Confirm</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
 </div>
}

// Popular extensions people add first: each opens its page in the Chrome Web Store, where Add to Carbon installs it after
// you review what it can do.
const PICKS:[string,string][]=[["Dark Reader","Dark mode for every website"],["Grammarly","Writing help as you type"],["Google Translate","Translate words and pages"],["Bitwarden","Password manager"],["Google Docs Offline","Docs without the internet"],["Read Aloud","Reads pages out loud"],["SponsorBlock","Skips sponsor segments on YouTube"],["Return YouTube Dislike","Shows dislikes again"]]
const hueOfName=(s:string)=>[...s].reduce((h,c)=>(h*31+c.charCodeAt(0))%360,7)
/// Extensions: what's installed as cards with their own icons, on and off, popup, options and remove; and where to get more.
function Extensions({data,send,refresh}:{data:ToolData;send:Send;refresh:()=>void}){
 const [find,setFind]=useState("")
 const list=(data.extensions??[]).filter(e=>!isGone("ext:"+e.id)&&e.name.toLowerCase().includes(find.trim().toLowerCase()))
 const all=(data.extensions??[]).filter(e=>!isGone("ext:"+e.id))
 return <>
  <div className="ext-hero"><Puzzle/><div><b>{all.length?`${all.length} extension${all.length===1?"":"s"}`:"Extensions"}</b><p>Add anything from the Chrome Web Store: open an extension there and press <strong>Add to Carbon</strong>. You see what it can do before it installs.</p></div>
   <div className="ext-hero-actions"><Button size="sm" disabled={data.downloading} onClick={()=>send("openStore")}>{data.downloading?"Downloading…":<><ExternalLink/>Web Store</>}</Button><Button size="sm" variant="ghost" onClick={()=>send("extensionChoose")}><FolderOpen/>Load folder</Button></div></div>
  {data.candidate&&<div className="extension-review"><strong>Add {data.candidate.name}?</strong><p>Declared permissions and website access:</p>{data.candidate.desktopCompanion&&<p className="tool-caption">Requires its desktop companion app. The publisher must support Carbon; installing an extension alone does not prove its desktop features work.</p>}<ScrollArea className="permission-list">{data.candidate.permissions.length?data.candidate.permissions.map(p=><code key={p}>{p}</code>):<span>No permissions requested.</span>}</ScrollArea><div className="tool-toolbar"><Button variant="ghost" onClick={()=>send("extensionCancel")}>Cancel</Button><Button onClick={()=>send("extensionInstall")}>Add extension</Button></div></div>}
  <div className="ext-tools">{all.length>3&&<Input aria-label="Find an extension" placeholder="Find an extension" value={find} onChange={e=>setFind(e.target.value)}/>}<Button variant="ghost" size="icon-sm" aria-label="Refresh extensions" onClick={refresh}><RotateCw/></Button></div>
  <ScrollArea className="tools-list">
   {all.length===0?<div className="ext-empty"><Puzzle/><b>No extensions yet</b><span>Start with one of these, or browse the Web Store.</span></div>
    :<div className="ext-grid">{list.map(e=><article className="ext-card" key={e.id} data-off={!e.enabled||undefined} style={{"--hue":hueOfName(e.name)} as CSSProperties}>
     <div className="ext-card-top"><span className="ext-card-icon">{e.icon?<img src={e.icon} alt=""/>:<Puzzle/>}</span><div className="ext-card-name"><strong title={e.name}>{e.name}</strong><small>{e.enabled?"On":"Off"}</small></div></div>
     <div className="ext-card-bar">
      {e.hasPopup&&<button className="ext-card-action" disabled={!e.enabled} onClick={()=>send("extensionPopup",{id:e.id})}><PanelTop/>Open</button>}
      {e.hasPage&&<button className="ext-card-action" onClick={()=>send("extensionPage",{id:e.id})}><Settings2/>Options</button>}
      <button className="ext-card-action is-danger" aria-label={"Remove "+e.name} onClick={()=>destroy({key:"ext:"+e.id,title:"Remove this extension?",body:e.name+" is removed from your regular profile, with its settings.",action:"Remove extension",done:"Extension removed",run:()=>send("extensionRemove",{id:e.id})})}><Trash2/></button>
      <Switch aria-label={"Enable "+e.name} checked={e.enabled} onCheckedChange={()=>send("extensionToggle",{id:e.id})}/>
     </div>
    </article>)}{list.length===0&&<p className="empty-state">No extensions match.</p>}</div>}
   <div className="ext-picks-title">Popular picks</div>
   <div className="ext-picks">{PICKS.map(([name,what])=><button key={name} className="ext-pick" style={{"--hue":hueOfName(name)} as CSSProperties} onClick={()=>send("new",{url:"https://chromewebstore.google.com/search/"+encodeURIComponent(name)})}><span>{name[0]}</span><span><b>{name}</b><small>{what}</small></span></button>)}
    <button className="ext-pick" style={{"--hue":250} as CSSProperties} onClick={()=>send("openStore")}><span><Search size={14}/></span><span><b>Browse all</b><small>The whole Chrome Web Store</small></span></button></div>
  </ScrollArea>
 </>
}
