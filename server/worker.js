// Carbon AI's server: a Cloudflare Worker (free plan) that answers Carbon AI in the OpenAI Chat Completions format, the
// format Carbon's agent already speaks (streamed replies, tool calls). It holds the AI keys, so no key is ever inside
// Carbon, and it tries free AI services in turn: when one is busy or out of free use, the next answers at once, and a
// used-up one is skipped for a while. Each service is used only if its key is set (Settings > Variables and Secrets):
//   GEMINI_API_KEY    Google AI Studio (free)            GEMINI_MODEL   (default gemini-flash-latest)
//   GROQ_API_KEY      Groq (free)                         GROQ_MODEL     (default openai/gpt-oss-120b)
//   CEREBRAS_API_KEY  Cerebras (free)                     CEREBRAS_MODEL (default gpt-oss-120b)
//   CF_ACCOUNT_ID + CF_AI_TOKEN  Cloudflare Workers AI (free daily allowance)  CF_MODEL (default @cf/meta/llama-3.3-70b-instruct-fp8-fast)
//   EXTRA_URL + EXTRA_KEY + EXTRA_MODEL  any OpenAI-compatible service, tried last. With a pay-as-you-go key here,
//                     Carbon AI never runs out (the free ones are still used first).
// Optional: PER_MINUTE (requests per minute from one address, default 30).
// Carbon, AGPL-3.0-or-later.

const CORS = { "access-control-allow-origin": "*", "access-control-allow-headers": "authorization, content-type", "access-control-allow-methods": "GET, POST, OPTIONS" };
const resting = new Map(); // service name → time it can be tried again (this worker instance only)
const recent = new Map();  // address → request times in the last minute

function services(env) {
  const list = [];
  if (env.GEMINI_API_KEY) list.push({ name: "gemini", url: "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", key: env.GEMINI_API_KEY, model: env.GEMINI_MODEL || "gemini-flash-latest", gemini: true });
  if (env.GROQ_API_KEY) list.push({ name: "groq", url: "https://api.groq.com/openai/v1/chat/completions", key: env.GROQ_API_KEY, model: env.GROQ_MODEL || "openai/gpt-oss-120b" });
  if (env.CEREBRAS_API_KEY) list.push({ name: "cerebras", url: "https://api.cerebras.ai/v1/chat/completions", key: env.CEREBRAS_API_KEY, model: env.CEREBRAS_MODEL || "gpt-oss-120b" });
  if (env.CF_ACCOUNT_ID && env.CF_AI_TOKEN) list.push({ name: "workers-ai", url: `https://api.cloudflare.com/client/v4/accounts/${env.CF_ACCOUNT_ID}/ai/v1/chat/completions`, key: env.CF_AI_TOKEN, model: env.CF_MODEL || "@cf/meta/llama-3.3-70b-instruct-fp8-fast" });
  if (env.EXTRA_URL && env.EXTRA_KEY && env.EXTRA_MODEL) list.push({ name: "extra", url: env.EXTRA_URL.replace(/\/+$/, "") + (/\/chat\/completions$/.test(env.EXTRA_URL) ? "" : "/chat/completions"), key: env.EXTRA_KEY, model: env.EXTRA_MODEL });
  return list;
}

// Gemini's OpenAI-compatible API takes a smaller set of JSON Schema words in tool parameters.
const SCHEMA_WORDS = new Set(["type", "description", "properties", "required", "items", "enum", "format", "nullable", "minimum", "maximum", "anyOf"]);
function cleanSchema(s) {
  if (Array.isArray(s)) return s.map(cleanSchema);
  if (!s || typeof s !== "object") return s;
  const out = {};
  for (const [k, v] of Object.entries(s)) {
    if (!SCHEMA_WORDS.has(k)) continue;
    if (k === "properties") out.properties = Object.fromEntries(Object.entries(v || {}).map(([p, ps]) => [p, cleanSchema(ps)]));
    else if (k === "type" && Array.isArray(v)) { out.type = v.find(t => t !== "null") || "string"; if (v.includes("null")) out.nullable = true; }
    else if (k === "required") { if (Array.isArray(v) && v.length) out.required = v; }
    else out[k] = cleanSchema(v);
  }
  if (out.type === "object" && !out.properties) out.properties = {};
  return out;
}

