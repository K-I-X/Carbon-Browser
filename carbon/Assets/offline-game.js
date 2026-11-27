// Signal Run: Carbon's offline game, on the "You're offline" page. Carbon's own game (Carbon, AGPL-3.0).
// Carby, a little carbon ring, runs through the dead zone while the internet is down: jump the static, duck the dropped
// packets, double-jump the firewalls and clear the gaps. Electrons build a combo; close calls, boosts, magnets, daily
// missions, unlockable skins and a flag where your best run ended keep you coming back. Space, Up, W, a click or a tap
// jumps (hold it for a higher jump, again in the air for a double jump), Down or S ducks and drops fast, P pauses, M mutes.
// Carbon fills window.CARBON_OFFLINE with the saved best, electrons and skin, and saves them again when a run ends.
(() => {
 'use strict';
 const S = Object.assign({ best: 0, bestDist: 0, electrons: 0, skin: 0, mute: false, day: '', done: [], dark: true }, window.CARBON_OFFLINE || {});
 const cv = document.getElementById('signal-run'); if (!cv) return;
 const ctx = cv.getContext('2d');
 const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
 const DARK = !!S.dark;

 // ---------- saved state ----------
 const today = new Date().toISOString().slice(0, 10);
 if (S.day !== today) { S.day = today; S.done = []; }
 const save = () => { try { window.chrome && chrome.webview && chrome.webview.postMessage({ kind: 'carbon-offline', best: S.best, bestDist: S.bestDist, electrons: S.electrons, skin: S.skin, mute: S.mute, day: S.day, done: S.done }); } catch (e) { /* not in Carbon: nothing kept */ } };

 // ---------- skins: unlocked by electrons collected over all runs ----------
 const SKINS = [
  { name: 'Carbon', need: 0, c: '#8b7bff', g: '#b9afff' }, { name: 'Mint', need: 150, c: '#2fe0a8', g: '#9bf5d8' },
  { name: 'Sunset', need: 400, c: '#ff8a5c', g: '#ffc2a6' }, { name: 'Gold', need: 900, c: '#ffcc33', g: '#ffe9a3' },
  { name: 'Ice', need: 1800, c: '#6fd6ff', g: '#c8f0ff' }, { name: 'Glitch', need: 3500, c: '#ff3dbb', g: '#3dfff3' },
  { name: 'Prism', need: 6000, c: 'prism', g: '#ffffff' }];
 const unlocked = i => S.electrons >= SKINS[i].need;
 if (!unlocked(S.skin | 0)) S.skin = 0;
 const skinColor = t => { const k = SKINS[S.skin]; if (k.c === 'prism') return `hsl(${(t * 120) % 360} 95% 65%)`; if (k.name === 'Glitch') return (t * 8 | 0) % 2 ? k.c : k.g; return k.c; };

 // ---------- daily missions: three a day, the same for everyone on that day ----------
 const POOL = [
  { id: 'orbs', text: 'Collect 30 electrons in one run', test: r => r.orbs >= 30 },
  { id: 'close', text: 'Get 3 close calls in one run', test: r => r.close >= 3 },
  { id: 'score', text: 'Score 1,500 in one run', test: r => r.score >= 1500 },
  { id: 'time', text: 'Survive for 45 seconds', test: r => r.time >= 45 },
  { id: 'boost', text: 'Grab a boost', test: r => r.boosts >= 1 },
  { id: 'combo', text: 'Reach a x5 combo', test: r => r.maxCombo >= 5 },
  { id: 'walls', text: 'Double-jump 4 firewalls in one run', test: r => r.walls >= 4 },
  { id: 'gaps', text: 'Clear 5 gaps in one run', test: r => r.gaps >= 5 },
  { id: 'duck', text: 'Duck under 6 packets in one run', test: r => r.ducks >= 6 },
  { id: 'far', text: 'Run 800 m', test: r => r.dist >= 800 }];
 let seed = [...today].reduce((a, ch) => (a * 31 + ch.charCodeAt(0)) >>> 0, 7);
 const rnd = () => (seed = (seed * 1664525 + 1013904223) >>> 0) / 4294967296;
 const MISSIONS = []; while (MISSIONS.length < 3) { const m = POOL[Math.floor(rnd() * POOL.length)]; if (!MISSIONS.includes(m)) MISSIONS.push(m); }
 const MISSION_PAY = 60;

 // ---------- sound: tiny blips made on the spot ----------
 let ac = null;
 const tone = (f, d = .08, type = 'square', v = .05, slide = 0) => {
  if (S.mute) return;
  try {
   ac = ac || new (window.AudioContext || window.webkitAudioContext)();
   const o = ac.createOscillator(), g = ac.createGain(), t = ac.currentTime;
   o.type = type; o.frequency.setValueAtTime(f, t); if (slide) o.frequency.exponentialRampToValueAtTime(Math.max(40, f + slide), t + d);
   g.gain.setValueAtTime(v, t); g.gain.exponentialRampToValueAtTime(.0001, t + d);
   o.connect(g); g.connect(ac.destination); o.start(t); o.stop(t + d + .02);
  } catch (e) { /* no audio */ }
 };

 // ---------- canvas ----------
 let W = 0, H = 0, GROUND = 0;
 const resize = () => {
  const r = cv.getBoundingClientRect(), dpr = Math.min(2, window.devicePixelRatio || 1);
  W = Math.max(280, r.width); H = Math.max(200, r.height); GROUND = H - 46;
  cv.width = Math.round(W * dpr); cv.height = Math.round(H * dpr); ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
 };
 resize(); addEventListener('resize', resize);

 // ---------- game state ----------
 const P = { x: 0, y: 0, vy: 0, w: 26, h: 26, ground: true, jumps: 0, duck: false, hold: 0, blink: 0, squash: 1, lastGround: 0 };
 let state = 'ready', t = 0, speed = 0, dist = 0, score = 0, combo = 1, comboTime = 0, orbsLeft = 0;
 let obs = [], orbs = [], ups = [], parts = [], pops = [], stars = [], towers = [], hills = [];
 let boost = 0, magnet = 0, shake = 0, slow = 1, flash = 0, deadAt = 0, nextAt = 0, run = null, newBest = false, earned = 0, paidToday = [];
 let jumpHeld = false, downHeld = false;
 for (let i = 0; i < 70; i++) stars.push({ x: Math.random(), y: Math.random() * .7, s: Math.random() * 1.6 + .3, p: Math.random() * 6 });
 for (let i = 0; i < 9; i++) towers.push({ x: i * 170 + Math.random() * 80, h: 50 + Math.random() * 70, broken: Math.random() < .6 });
 for (let i = 0; i < 6; i++) hills.push({ x: i * 260, h: 18 + Math.random() * 26, w: 200 + Math.random() * 160 });

 const reset = () => {
  P.x = Math.min(110, W * .18); P.y = GROUND - P.h; P.vy = 0; P.ground = true; P.jumps = 0; P.duck = false;
  t = 0; speed = 330; dist = 0; score = 0; combo = 1; comboTime = 0; boost = 0; magnet = 0; slow = 1; flash = 0; newBest = false; earned = 0;
  obs = []; orbs = []; ups = []; parts = []; pops = []; nextAt = 520;
  run = { orbs: 0, close: 0, score: 0, time: 0, boosts: 0, maxCombo: 1, walls: 0, gaps: 0, ducks: 0, dist: 0 };
 };
 reset();

 // ---------- spawning: patterns get busier as you speed up ----------
 const level = () => Math.min(1, dist / 30000); // the hardest patterns after about a minute
 const orbArc = (x, n, base, lift) => { for (let i = 0; i < n; i++) orbs.push({ x: x + i * 30, y: base - Math.sin(i / (n - 1 || 1) * Math.PI) * lift, r: 7, ph: Math.random() * 6 }); };
 const spawn = () => {
  const x = W + 40, L = level(), roll = Math.random();
  if (roll < .30) { // static: one, two or three spikes in a row
   const n = 1 + (Math.random() < L * .7 ? 1 : 0) + (Math.random() < L * .35 ? 1 : 0);
   obs.push({ type: 'spike', x, w: 22 * n, h: 26 + Math.random() * 8, n });
   orbArc(x - 20, 5, GROUND - 40, 70);
  } else if (roll < .48 && dist > 250) { // a dropped packet at head height: duck
   obs.push({ type: 'packet', x, w: 30, h: 26, y: GROUND - 40 - Math.random() * 2, rot: 0 });
   orbArc(x - 10, 3, GROUND - 14, 0);
  } else if (roll < .62 && dist > 500) { // a firewall: double-jump it
   obs.push({ type: 'wall', x, w: 26, h: 105 + L * 50 });
   orbArc(x - 50, 6, GROUND - 120, 50);
  } else if (roll < .76 && dist > 350) { // a gap in the ground
   const w = 70 + L * 70; obs.push({ type: 'gap', x, w, h: 0 });
   orbArc(x, Math.max(3, Math.round(w / 30)), GROUND - 70, 30);
  } else if (roll < .86) { // a breather full of electrons
   orbArc(x, 8, GROUND - 30, 60);
  } else { // spikes with a packet over them
   obs.push({ type: 'spike', x, w: 22, h: 26, n: 1 });
   if (dist > 700) obs.push({ type: 'packet', x: x + 260, w: 30, h: 26, y: GROUND - 40, rot: 0 });
   orbArc(x - 10, 4, GROUND - 60, 40);
  }
  if (Math.random() < .07 + L * .05) ups.push({ type: Math.random() < .55 ? 'boost' : 'magnet', x: x + 90, y: GROUND - 95, ph: 0 });
  const gap = 340 + Math.random() * 300 - L * 140;
  nextAt = dist + Math.max(240, gap * (speed / 330) * .75);
 };

 // ---------- effects ----------
 const burst = (x, y, color, n = 12, power = 220) => { if (reduce) n = Math.ceil(n / 3); for (let i = 0; i < n; i++) { const a = Math.random() * Math.PI * 2, v = power * (.3 + Math.random() * .7); parts.push({ x, y, vx: Math.cos(a) * v, vy: Math.sin(a) * v - 60, life: .5 + Math.random() * .4, max: .9, c: color, r: 1.5 + Math.random() * 2.5 }); } };
 const pop = (x, y, text, color = '#fff', size = 15) => pops.push({ x, y, text, color, size, life: .9 });
 const toast = (text, color) => pop(W / 2, 46, text, color || '#fff', 17);

 // ---------- controls ----------
 const jump = () => {
  if (state === 'paused') { state = 'play'; return; }
  if (state === 'ready' || state === 'dead') { if (state === 'dead' && performance.now() - deadAt < 650) return; reset(); state = 'play'; tone(523, .07); tone(784, .09, 'square', .04); return; }
  if (state !== 'play') return;
  const coyote = t - P.lastGround < .09;
  if (P.ground || coyote) { P.vy = -560; P.ground = false; P.jumps = 1; P.hold = .16; P.squash = .7; tone(440, .09, 'square', .045, 260); burst(P.x + 13, GROUND, skinColor(t), 6, 90); }
  else if (P.jumps < 2) { P.vy = -500; P.jumps = 2; P.hold = .1; tone(660, .1, 'triangle', .05, 330); burst(P.x + 13, P.y + 26, '#ffffff', 8, 120); }
 };
 const press = down => { if (down) { if (!jumpHeld) jump(); jumpHeld = true; } else { jumpHeld = false; P.hold = 0; } };
 const pause = () => { if (state === 'play') { state = 'paused'; } else if (state === 'paused') { state = 'play'; } };
 addEventListener('keydown', e => {
  if (e.target && /INPUT|TEXTAREA/.test(e.target.tagName)) return;
  const k = e.key;
  if (k === ' ' || k === 'ArrowUp' || k === 'w' || k === 'W') { e.preventDefault(); press(true); }
  else if (k === 'ArrowDown' || k === 's' || k === 'S') { e.preventDefault(); downHeld = true; }
  else if (k === 'p' || k === 'P' || k === 'Escape') { if (state === 'play' || state === 'paused') { e.preventDefault(); pause(); } }
  else if (k === 'm' || k === 'M') { S.mute = !S.mute; save(); toast(S.mute ? 'Sound off' : 'Sound on'); }
  else if ((k === 'ArrowLeft' || k === 'ArrowRight') && state !== 'play') { e.preventDefault(); let i = S.skin; do { i = (i + (k === 'ArrowLeft' ? -1 : 1) + SKINS.length) % SKINS.length; } while (!unlocked(i) && i !== S.skin); if (i !== S.skin) { S.skin = i; save(); tone(880, .05); } }
 });
 addEventListener('keyup', e => { const k = e.key; if (k === ' ' || k === 'ArrowUp' || k === 'w' || k === 'W') press(false); else if (k === 'ArrowDown' || k === 's' || k === 'S') downHeld = false; });
 cv.addEventListener('pointerdown', e => {
  e.preventDefault(); cv.focus();
  const r = cv.getBoundingClientRect(), x = e.clientX - r.left, y = e.clientY - r.top;
  if (state !== 'play' && y > H - 40 && Math.abs(x - W / 2) < 120) { const i = x < W / 2 ? -1 : 1; let n = S.skin; do { n = (n + i + SKINS.length) % SKINS.length; } while (!unlocked(n) && n !== S.skin); S.skin = n; save(); return; }
  if (state === 'play' && y > H * .66 && e.pointerType === 'touch') { downHeld = true; return; }
  press(true);
 });
 addEventListener('pointerup', () => { press(false); downHeld = false; });
 addEventListener('blur', () => { if (state === 'play') state = 'paused'; });

 // ---------- the run ----------
 const die = () => {
  state = 'dying'; slow = .25; shake = reduce ? 0 : 14; flash = 1; deadAt = performance.now();
  burst(P.x + 13, P.y + 13, skinColor(t), 34, 360); burst(P.x + 13, P.y + 13, '#ffffff', 14, 260);
  tone(180, .35, 'sawtooth', .07, -140); tone(90, .45, 'square', .05, -50);
  setTimeout(finish, reduce ? 200 : 650);
 };
 const finish = () => {
  state = 'dead'; slow = 1;
  run.score = Math.floor(score); run.dist = Math.floor(dist / 10); run.time = t;
  if (run.score > S.best) { S.best = run.score; newBest = true; }
  S.bestDist = Math.max(S.bestDist, Math.floor(dist));
  paidToday = [];
  MISSIONS.forEach(m => { if (!S.done.includes(m.id) && m.test(run)) { S.done.push(m.id); paidToday.push(m); S.electrons += MISSION_PAY; earned += MISSION_PAY; } });
  const before = SKINS.filter((_, i) => S.electrons - earned >= SKINS[i].need).length, after = SKINS.filter((_, i) => unlocked(i)).length;
  run.unlocked = after > before ? SKINS[after - 1].name : '';
  if (newBest || paidToday.length || run.unlocked) { tone(523, .1, 'triangle', .05); setTimeout(() => tone(659, .1, 'triangle', .05), 110); setTimeout(() => tone(1047, .18, 'triangle', .05), 220); }
  save();
 };
 const hit = (a, b) => a.x < b.x + b.w && a.x + a.w > b.x && a.y < b.y + b.h && a.y + a.h > b.y;

 const step = dt => {
  t += dt;
  const L = level();
  speed = Math.min(760, 330 + dist * .014) * (boost > 0 ? 1.55 : 1);
  const dx = speed * dt; dist += dx; score += dx * .1 * (boost > 0 ? 2 : 1);
  if (dist >= nextAt) spawn();
  // player
  P.duck = downHeld && P.ground;
  const ph = P.duck ? 15 : 26;
  if (P.ground && ph !== P.h) { P.y = GROUND - ph; P.h = ph; } // ducking or standing up keeps Carby's feet on the ground
  if (P.hold > 0 && jumpHeld) { P.vy -= 1500 * dt; P.hold -= dt; }
  P.vy += (downHeld && !P.ground ? 4200 : 1900) * dt;
  P.y += P.vy * dt; P.h = ph;
  const overGap = obs.some(o => o.type === 'gap' && P.x + 6 > o.x && P.x + P.w - 6 < o.x + o.w);
  if (!overGap && P.y + P.h >= GROUND && P.vy >= 0) {
   if (!P.ground) { P.squash = 1.35; burst(P.x + 13, GROUND, 'rgba(255,255,255,.7)', 4, 70); }
   P.y = GROUND - P.h; P.vy = 0; P.ground = true; P.jumps = 0; P.lastGround = t;
  } else if (P.y + P.h < GROUND - 1 || overGap) P.ground = false;
  if (P.y > H + 40) return die();
  P.squash += (1 - P.squash) * Math.min(1, dt * 12);
  if (Math.random() < dt * .4) P.blink = .12; P.blink -= dt;
  if (!reduce && Math.random() < .6) parts.push({ x: P.x + 4, y: P.y + P.h / 2 + (Math.random() - .5) * 10, vx: -speed * .3, vy: 0, life: .35, max: .35, c: boost > 0 ? `hsl(${t * 400 % 360} 90% 65%)` : skinColor(t), r: boost > 0 ? 4 : 2.2 });
  // the world moves
  const box = { x: P.x + 4, y: P.y + 3, w: P.w - 8, h: P.h - 5 };
  for (const o of obs) {
   o.x -= dx;
   if (o.type === 'packet') o.rot += dt * 3;
   if (o.type === 'gap') { if (!o.passed && o.x + o.w < P.x) { o.passed = true; run.gaps++; } continue; }
   if (o.smashed) continue;
   const ob = o.type === 'packet' ? { x: o.x + 3, y: o.y + 3, w: o.w - 6, h: o.h - 6 } : { x: o.x + 3, y: GROUND - o.h + 4, w: o.w - 6, h: o.h - 4 };
   if (hit(box, ob)) {
    if (boost > 0) { { o.smashed = true; burst(o.x + o.w / 2, ob.y + ob.h / 2, '#ff5a7a', 16, 300); score += 40; pop(o.x, ob.y, '+40 SMASH', '#ffd25c'); tone(220, .12, 'sawtooth', .05, -100); shake = reduce ? 0 : 6; } continue; }
    return die();
   }
   if (!o.passed && o.x + o.w < P.x) {
    o.passed = true;
    if (o.type === 'wall') run.walls++;
    if (o.type === 'packet' && P.duck) run.ducks++;
    // a close call: you cleared it by a hair
    const gapY = o.type === 'packet' ? -1 : (GROUND - o.h) - (P.y + P.h);
    if (gapY >= 0 && gapY < 14) { run.close++; score += 25 * combo; pop(P.x + 10, P.y - 10, 'CLOSE! +' + 25 * combo, '#ffd25c', 14); tone(990, .07, 'triangle', .05); }
   }
  }
  obs = obs.filter(o => o.x + o.w > -60);
  for (const o of orbs) {
   o.x -= dx;
   if (magnet > 0 && !o.taken) { const ex = P.x + 13 - o.x, ey = P.y + 13 - o.y, d = Math.hypot(ex, ey); if (d < 220) { o.x += ex / d * 520 * dt; o.y += ey / d * 520 * dt; } }
   if (!o.taken && Math.hypot(P.x + 13 - o.x, P.y + P.h / 2 - o.y) < o.r + 15) {
    o.taken = true; run.orbs++; S.electrons++; earned++;
    combo = Math.min(5, combo + (comboTime > 0 ? .25 : 0)); comboTime = 1.4; run.maxCombo = Math.max(run.maxCombo, Math.floor(combo));
    score += 10 * Math.floor(combo);
    tone(700 + Math.floor(combo) * 110 + (run.orbs % 4) * 30, .06, 'triangle', .04);
    burst(o.x, o.y, '#5ff2ff', 5, 110);
   }
  }
  orbs = orbs.filter(o => o.x > -30 && !o.taken);
  for (const u of ups) {
   u.x -= dx; u.ph += dt * 4;
   if (!u.taken && Math.hypot(P.x + 13 - u.x, P.y + 13 - u.y) < 26) {
    u.taken = true;
    if (u.type === 'boost') { boost = 4; run.boosts++; toast('BOOST! Smash through anything', '#7dff9b'); tone(523, .25, 'sawtooth', .05, 500); }
    else { magnet = 7; toast('MAGNET! Electrons come to you', '#c28bff'); tone(392, .25, 'triangle', .05, 300); }
    burst(u.x, u.y, u.type === 'boost' ? '#7dff9b' : '#c28bff', 20, 260);
   }
  }
  ups = ups.filter(u => u.x > -30 && !u.taken);
  comboTime -= dt; if (comboTime <= 0) combo = Math.max(1, combo - dt * 2);
  boost = Math.max(0, boost - dt); magnet = Math.max(0, magnet - dt);
  // the best run's flag: passing it is a moment
  if (S.bestDist > 0 && !run.passedBest && dist > S.bestDist) { run.passedBest = true; toast('You passed your best!', '#ffd25c'); burst(P.x + 13, P.y, '#ffd25c', 30, 320); tone(784, .12, 'triangle', .05); setTimeout(() => tone(1175, .2, 'triangle', .05), 120); }
  run.score = Math.floor(score); run.time = t; run.dist = Math.floor(dist / 10);
  // a mission finished mid-run says so right away
  MISSIONS.forEach(m => { if (!S.done.includes(m.id) && !run['m_' + m.id] && m.test(run)) { run['m_' + m.id] = true; toast('Mission done: ' + m.text, '#7dff9b'); tone(880, .1, 'triangle', .05); } });
 };

 const tick = dt => {
  for (const p of parts) { p.x += p.vx * dt; p.y += p.vy * dt; p.vy += 600 * dt; p.life -= dt; }
  parts = parts.filter(p => p.life > 0);
  for (const q of pops) { q.y -= 30 * dt; q.life -= dt; }
  pops = pops.filter(q => q.life > 0);
  shake = Math.max(0, shake - dt * 40); flash = Math.max(0, flash - dt * 3);
 };

 // ---------- drawing ----------
 const C = DARK ? { sky1: '#0b0a16', sky2: '#17132e', ground: '#8b7bff', grid: 'rgba(139,123,255,.16)', far: '#1d1838', near: '#241d45', ink: '#f2f0ff', mute: '#9a95b8', panel: 'rgba(14,12,28,.82)' }
  : { sky1: '#eef0ff', sky2: '#dcdcff', ground: '#6b5cf0', grid: 'rgba(107,92,240,.16)', far: '#cfcdf5', near: '#bdb8ef', ink: '#17142e', mute: '#5d5a7a', panel: 'rgba(255,255,255,.86)' };
 const hex = (x, y, r, rot = 0) => { ctx.beginPath(); for (let i = 0; i < 6; i++) { const a = rot + i * Math.PI / 3 + Math.PI / 6; ctx[i ? 'lineTo' : 'moveTo'](x + Math.cos(a) * r, y + Math.sin(a) * r); } ctx.closePath(); };
 // Light mode: the neon text colors, darkened so they read on a pale sky.
 const LIGHT_INK = { '#ffd25c': '#a16207', '#7dff9b': '#15803d', '#5ff2ff': '#0e7490', '#c28bff': '#7c3aed', '#fff': '#17142e' };
 const text = (s, x, y, size, color, align = 'center', weight = 700) => { if (!DARK && LIGHT_INK[color]) color = LIGHT_INK[color]; ctx.font = `${weight} ${size}px "Segoe UI Variable Display","Segoe UI",system-ui,sans-serif`; ctx.textAlign = align; ctx.fillStyle = color; ctx.fillText(s, x, y); };
 let bgX = 0;

 const drawCarby = (x, y, w, h, color, t) => {
  const cx = x + w / 2, cy = y + h / 2, sq = P.squash;
  ctx.save(); ctx.translate(cx, cy + (1 - sq) * 6); ctx.scale(1 / sq, sq);
  ctx.shadowColor = color; ctx.shadowBlur = reduce ? 0 : 18;
  ctx.fillStyle = color; hex(0, 0, 15, state === 'play' && !P.ground ? t * 6 : 0); ctx.fill();
  ctx.shadowBlur = 0; ctx.fillStyle = DARK ? '#0b0a16' : '#ffffff'; hex(0, 0, 8.5, state === 'play' && !P.ground ? t * 6 : 0); ctx.fill();
  // eyes
  ctx.fillStyle = DARK ? '#ffffff' : '#17142e';
  const eh = P.blink > 0 ? 1 : 4.2;
  ctx.fillRect(-4.5, -2.5, 2.6, eh); ctx.fillRect(2, -2.5, 2.6, eh);
  ctx.restore();
 };

 const draw = () => {
  ctx.save();
  if (shake > 0) ctx.translate((Math.random() - .5) * shake, (Math.random() - .5) * shake);
  // sky
  const g = ctx.createLinearGradient(0, 0, 0, H); g.addColorStop(0, C.sky1); g.addColorStop(1, C.sky2); ctx.fillStyle = g; ctx.fillRect(-20, -20, W + 40, H + 40);
  if (DARK) for (const s of stars) { ctx.globalAlpha = .35 + .35 * Math.sin(t * 2 + s.p); ctx.fillStyle = '#fff'; ctx.fillRect((s.x * W - bgX * .05 % W + W) % W, s.y * GROUND, s.s, s.s); } ctx.globalAlpha = 1;
  // far: broken signal towers
  ctx.fillStyle = C.far;
  for (const tw of towers) {
   const x = ((tw.x - bgX * .18) % 1530 + 1530) % 1530 - 100, base = GROUND;
   ctx.fillRect(x, base - tw.h, 4, tw.h); ctx.beginPath(); ctx.moveTo(x - 14, base); ctx.lineTo(x + 2, base - tw.h * .7); ctx.lineTo(x + 18, base); ctx.fill();
   ctx.strokeStyle = C.far; ctx.lineWidth = 2;
   for (let i = 1; i <= 3; i++) { if (tw.broken && i === 3) continue; ctx.beginPath(); ctx.arc(x + 2, base - tw.h, i * 7, -2.3, -.85); ctx.stroke(); }
   if (tw.broken) { ctx.fillStyle = '#ff5a7a'; ctx.globalAlpha = .5 + .5 * Math.sin(t * 5 + tw.x); ctx.fillRect(x, base - tw.h - 4, 4, 4); ctx.globalAlpha = 1; ctx.fillStyle = C.far; }
  }
  // near hills
  ctx.fillStyle = C.near;
  for (const hl of hills) { const x = ((hl.x - bgX * .45) % 1560 + 1560) % 1560 - 200; ctx.beginPath(); ctx.ellipse(x + hl.w / 2, GROUND, hl.w / 2, hl.h, 0, Math.PI, 0); ctx.fill(); }
  // ground: a glowing line over a moving grid, with the gaps cut out
  ctx.fillStyle = DARK ? '#0d0b1c' : '#e7e6ff'; ctx.fillRect(0, GROUND, W, H - GROUND);
  ctx.strokeStyle = C.grid; ctx.lineWidth = 1;
  for (let i = 0; i < 4; i++) { const y = GROUND + 8 + i * i * 4; ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(W, y); ctx.stroke(); }
  for (let x = -((bgX) % 40); x < W; x += 40) { ctx.beginPath(); ctx.moveTo(x, GROUND); ctx.lineTo(x - (W / 2 - x) * .25, H); ctx.stroke(); }
  const gaps = obs.filter(o => o.type === 'gap').sort((a, b) => a.x - b.x);
  ctx.strokeStyle = C.ground; ctx.lineWidth = 3; ctx.shadowColor = C.ground; ctx.shadowBlur = reduce ? 0 : 12;
  let from = 0; ctx.beginPath();
  for (const gp of gaps) { ctx.moveTo(from, GROUND); ctx.lineTo(Math.max(from, gp.x), GROUND); from = gp.x + gp.w; }
  ctx.moveTo(from, GROUND); ctx.lineTo(W, GROUND); ctx.stroke(); ctx.shadowBlur = 0;
  for (const gp of gaps) {
   ctx.fillStyle = DARK ? '#04030a' : '#bdb8ef'; ctx.fillRect(gp.x, GROUND - 1, gp.w, H - GROUND + 2);
   for (let i = 0; i < 3; i++) { ctx.fillStyle = i % 2 ? '#ffcc33' : '#17142e'; ctx.fillRect(gp.x - 6, GROUND + i * 6, 6, 6); ctx.fillRect(gp.x + gp.w, GROUND + i * 6, 6, 6); }
  }
  // the flag where your best run ended
  if (S.bestDist > 0 && state !== 'ready') {
   const fx = P.x + (S.bestDist - dist);
   if (fx > -20 && fx < W + 20) {
    ctx.strokeStyle = '#ffd25c'; ctx.lineWidth = 2; ctx.beginPath(); ctx.moveTo(fx, GROUND); ctx.lineTo(fx, GROUND - 70); ctx.stroke();
    ctx.fillStyle = '#ffd25c'; ctx.beginPath(); ctx.moveTo(fx, GROUND - 70); ctx.lineTo(fx + 34, GROUND - 62); ctx.lineTo(fx, GROUND - 54); ctx.fill();
    text('BEST', fx + 3, GROUND - 76, 10, '#ffd25c', 'left', 800);
   }
  }
  // obstacles
  for (const o of obs) {
   if (o.type === 'spike' && !o.smashed) {
    const n = o.n || 1, each = o.w / n;
    ctx.fillStyle = '#ff4d79'; ctx.shadowColor = '#ff4d79'; ctx.shadowBlur = reduce ? 0 : 10;
    for (let i = 0; i < n; i++) {
     const x = o.x + i * each, j = reduce ? 0 : Math.sin(t * 30 + i + o.x) * 1.5;
     ctx.beginPath(); ctx.moveTo(x, GROUND); ctx.lineTo(x + each * .22, GROUND - o.h * .55 + j); ctx.lineTo(x + each * .4, GROUND - o.h * .35); ctx.lineTo(x + each * .55, GROUND - o.h + j); ctx.lineTo(x + each * .72, GROUND - o.h * .4); ctx.lineTo(x + each * .85, GROUND - o.h * .62 - j); ctx.lineTo(x + each, GROUND); ctx.fill();
    }
    ctx.shadowBlur = 0;
   } else if (o.type === 'packet' && !o.smashed) {
    ctx.save(); ctx.translate(o.x + o.w / 2, o.y + o.h / 2); ctx.rotate(Math.sin(o.rot) * .25);
    ctx.fillStyle = '#ff6b4a'; ctx.shadowColor = '#ff6b4a'; ctx.shadowBlur = reduce ? 0 : 12; ctx.fillRect(-o.w / 2, -o.h / 2, o.w, o.h); ctx.shadowBlur = 0;
    ctx.fillStyle = '#2a0f0a'; ctx.fillRect(-o.w / 2 + 4, -o.h / 2 + 4, o.w - 8, 3); text('404', 0, 7, 10, '#2a0f0a', 'center', 900);
    ctx.restore();
   } else if (o.type === 'wall' && !o.smashed) {
    const y = GROUND - o.h, gr = ctx.createLinearGradient(0, y, 0, GROUND); gr.addColorStop(0, '#ffcc33'); gr.addColorStop(1, '#ff5a3c');
    ctx.fillStyle = gr; ctx.shadowColor = '#ff8a3c'; ctx.shadowBlur = reduce ? 0 : 14; ctx.fillRect(o.x, y, o.w, o.h); ctx.shadowBlur = 0;
    ctx.fillStyle = 'rgba(0,0,0,.25)'; for (let yy = y + 6; yy < GROUND; yy += 14) ctx.fillRect(o.x, yy, o.w, 4);
    for (let i = 0; i < 3; i++) { const fx = o.x + 4 + i * 8, fh = 8 + Math.sin(t * 12 + i * 2) * 4; ctx.fillStyle = '#ffe9a3'; ctx.beginPath(); ctx.moveTo(fx, y); ctx.lineTo(fx + 4, y - fh); ctx.lineTo(fx + 8, y); ctx.fill(); }
   }
  }
  // electrons and power-ups
  for (const o of orbs) {
   const pulse = 1 + Math.sin(t * 6 + o.ph) * .15;
   ctx.fillStyle = '#5ff2ff'; ctx.shadowColor = '#5ff2ff'; ctx.shadowBlur = reduce ? 0 : 12;
   ctx.beginPath(); ctx.arc(o.x, o.y, o.r * .55 * pulse, 0, Math.PI * 2); ctx.fill(); ctx.shadowBlur = 0;
   ctx.strokeStyle = 'rgba(95,242,255,.55)'; ctx.lineWidth = 1.2; ctx.beginPath(); ctx.ellipse(o.x, o.y, o.r * 1.2, o.r * .5, t * 2 + o.ph, 0, Math.PI * 2); ctx.stroke();
  }
  for (const u of ups) {
   const y = u.y + Math.sin(u.ph) * 6, c = u.type === 'boost' ? '#7dff9b' : '#c28bff';
   ctx.strokeStyle = c; ctx.lineWidth = 2.5; ctx.shadowColor = c; ctx.shadowBlur = reduce ? 0 : 16; hex(u.x, y, 15, t); ctx.stroke(); ctx.shadowBlur = 0;
   ctx.fillStyle = c;
   if (u.type === 'boost') { for (let i = 0; i < 3; i++) ctx.fillRect(u.x - 7 + i * 5, y + 5 - i * 4, 3, 2 + i * 4); }
   else { ctx.beginPath(); ctx.arc(u.x, y, 6, Math.PI, 0); ctx.lineWidth = 3.5; ctx.stroke(); ctx.fillRect(u.x - 7.7, y, 3.5, 4); ctx.fillRect(u.x + 4.2, y, 3.5, 4); }
  }
  // particles
  for (const p of parts) { ctx.globalAlpha = Math.max(0, p.life / p.max); ctx.fillStyle = p.c; ctx.beginPath(); ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2); ctx.fill(); }
  ctx.globalAlpha = 1;
  // Carby
  if (state !== 'dying' && state !== 'dead') {
   if (boost > 0) { ctx.globalAlpha = .35; drawCarby(P.x - 10, P.y, P.w, P.h, `hsl(${t * 500 % 360} 90% 65%)`, t); ctx.globalAlpha = 1; }
   if (magnet > 0) { ctx.strokeStyle = 'rgba(194,139,255,.35)'; ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(P.x + 13, P.y + 13, 30 + Math.sin(t * 8) * 4, 0, Math.PI * 2); ctx.stroke(); }
   drawCarby(P.x, P.y, P.w, P.h, boost > 0 ? `hsl(${t * 500 % 360} 90% 65%)` : skinColor(t), t);
  }
  // popups
  for (const q of pops) { ctx.globalAlpha = Math.min(1, q.life * 2); text(q.text, q.x, q.y, q.size, q.color, 'center', 800); }
  ctx.globalAlpha = 1;
  ctx.restore();
  if (flash > 0) { ctx.fillStyle = `rgba(255,255,255,${flash * .3})`; ctx.fillRect(0, 0, W, H); }
  // HUD
  if (state === 'play' || state === 'paused' || state === 'dying') {
   text(String(Math.floor(score)).padStart(5, '0'), W - 16, 28, 20, C.ink, 'right', 800);
   text('BEST ' + String(S.best).padStart(5, '0'), W - 16, 46, 11, C.mute, 'right', 700);
   text('⬢ ' + run.orbs, 16, 28, 16, '#5ff2ff', 'left', 800);
   if (combo >= 1.25) { text('x' + Math.floor(combo) + ' COMBO', 16, 48, 12, '#ffd25c', 'left', 800); ctx.fillStyle = 'rgba(255,210,92,.3)'; ctx.fillRect(16, 54, 80, 3); ctx.fillStyle = '#ffd25c'; ctx.fillRect(16, 54, 80 * Math.max(0, comboTime / 1.4), 3); }
   const bar = (v, max, c, y, label) => { if (v <= 0) return; ctx.fillStyle = 'rgba(255,255,255,.15)'; ctx.fillRect(W / 2 - 60, y, 120, 4); ctx.fillStyle = c; ctx.fillRect(W / 2 - 60, y, 120 * v / max, 4); text(label, W / 2, y - 4, 10, c, 'center', 800); };
   bar(boost, 4, '#7dff9b', 22, 'BOOST'); bar(magnet, 7, '#c28bff', boost > 0 ? 44 : 22, 'MAGNET');
  }
  if (state === 'paused') panel(['Paused', 'Press P or Space to keep running']);
  if (state === 'ready') title();
  if (state === 'dead') results();
 };
 const panel = (lines) => {
  ctx.fillStyle = C.panel; ctx.fillRect(0, 0, W, H);
  lines.forEach((l, i) => text(l, W / 2, H / 2 - 10 + i * 26, i ? 14 : 26, i ? C.mute : C.ink, 'center', i ? 600 : 800));
 };
 const skinRow = () => {
  const k = SKINS[S.skin], next = SKINS.find((s, i) => !unlocked(i));
  text('◀  ' + k.name + '  ▶', W / 2, H - 16, 13, C.ink, 'center', 700);
  if (next) text(`${next.name} unlocks at ${next.need} ⬢ (you have ${S.electrons})`, W / 2, H - 34, 11, C.mute, 'center', 600);
 };
 const title = () => {
  ctx.fillStyle = C.panel; ctx.fillRect(0, 0, W, H);
  drawCarby(W / 2 - 13, H * .2 - 13 + Math.sin(t * 3) * 4, 26, 26, skinColor(t), t);
  text('SIGNAL RUN', W / 2, H * .2 + 42, 26, C.ink, 'center', 900);
  text('Space, ↑ or tap to jump · again to double-jump · ↓ to duck', W / 2, H * .2 + 64, 12, C.mute, 'center', 600);
  text("Today's missions", W / 2, H * .2 + 94, 12, '#ffd25c', 'center', 800);
  MISSIONS.forEach((m, i) => text((S.done.includes(m.id) ? '✓ ' : '○ ') + m.text, W / 2, H * .2 + 114 + i * 17, 12, S.done.includes(m.id) ? '#7dff9b' : C.ink, 'center', 600));
  if (S.best > 0) text('Best ' + S.best, W / 2, H * .2 + 172, 12, C.mute, 'center', 700);
  if (H > 300) skinRow();
  text(Math.sin(t * 4) > 0 ? 'Press Space to run' : '', W / 2, H * .2 - 34, 13, skinColor(t), 'center', 800);
 };
 const results = () => {
  ctx.fillStyle = C.panel; ctx.fillRect(0, 0, W, H);
  text(newBest ? 'NEW BEST!' : 'SIGNAL LOST', W / 2, H * .22, 28, newBest ? '#ffd25c' : C.ink, 'center', 900);
  text(String(run.score), W / 2, H * .22 + 40, 34, C.ink, 'center', 900);
  text(`${run.dist} m · ${run.orbs} electrons · ${run.close} close calls · best ${S.best}`, W / 2, H * .22 + 64, 12, C.mute, 'center', 600);
  let y = H * .22 + 92;
  if (paidToday.length) { paidToday.forEach(m => { text(`✓ ${m.text}  +${MISSION_PAY} ⬢`, W / 2, y, 12, '#7dff9b', 'center', 700); y += 18; }); }
  else { const left = MISSIONS.filter(m => !S.done.includes(m.id)); if (left.length) { text('Next mission: ' + left[0].text, W / 2, y, 12, '#ffd25c', 'center', 700); y += 18; } else { text('All of today\'s missions done. New ones tomorrow!', W / 2, y, 12, '#7dff9b', 'center', 700); y += 18; } }
  if (run.unlocked) { text(`New skin unlocked: ${run.unlocked}! Use ◀ ▶ to wear it`, W / 2, y, 13, '#ffd25c', 'center', 800); y += 18; }
  text(performance.now() - deadAt > 650 ? 'Press Space to run again' : '', W / 2, y + 14, 13, skinColor(t), 'center', 800);
  if (H > 300) skinRow();
 };

 // ---------- loop ----------
 let last = performance.now(), acc = 0;
 const frame = now => {
  let dt = Math.min(.05, (now - last) / 1000); last = now;
  if (document.hidden) { requestAnimationFrame(frame); return; }
  if (state === 'play') { acc += dt; while (acc >= 1 / 120) { step(1 / 120); acc -= 1 / 120; if (state !== 'play') break; } bgX += speed * dt; }
  else if (state === 'dying') { const s = dt * slow; tick(s); bgX += speed * s * .3; t += s; }
  else { t += dt; if (state === 'ready') bgX += 60 * dt; }
  if (state === 'play') tick(dt);
  draw();
  requestAnimationFrame(frame);
 };
 requestAnimationFrame(frame);
 cv.tabIndex = 0;
 window.signalRun = { get state() { return state; }, get score() { return Math.floor(score); }, get run() { return run; }, jump, press, peek: () => ({ obs, P, GROUND, speed }), duck: d => { downHeld = d; } }; // for Carbon's tests
})();
