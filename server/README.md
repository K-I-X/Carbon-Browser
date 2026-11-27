# Carbon AI's server

Carbon AI talks to this small server, not to any AI service directly. The server keeps the AI keys, so people who use Carbon never sign up, never add a key, and never see a usage limit. It runs on Cloudflare's free plan.

The server tries free AI services in order. If one is busy or out of free use, the next one answers right away, and the used-up one is skipped for a while:

1. Google Gemini (free)
2. Groq (free)
3. Cerebras (free)
4. Cloudflare Workers AI (free daily allowance)
5. Any pay-as-you-go service you add. With one here, Carbon AI never runs out: the free services are still used first, so you only pay when they're all used up.

You need at least the Gemini key. Each extra service adds more free capacity.

## Set it up (about 10 minutes, no coding)

### 1. Get the free AI keys

- **Gemini (needed):** go to [aistudio.google.com/apikey](https://aistudio.google.com/apikey), sign in with a Google account, press **Create API key**, and copy it.
- **Groq (recommended):** go to [console.groq.com/keys](https://console.groq.com/keys), sign up, press **Create API Key**, and copy it.
- **Cerebras (optional):** go to [cloud.cerebras.ai](https://cloud.cerebras.ai), sign up, open **API Keys**, create one, and copy it.

Keep these to yourself: anyone with a key can use your allowance.

### 2. Put the server on Cloudflare

1. Sign up (free) at [dash.cloudflare.com](https://dash.cloudflare.com/sign-up).
2. Open **Workers & Pages**, press **Create**, then **Create Worker** (the "Hello World" one). Name it `carbon-ai` and press **Deploy**.
3. Press **Edit code**. Delete everything in the editor, paste in all of [`worker.js`](worker.js) from this folder, and press **Deploy**.
4. Go back to the worker and open **Settings → Variables and Secrets**. Press **Add**, choose **Secret**, and add each key you have:
   - `GEMINI_API_KEY`
   - `GROQ_API_KEY`
   - `CEREBRAS_API_KEY`

   Press **Deploy** when asked.
5. Copy your worker's address from its page. It looks like `https://carbon-ai.YOUR-NAME.workers.dev`.
6. Check it works: open that address followed by `/health` in a browser. It should show `"ok":true` and list the services you added, like `["gemini","groq"]`.

### 3. Connect Carbon to it

1. On GitHub, open the Carbon-Browser repository's **Settings → Secrets and variables → Actions → Variables** tab.
2. Press **New repository variable**. Name: `CARBON_AI_URL`. Value: your worker's address from step 2.5, for example `https://carbon-ai.YOUR-NAME.workers.dev`.
3. Publish a new build: push a commit with `[release]` in its message. Carbon builds from then on carry the address, and copies people already have pick it up when they update.

## Optional extras

All of these go in the same **Variables and Secrets** page. Use **Secret** for keys and **Text** for everything else.

- **Cloudflare Workers AI:** set `CF_ACCOUNT_ID` (on your Cloudflare dashboard's home page) and `CF_AI_TOKEN`. To make the token, go to **My Profile → API Tokens → Create Token** and use the "Workers AI" template.
- **Never run out (paid):** set `EXTRA_URL`, `EXTRA_KEY` and `EXTRA_MODEL` for any OpenAI-compatible service. For example, a paid Gemini key with `EXTRA_URL` = `https://generativelanguage.googleapis.com/v1beta/openai` and `EXTRA_MODEL` = `gemini-flash-latest`. It's only used when every free service is used up.
- **Other models:** `GEMINI_MODEL`, `GROQ_MODEL`, `CEREBRAS_MODEL` and `CF_MODEL` choose the model each service uses. Use this if a default model is retired.
- **Per-person limit:** `PER_MINUTE` sets how many messages one internet address can send per minute (default 30). It stops one person from using up everyone's free allowance.

## How it works

Carbon sends each Carbon AI message to `/v1/chat/completions` in the OpenAI format. The server passes it to the first service that can answer and streams the reply straight back. Tool calls (reading pages, clicking, typing) pass through unchanged.