function requestFor(service, body) {
  const b = { model: service.model, messages: body.messages, stream: !!body.stream };
  for (const k of ["tools", "tool_choice", "temperature", "top_p", "max_tokens", "stop"]) if (body[k] !== undefined) b[k] = body[k];
  if (b.tools && service.gemini) b.tools = b.tools.map(t => ({ ...t, function: { ...t.function, parameters: cleanSchema(t.function?.parameters || { type: "object" }) } }));
  if (b.stream && !service.gemini) b.stream_options = { include_usage: true };
  return b;
}

const json = (status, obj, extra = {}) => new Response(JSON.stringify(obj), { status, headers: { "content-type": "application/json", ...CORS, ...extra } });
const error = (status, message) => json(status, { error: { message, type: "carbon_ai" } });

async function chat(request, env) {
  let body;
  try { body = await request.json(); } catch { return error(400, "That isn't a chat request."); }
  if (!Array.isArray(body.messages)) return error(400, "That isn't a chat request.");
  const all = services(env);
  if (!all.length) return error(503, "Carbon AI's server has no AI service set up yet. Add a GEMINI_API_KEY (see the server's README).");
  const now = Date.now(), problems = [];
  // Rested services first in order; ones resting go last, in case they're all resting.
  const order = [...all.filter(s => (resting.get(s.name) || 0) <= now), ...all.filter(s => (resting.get(s.name) || 0) > now)];
  for (const service of order) {
    let res;
    try {
      res = await fetch(service.url, { method: "POST", headers: { "content-type": "application/json", authorization: `Bearer ${service.key}` }, body: JSON.stringify(requestFor(service, body)) });
    } catch (e) { problems.push(`${service.name}: ${e.message}`); resting.set(service.name, now + 30_000); continue; }
    if (res.ok) {
      resting.delete(service.name);
      const headers = { "content-type": res.headers.get("content-type") || (body.stream ? "text/event-stream" : "application/json"), "cache-control": "no-cache", "x-carbon-ai": service.name, ...CORS };
      return new Response(res.body, { status: 200, headers });
    }
    const text = (await res.text()).slice(0, 300);
    problems.push(`${service.name} ${res.status}: ${text}`);
    // Out of free use or busy: rest it (longer for a daily limit). Bad request: likely this service's quirk, try the next.
    const daily = /day|daily|quota|exhaust|free usage/i.test(text);
    resting.set(service.name, now + (res.status === 429 ? (daily ? 30 * 60_000 : 60_000) : res.status >= 500 ? 30_000 : res.status === 401 || res.status === 403 ? 10 * 60_000 : 0));
  }
  console.log("all services failed", problems);
  return error(429, "Carbon AI's free services are all busy or used up for now. Try again in a few minutes.", { "retry-after": "60" });
}

function tooMany(request, env) {
  const ip = request.headers.get("cf-connecting-ip") || "?", now = Date.now(), limit = Number(env.PER_MINUTE) || 30;
  const times = (recent.get(ip) || []).filter(t => now - t < 60_000);
  times.push(now); recent.set(ip, times);
  if (recent.size > 5000) recent.clear();
  return times.length > limit;
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (request.method === "OPTIONS") return new Response(null, { headers: CORS });
    if (url.pathname === "/" || url.pathname === "/health") return json(200, { ok: true, name: "Carbon AI", services: services(env).map(s => s.name) });
    if (url.pathname === "/v1/models") return json(200, { object: "list", data: [{ id: "carbon", object: "model", owned_by: "carbon" }] });
    if (url.pathname === "/v1/chat/completions" && request.method === "POST") {
      if (tooMany(request, env)) return error(429, "Too many messages at once. Wait a moment.");
      return chat(request, env);
    }
    return error(404, "Not found.");
  },
};
