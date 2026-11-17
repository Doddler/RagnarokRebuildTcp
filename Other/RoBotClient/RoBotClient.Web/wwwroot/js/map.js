// Live top-down bot map. The component pushes a payload ~10x/s; an animation-frame loop paints it, gliding
// bots/monsters between cells and running the HP-trail and damage-number animations ported from the Unity client.
// World Y increases UPWARD, so canvas Y is flipped: canvasY = (height-1 - worldY) * cell.

const COLORS = {
    bg: "#10131a",
    walkable: "#3a4254",
    blocked: "#191c24",
    self: "#3b9dff",
    monster: "#e5484d",
    path: "#ffe066",
    text: "#cbd2dc",
    textDim: "#7c8493",
};

const font = (px, bold = false) => `${bold ? "bold " : ""}${px}px system-ui, sans-serif`;
const clamp01 = v => Math.max(0, Math.min(1, v));
const hpColor = pct => pct <= 0.25 ? "#dc3545" : pct <= 0.5 ? "#ffc107" : "#198754";
const dpr = () => window.devicePixelRatio || 1;
const overlaps = (a, b) => a[0] < b[2] && a[2] > b[0] && a[1] < b[3] && a[3] > b[1]; // [left, top, right, bottom]

// ---------------------------------------------------------------------------------------------------------------
// Per-canvas state. Everything the canvas remembers between frames lives in one object.
// ---------------------------------------------------------------------------------------------------------------

const states = new WeakMap();

function stateOf(canvas) {
    let s = states.get(canvas);
    if (!s) states.set(canvas, s = {
        terrain: null,        // { map, img, w, h, spawns }
        payload: null,        // latest payload (without terrain cells)
        animating: false,
        hover: [],            // tooltip targets: points { x, y, name } and rects { left, top, width, height, name }
        glides: new Map(),    // "b:name" / "m:id" -> { x, y, seen }   drawn position, eased toward the server cell
        trails: new Map(),    // monster id -> { delayed, target, accel, moving, seen }   HP bar damage trail
        plates: new Map(),    // bot name -> name-plate opacity
        lastHp: new Map(),    // monster id -> hp at the previous frame (a drop spawns damage numbers)
        numbers: [],          // flying damage numbers
        payloadAt: 0,         // performance.now() when the latest payload arrived (its atk/hit ages are as of then)
        deadAt: new Map(),    // monster id -> when it was first seen dead (the death animation plays from there)
    });
    return s;
}

// ---------------------------------------------------------------------------------------------------------------
// Entry point and animation loop
// ---------------------------------------------------------------------------------------------------------------

export function drawWorld(canvas, payload) {
    if (!canvas) return;
    const s = stateOf(canvas);
    if (!payload || !payload.ok) {
        s.payload = null;
        message(canvas, payload?.message || "no data");
        return;
    }
    if (payload.cells) s.terrain = buildTerrain(payload);
    s.payload = { ...payload, cells: undefined };
    s.payloadAt = performance.now();
    if (!s.animating) startLoop(canvas, s);
}

function startLoop(canvas, s) {
    s.animating = true;
    let last = performance.now();
    const step = now => {
        if (!s.payload || !canvas.isConnected) { s.animating = false; return; }
        const dt = Math.min(0.1, (now - last) / 1000);
        last = now;
        tickTrails(s, dt, now);
        render(canvas, s, glideAll(s, s.payload, dt, now), now);
        requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
}

// ---------------------------------------------------------------------------------------------------------------
// Frame
// ---------------------------------------------------------------------------------------------------------------

function render(canvas, s, payload, now) {
    const ctx = canvas.getContext("2d");
    if (!ctx) return;
    const t = s.terrain;
    if (!t || t.map !== payload.map) { message(canvas, "loading map…"); return; }

    const v = makeView(canvas, ctx, t, now);
    const bots = payload.bots || [];
    const mobs = layoutMobs(v, s, payload);

    drawTerrain(v, payload.minimap);
    const spawnHover = payload.showSpawnAreas !== false ? drawSpawnAreas(v, t.spawns) : [];
    const items = (payload.items || []).map(it => drawItem(v, it));
    mobs.forEach(m => drawMob(v, m));
    bots.forEach(b => drawPath(v, b));
    bots.forEach(b => drawWanderTarget(v, b));
    bots.forEach(b => drawBot(v, s, b, mobs));
    mobs.forEach(m => drawHpBar(v, s, m)); // over the name plates
    bots.forEach(b => b.shout && drawShout(v, b.shout, v.toX(b.x), v.toY(b.y) - v.r));
    spawnDamageNumbers(s, mobs, bots, now);
    drawDamageNumbers(v, s, mobs);
    drawLegend(v, payload, bots);
    if (payload.toast) drawToast(v, payload.toast);

    s.hover = [...spawnHover, ...items, ...mobs.map(m => m.hover), ...bots.flatMap(b => botHover(v, b))];
    attachHover(canvas, s);
}

/** Geometry and drawing context shared by every draw function for one frame. */
function makeView(canvas, ctx, t, now) {
    const { pw, ph } = fitCanvas(canvas);
    // Stretch the map to fill the canvas: x and y scale independently (a 400x400 map fills a wide card).
    const sx = pw / t.w, sy = ph / t.h;
    const fs = Math.max(11, Math.round(Math.min(pw, ph) * 0.022));
    const r = Math.max(4, Math.min(sx, sy) * 1.6);
    return {
        canvas, ctx, t, now, pw, ph, sx, sy, fs, r,
        mr: Math.max(3.5, r * 0.8),          // monster dot radius
        spriteHeight: 30 * dpr(),            // monster sprites ~30 css px tall
        toX: x => (x + 0.5) * sx,
        toY: y => (t.h - 1 - y + 0.5) * sy,
    };
}

function fitCanvas(canvas) {
    // Backing store matches the CSS box (any aspect), crisp on HiDPI.
    const pw = Math.round((canvas.clientWidth || canvas.width || 420) * dpr());
    const ph = Math.round((canvas.clientHeight || canvas.height || 420) * dpr());
    if (canvas.width !== pw || canvas.height !== ph) {
        canvas.width = pw;
        canvas.height = ph;
    }
    return { pw, ph };
}

function message(canvas, text) {
    const ctx = canvas.getContext("2d");
    if (!ctx) return;
    const { pw, ph } = fitCanvas(canvas);
    ctx.fillStyle = COLORS.bg;
    ctx.fillRect(0, 0, pw, ph);
    ctx.fillStyle = COLORS.textDim;
    ctx.font = font(Math.max(12, Math.round(Math.min(pw, ph) * 0.04)));
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(text, pw / 2, ph / 2);
    ctx.textAlign = "start";
    ctx.textBaseline = "alphabetic";
}

// ---------------------------------------------------------------------------------------------------------------
// Images
// ---------------------------------------------------------------------------------------------------------------

// Images by URL (minimap art, monster atlases): undefined while loading, null if missing.
const images = new Map();

/** Same lazy cache for the per-species monster animation JSON (MonsterSprites.Anim). */
const anims = new Map();
function animData(url) {
    if (!url) return null;
    if (!anims.has(url)) {
        anims.set(url, undefined);
        fetch(url).then(r => r.ok ? r.json() : null).then(a => anims.set(url, a), () => anims.set(url, null));
    }
    return anims.get(url) || null;
}

function image(url) {
    if (!url) return null;
    if (!images.has(url)) {
        images.set(url, undefined);
        const img = new Image();
        img.onload = () => images.set(url, img);
        img.onerror = () => images.set(url, null);
        img.src = url;
    }
    return images.get(url) || null;
}

// ---------------------------------------------------------------------------------------------------------------
// Terrain and spawn areas
// ---------------------------------------------------------------------------------------------------------------

const rgb = hex => [1, 3, 5].map(i => parseInt(hex.slice(i, i + 2), 16));

/** The terrain arrives once per map as a "0101..." string (row-major, world y=0 = bottom row); cache it as a
 *  1px-per-cell image, used until the minimap art loads (or when the map has none). */
function buildTerrain(payload) {
    const { width: w, height: h, cells } = payload;
    const img = document.createElement("canvas");
    img.width = w;
    img.height = h;
    const ictx = img.getContext("2d");
    const data = ictx.createImageData(w, h);
    const walk = rgb(COLORS.walkable), block = rgb(COLORS.blocked);
    for (let y = 0; y < h; y++) {
        const row = (h - 1 - y) * w; // flip Y
        for (let x = 0; x < w; x++) {
            const c = cells.charCodeAt(x + y * w) === 49 ? walk : block; // '1'
            data.data.set([...c, 255], (row + x) * 4);
        }
    }
    ictx.putImageData(data, 0, 0);
    return { map: payload.map, img, w, h, spawns: payload.spawns || [] };
}

function drawTerrain(v, minimapUrl) {
    const { ctx, t } = v;
    ctx.fillStyle = COLORS.bg;
    ctx.fillRect(0, 0, v.pw, v.ph);
    const art = image(minimapUrl);
    ctx.imageSmoothingEnabled = !!art;
    ctx.drawImage(art || t.img, 0, 0, t.w * v.sx, t.h * v.sy);
}

function hue(name) {
    let hash = 0;
    for (let i = 0; i < name.length; i++) hash = (hash * 31 + name.charCodeAt(i)) | 0;
    return (hash >>> 0) % 360;
}

/** Fixed spawn zones (inclusive world-cell rectangles), drawn lightly. Returns their hover rectangles. */
function drawSpawnAreas(v, spawns) {
    const { ctx, t, sx, sy } = v;
    return spawns.map(area => {
        const left = area.minX * sx, top = (t.h - 1 - area.maxY) * sy;
        const width = (area.maxX - area.minX + 1) * sx, height = (area.maxY - area.minY + 1) * sy;
        const h = hue(area.name || "");
        ctx.fillStyle = `hsla(${h}, 85%, 60%, 0.10)`;
        ctx.strokeStyle = `hsla(${h}, 90%, 68%, 0.82)`;
        ctx.lineWidth = Math.max(1, dpr());
        ctx.fillRect(left, top, width, height);
        ctx.strokeRect(left + 0.5, top + 0.5, Math.max(0, width - 1), Math.max(0, height - 1));
        return { left, top, width, height, name: `${area.name} spawn area · ${area.count} mob(s)` };
    });
}

// ---------------------------------------------------------------------------------------------------------------
// Monsters
// ---------------------------------------------------------------------------------------------------------------

/** Screen layout for each monster (nearer the bottom drawn on top): the sprite's layers for the current animation
 *  frame (or a single first-frame rect until the animation JSON loads), HP bar anchor, hover point, and the box it
 *  covers (for fading bot name plates). A red dot until the atlas loads, or with no sprite. */
function layoutMobs(v, s, payload) {
    for (const [id, t] of s.deadAt) if (v.now - t > 5000) s.deadAt.delete(id);
    return [...(payload.mobs || [])].sort((a, b) => b.y - a.y).map(m => {
        const x = v.toX(m.x), y = v.toY(m.y);
        const sp = payload.sprites?.[m.name];
        const atlas = sp ? image(sp.atlas) : null;
        const barSpace = m.hp != null ? 12 : 0;
        const name = m.name || "monster";
        if (atlas) {
            const k = Math.min(1, v.spriteHeight / Math.max(sp.w, sp.h)); // same size as the first-frame portrait
            const feet = y + v.mr;
            const a = animData(sp.anim);
            const pose = a && animFrame(s, m, a, v.now);
            const layers = pose
                ? pose.layers.filter(l => l[0] >= 0 && a.rects[l[0]]).map(([i, lx, ly, mirror, scx, scy, alpha]) => {
                    const r = a.rects[i], w = r[2] * Math.abs(scx) * k, h = r[3] * Math.abs(scy) * k;
                    return { r, left: x + lx * k - w / 2, top: feet + ly * k - h / 2, w, h, mirror: !!mirror !== scx < 0, alpha: alpha * pose.alpha };
                })
                : [{ r: [sp.x, sp.y, sp.w, sp.h], left: x - sp.w * k / 2, top: feet - sp.h * k, w: sp.w * k, h: sp.h * k, mirror: false, alpha: 1 }];
            if (!layers.length) return null;
            const left = Math.min(...layers.map(l => l.left)), right = Math.max(...layers.map(l => l.left + l.w));
            const top = Math.min(...layers.map(l => l.top)), bottom = Math.max(...layers.map(l => l.top + l.h));
            return { m, x, y, sprite: { atlas, layers }, barY: top - 3,
                hover: { x, y: (top + bottom) / 2, name }, box: [left, top - barSpace, right, bottom] };
        }
        return { m, x, y, sprite: null, barY: y - v.mr - 3,
            hover: { x, y, name }, box: [x - v.mr, y - v.mr - barSpace, x + v.mr, y + v.mr] };
    }).filter(Boolean);
}

// Monster motions as RoAnimationHelper.GetMotionIdForSprite lays them out: action = motion * 8 + facing.
const MOTION = { idle: 0, walk: 8, attack: 16, hit: 24, dead: 32 };

/** The animation frame to show now, like RoSpriteAnimator: dead plays once and holds its last frame (then the corpse
 *  fades, as the server already removed it), a swing or a flinch plays once from when it happened (whichever is
 *  newer), walking and idle loop. Facing is the server's Direction, which matches the sprite's facing order. */
function animFrame(s, m, a, now) {
    const since = performance.now() - s.payloadAt; // the ages below are as of the last payload
    const once = (motion, age) => {
        const act = a.actions[motion + (m.dir & 7)] || a.actions[motion];
        if (!act?.frames.length) return null;
        const i = Math.floor(age / Math.max(1, act.delay));
        return i < act.frames.length || motion === MOTION.dead ? act.frames[Math.min(i, act.frames.length - 1)] : null;
    };
    let alpha = 1, frame = null;
    if (m.dead) {
        if (!s.deadAt.has(m.id)) s.deadAt.set(m.id, now);
        const age = now - s.deadAt.get(m.id);
        frame = once(MOTION.dead, age);
        alpha = clamp01((1900 - age) / 700); // fade out before WorldMap drops the corpse at 2 s
    } else {
        const atk = m.atk + since, hit = m.hit + since;
        frame = atk <= hit ? once(MOTION.attack, atk) || once(MOTION.hit, hit) : once(MOTION.hit, hit) || once(MOTION.attack, atk);
    }
    if (!frame) {
        const motion = m.walk ? MOTION.walk : MOTION.idle;
        const act = a.actions[motion + (m.dir & 7)] || a.actions[0];
        if (!act?.frames.length) return null;
        const delay = Math.max(1, act.delay);
        // RoSpriteAnimator walks by distance: frame = cells * 4.6 * 0.37 * 4 / (delay / 24); idle loops on its delay.
        const step = m.walk && m.cell > 0 ? (now / 1000 / m.cell) * 6.808 * 24 / delay : now / delay;
        frame = act.frames[Math.floor(step) % act.frames.length];
    }
    return { layers: frame.layers ?? frame, alpha };
}

/** An item on the ground: its inventory icon (half a monster sprite), else a small gold diamond. Returns its hover point. */
function drawItem(v, it) {
    const { ctx } = v;
    const x = v.toX(it.x), y = v.toY(it.y), icon = image(it.icon), k = v.spriteHeight * 0.5;
    if (icon) {
        ctx.imageSmoothingEnabled = true;
        ctx.drawImage(icon, x - k / 2, y - k / 2, k, k);
    } else {
        const d = v.mr * 0.8;
        ctx.fillStyle = "#ffd54a";
        ctx.strokeStyle = "#000000cc";
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(x, y - d); ctx.lineTo(x + d, y); ctx.lineTo(x, y + d); ctx.lineTo(x - d, y);
        ctx.closePath();
        ctx.fill();
        ctx.stroke();
    }
    return { x, y, name: it.count > 1 ? `${it.name} x${it.count}` : it.name };
}

function drawMob(v, { x, y, sprite }) {
    const { ctx } = v;
    if (sprite) {
        ctx.imageSmoothingEnabled = true;
        for (const { r, left, top, w, h, mirror, alpha } of sprite.layers) {
            if (alpha <= 0) continue;
            ctx.save();
            ctx.globalAlpha = clamp01(alpha);
            if (mirror) { ctx.translate(left * 2 + w, 0); ctx.scale(-1, 1); } // flip about the layer's centre
            ctx.drawImage(sprite.atlas, r[0], r[1], r[2], r[3], left, top, w, h);
            ctx.restore();
        }
        return;
    }
    ctx.fillStyle = COLORS.monster;
    ctx.strokeStyle = "#000000cc";
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.arc(x, y, v.mr, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
}

// HP bar "damage trail", copied from the Unity client's SliderBar: on a hit the bar drops at once and a white chunk
// (ProgressDelayed) stays behind, then drains with accelerating speed (accel += 20/s each second) until it meets the bar.
function trailFor(s, id, pct, now) {
    let tr = s.trails.get(id);
    if (!tr) s.trails.set(id, tr = { delayed: pct, accel: 0, moving: false });
    tr.seen = now;
    tr.target = pct;
    if (pct >= tr.delayed) { tr.delayed = pct; tr.moving = false; }
    else if (!tr.moving) { tr.moving = true; tr.accel = 0; }
    return tr;
}

function tickTrails(s, dt, now) {
    for (const [id, tr] of s.trails) {
        if (now - tr.seen > 2000) { s.trails.delete(id); continue; }
        if (!tr.moving) continue;
        tr.delayed -= tr.accel * dt;
        tr.accel += dt * 20;
        if (tr.delayed < tr.target) tr.moving = false; // caught up with the bar, like SliderBar.Update
    }
}

/** Dueled monsters only (hp is null otherwise): a small bar above the sprite, coloured like the bots' HP bars. */
function drawHpBar(v, s, { m, x, barY }) {
    if (m.hp == null) return;
    const { ctx } = v;
    const w = Math.max(18, v.spriteHeight * 1.2), h = Math.max(3, v.spriteHeight * 0.16);
    const left = x - w / 2, top = barY - h;
    const pct = clamp01(m.hp / m.maxHp);
    const tr = trailFor(s, m.id, pct, v.now);
    ctx.fillStyle = "#000000cc";
    ctx.fillRect(left - 1, top - 1, w + 2, h + 2);
    if (tr.moving && tr.delayed > pct) {
        ctx.fillStyle = "#ffffff";
        ctx.fillRect(left, top, w * tr.delayed, h);
    }
    ctx.fillStyle = hpColor(pct);
    ctx.fillRect(left, top, w * pct, h);
}

// ---------------------------------------------------------------------------------------------------------------
// Bots
// ---------------------------------------------------------------------------------------------------------------

/** Planned walk of a traveling bot: dashed line along its A* path, ring on the goal cell. */
function drawPath(v, b) {
    const p = b.path || [];
    if (p.length < 4) return;
    const { ctx, toX, toY } = v;
    ctx.strokeStyle = COLORS.path;
    ctx.lineWidth = 2;
    ctx.setLineDash([6, 4]);
    ctx.beginPath();
    ctx.moveTo(toX(b.x), toY(b.y));
    for (let i = 0; i < p.length; i += 2) ctx.lineTo(toX(p[i]), toY(p[i + 1]));
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.beginPath();
    ctx.arc(toX(p[p.length - 2]), toY(p[p.length - 1]), Math.max(4, v.r), 0, Math.PI * 2);
    ctx.stroke();
}

/** Wander A* destination: a gold dot, separate from the bot's current position. */
function drawWanderTarget(v, b) {
    if (!b.target) return;
    const { ctx } = v;
    ctx.fillStyle = COLORS.path;
    ctx.strokeStyle = "#171717";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(v.toX(b.target.x), v.toY(b.target.y), Math.max(4, v.r * 0.9), 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
}

function drawBot(v, s, b, mobs) {
    const { ctx } = v;
    const x = v.toX(b.x), y = v.toY(b.y);
    ctx.fillStyle = COLORS.self;
    ctx.beginPath();
    ctx.arc(x, y, v.r, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = "#ffffffaa";
    ctx.lineWidth = 1.5;
    ctx.stroke();
    drawNamePlate(v, s, b.name || "?", x, y + v.r, mobs);
}

/** Unity's ItemObtainedToast: icon + "Jellopy - 1 obtained." at the top middle, name-plate style. */
function drawToast(v, { text, icon }) {
    const { ctx } = v;
    const size = Math.max(10, Math.round(v.fs * 0.9)), img = image(icon), k = img ? size * 1.6 : 0;
    ctx.font = font(size, true);
    ctx.textBaseline = "middle";
    const w = ctx.measureText(text).width + (k ? k + 6 : 0), h = Math.max(size, k) + 8;
    const left = ctx.canvas.width / 2 - w / 2, top = 8 * dpr(), mid = top + h / 2;
    ctx.fillStyle = "#000000aa";
    ctx.fillRect(left - 6, top, w + 12, h);
    if (img) ctx.drawImage(img, left, mid - k / 2, k, k);
    ctx.fillStyle = COLORS.text;
    ctx.fillText(text, left + (k ? k + 6 : 0), mid);
}

/** Skill shout ("Bash!!") above the dot, styled like the name plate. */
function drawShout(v, text, x, above) {
    const { ctx } = v;
    const size = Math.max(9, Math.round(v.fs * 0.75));
    ctx.font = font(size, true);
    ctx.textBaseline = "middle";
    const w = ctx.measureText(text).width;
    const y = above - 3 - size / 2 - 2;
    ctx.fillStyle = "#000000aa";
    ctx.fillRect(x - w / 2 - 3, y - size / 2 - 2, w + 6, size + 4);
    ctx.fillStyle = COLORS.text;
    ctx.fillText(text, x - w / 2, y);
}

/** Name centred under the bot dot. A monster under the plate fades it so the monster shows through. */
function drawNamePlate(v, s, label, x, below, mobs) {
    const { ctx } = v;
    const size = Math.max(9, Math.round(v.fs * 0.75));
    ctx.font = font(size);
    ctx.textBaseline = "middle";
    const w = ctx.measureText(label).width;
    const y = below + 3 + size / 2 + 2;
    const box = [x - w / 2 - 3, y - size / 2 - 2, x + w / 2 + 3, y + size / 2 + 2];
    const covered = mobs.some(m => overlaps(m.box, box));
    ctx.globalAlpha = plateFade(s, label, covered ? 0.65 : 1);
    ctx.fillStyle = "#000000aa";
    ctx.fillRect(box[0], box[1], box[2] - box[0], box[3] - box[1]);
    ctx.fillStyle = COLORS.text;
    ctx.fillText(label, x - w / 2, y);
    ctx.globalAlpha = 1;
}

/** Name-plate opacity per bot, eased toward its target each frame (~0.15 s) so the fade isn't a flicker. */
function plateFade(s, key, target) {
    const a = s.plates.get(key) ?? 1;
    const next = a + (target - a) * 0.2;
    s.plates.set(key, next);
    return next;
}

function botHover(v, b) {
    const name = b.name || "bot";
    const points = [{ x: v.toX(b.x), y: v.toY(b.y), name }];
    if (b.target) points.push({ x: v.toX(b.target.x), y: v.toY(b.target.y), name: `${name} A* target` });
    return points;
}

/** Top-left box: map title, then one row per bot with tiny HP (green/yellow/red) and SP bars. */
function drawLegend(v, payload, bots) {
    const { ctx, canvas, t, fs } = v;
    ctx.font = font(fs);
    const aspect = (canvas.clientWidth / Math.max(1, canvas.clientHeight)).toFixed(2);
    const title = `${payload.map}  ${t.w}×${t.h} cells  ·  shown ${canvas.clientWidth}×${canvas.clientHeight}px (${aspect}:1)`
        + `  ·  ${bots.length} bot(s)  ·  ${(payload.mobs || []).length} mob(s) in view`;
    const rowHeight = fs + 6, barWidth = Math.round(fs * 6), barHeight = Math.max(4, Math.round(fs * 0.4));
    const numbers = b => `${b.hp}/${b.maxHp}  ${b.sp}/${b.maxSp}`;
    const widest = f => Math.max(0, ...bots.map(b => ctx.measureText(f(b)).width));
    const nameWidth = widest(b => b.name || "?");
    const boxWidth = Math.max(ctx.measureText(title).width, bots.length ? nameWidth + barWidth + widest(numbers) + 20 : 0) + 10;

    ctx.textBaseline = "top";
    ctx.fillStyle = "#000000aa";
    ctx.fillRect(4, 4, boxWidth, fs + 8 + bots.length * rowHeight);
    ctx.fillStyle = COLORS.text;
    ctx.fillText(title, 9, 8);
    const barX = 9 + nameWidth + 8;
    const bar = (y, pct, color) => {
        ctx.fillStyle = "#ffffff22";
        ctx.fillRect(barX, y, barWidth, barHeight);
        ctx.fillStyle = color;
        ctx.fillRect(barX, y, barWidth * pct, barHeight);
    };
    bots.forEach((b, i) => {
        const top = 8 + fs + 4 + i * rowHeight;
        const hp = b.maxHp > 0 ? Math.min(1, b.hp / b.maxHp) : 0;
        const sp = b.maxSp > 0 ? Math.min(1, b.sp / b.maxSp) : 0;
        ctx.fillStyle = COLORS.text;
        ctx.fillText(b.name || "?", 9, top);
        bar(top + 1, hp, hpColor(hp));
        bar(top + 1 + barHeight + 2, sp, "#0dcaf0");
        ctx.fillStyle = COLORS.text;
        ctx.fillText(numbers(b), barX + barWidth + 8, top);
    });
    ctx.textBaseline = "alphabetic";
}

// ---------------------------------------------------------------------------------------------------------------
// Smooth movement
// ---------------------------------------------------------------------------------------------------------------

// The server sends cell positions ~10x/s, so drawn as-is bots hop cell to cell. Each entity glides toward its
// latest cell at walking pace; jumps over 12 cells (warp, Fly Wing, a new mob) snap instead of sliding.
function glide(s, key, x, y, dt, now) {
    let g = s.glides.get(key);
    if (!g || Math.max(Math.abs(x - g.x), Math.abs(y - g.y)) > 12) s.glides.set(key, g = { x, y });
    else {
        const dx = x - g.x, dy = y - g.y, d = Math.hypot(dx, dy);
        const len = Math.max(7, d / 0.2) * dt; // ~7 cells/s, faster if we fell behind
        if (d <= len) { g.x = x; g.y = y; } else { g.x += dx / d * len; g.y += dy / d * len; }
    }
    g.seen = now;
    return { x: g.x, y: g.y };
}

function glideAll(s, p, dt, now) {
    const out = {
        ...p,
        bots: (p.bots || []).map(b => ({ ...b, ...glide(s, "b:" + b.name, b.x, b.y, dt, now) })),
        mobs: (p.mobs || []).map(m => ({ ...m, ...glide(s, "m:" + m.id, m.x, m.y, dt, now) })),
    };
    for (const [k, g] of s.glides) if (now - g.seen > 2000) s.glides.delete(k);
    return out;
}

// ---------------------------------------------------------------------------------------------------------------
// Damage numbers, ported from the Unity client
// ---------------------------------------------------------------------------------------------------------------

// DamageIndicator.OnUpdate driven by Data/FlyingText/*.asset. Keys are Unity AnimationCurve keys
// [time, value, inSlope, outSlope] over a 0..1 lifetime of `tween` seconds.
// Damage: starts where it spawned, drifts sideways away from the attacker while arcing up and down, punches in at
// 5x size then settles, and fades out.
const DAMAGE_PATH = {
    trajectory: [[0.0019874573, -0.0014190674, 4.052195, 4.052195], [0.9942106, 0.011132479, -3.9042158, -3.9042158]],
    size: [[-0.0009186715, 5.0193124, -14.1374855, -14.1374855], [0.3964636, 0.99757385, -0.60127693, -0.3272466], [1, 0.8000686, -0.3272466, -0.3272466]],
    alpha: [[0, 1, -0.00026645948, -0.00026645948], [0.11452991, 0.9999695, -0.00026645948, -0.075074494], [0.56060225, 0.70585805, -1.3047085, -1.4544252], [1.0042865, 0.006022349, -0.2006653, -0.2006653]],
    tween: 1, height: 4, distance: 4, color: "#ffffff",
    maxSize: 2.5, // minimap: Unity pops to 5x, capped so the yellow total stays the biggest number
};
// Multi-hit (Double Attack): EntityMessageQueue.SendDamageEvent shows each hit 0.2 s apart, and ServerControllable
// .AttachDamageIndicator adds a yellow running total (ComboPath.asset) that rides with the monster, each new hit
// replacing the previous total (ComboIndicator.EndDamageIndicator).
const COMBO_PATH = {
    trajectory: [[0.0039138794, 0.51712036, 0.4858895, 0.4858895], [0.9932951, 0.9978503, 0.4858895, 0.4858895]],
    size: [[0.0007741172, 0.0036451616, 18.02707, 18.02707], [0.10320092, 1.4825153, 2.0349398, 0.004584544], [0.9957344, 1.4866072, 0.004584544, 0.004584544]],
    alpha: [[0, 1, -0.00006110869, -0.00006110869], [0.49939838, 0.9999695, -0.00006110869, -0.075074494], [0.75252, 0.70585805, -2.4358597, -2.8889782], [1.0042865, 0.006022349, -0.2006653, -0.2006653]],
    tween: 3, height: 6, distance: 0, color: "#ffff00",
    scale: 2, // minimap: settles at ~3x, above any (capped) white hit
};
const HIT_INTERVAL_MS = 200;

/** Unity's AnimationCurve.Evaluate: cubic Hermite between keys, clamped at the ends. */
function evalCurve(keys, t) {
    if (t <= keys[0][0]) return keys[0][1];
    for (let i = 0; i < keys.length - 1; i++) {
        const [t0, v0, , o0] = keys[i], [t1, v1, i1] = keys[i + 1];
        if (t > t1) continue;
        const d = t1 - t0, u = (t - t0) / d, u2 = u * u, u3 = u2 * u;
        return (2 * u3 - 3 * u2 + 1) * v0 + (u3 - 2 * u2 + u) * d * o0 + (-2 * u3 + 3 * u2) * v1 + (u3 - u2) * d * i1;
    }
    return keys[keys.length - 1][1];
}

function nearestBot(bots, m) {
    let near = null, best = Infinity;
    for (const b of bots) {
        const d = Math.hypot(b.x - m.x, b.y - m.y);
        if (d < best) { best = d; near = b; }
    }
    return near;
}

/** A dueled monster's HP dropped since last frame: one white number per hit (0.2 s apart), flying away from the
 *  nearest bot (the attacker), plus the yellow running total on multi-hits. */
function spawnDamageNumbers(s, mobs, bots, now) {
    for (const { m, x, barY } of mobs) {
        if (m.hp == null) continue;
        const before = s.lastHp.get(m.id);
        s.lastHp.set(m.id, m.hp);
        if (before == null || m.hp >= before) continue;
        const near = nearestBot(bots, m);
        const dir = near && near.x !== m.x ? Math.sign(m.x - near.x) : (Math.random() < 0.5 ? -1 : 1);
        const hits = Math.max(1, m.hits || 1), per = Math.round((before - m.hp) / hits);
        for (let h = 0; h < hits; h++) {
            const born = now + HIT_INTERVAL_MS * h;
            s.numbers.push({ path: DAMAGE_PATH, value: per, x, y: barY - 6, dir, born });
            if (hits > 1) s.numbers.push({ path: COMBO_PATH, value: per * (h + 1), id: m.id, born });
        }
    }
}

function drawDamageNumbers(v, s, mobs) {
    const { ctx, now } = v;
    const barAt = new Map(mobs.map(({ m, x, barY }) => [m.id, [x, barY - 6]]));
    // A combo total that has started ends the older totals on the same monster.
    const newestCombo = new Map();
    for (const n of s.numbers)
        if (n.id != null && n.born <= now && n.born > (newestCombo.get(n.id) ?? -1)) newestCombo.set(n.id, n.born);
    const lifeOf = n => (now - n.born) / 1000 / n.path.tween;
    s.numbers = s.numbers.filter(n => lifeOf(n) <= 1 && !(n.id != null && n.born < (newestCombo.get(n.id) ?? -1)));

    ctx.textAlign = "center";
    ctx.textBaseline = "alphabetic";
    for (const n of s.numbers) {
        const life = lifeOf(n);
        if (life < 0) continue; // a later hit of a multi-hit, not shown yet
        if (n.id != null && barAt.has(n.id)) [n.x, n.y] = barAt.get(n.id); // the total rides with the monster
        drawNumber(v, n, life);
    }
    ctx.globalAlpha = 1;
    ctx.textAlign = "start";
}

function drawNumber(v, n, life) {
    const { ctx } = v, P = n.path;
    const unit = v.spriteHeight * 0.5; // one Unity world unit on the minimap, relative to the sprite
    const size = Math.min(evalCurve(P.size, life), P.maxSize ?? Infinity) * (P.scale ?? 1);
    const x = n.x + (n.dir || 0) * P.distance * unit * life * 0.5;
    const y = n.y - evalCurve(P.trajectory, life) * P.height * unit * 0.6; // minimap: Unity's full arc (4 units) read too high, 0.3 too low
    ctx.globalAlpha = clamp01(evalCurve(P.alpha, life));
    ctx.font = font(Math.round(9 * dpr() * size), true);
    ctx.lineWidth = Math.max(2, 2.5 * size);
    ctx.strokeStyle = "#000000";
    ctx.strokeText(String(n.value), x, y);
    ctx.fillStyle = P.color;
    ctx.fillText(String(n.value), x, y);
}

// ---------------------------------------------------------------------------------------------------------------
// Hover tooltips
// ---------------------------------------------------------------------------------------------------------------

/** The native tooltip names the nearest dot under the pointer (monster or bot), else the spawn areas under it. */
function attachHover(canvas, s) {
    if (s.hoverAttached) return;
    s.hoverAttached = true;
    canvas.addEventListener("mousemove", e => {
        const rect = canvas.getBoundingClientRect();
        const k = canvas.width / rect.width;
        const px = (e.clientX - rect.left) * k, py = (e.clientY - rect.top) * k;
        canvas.title = pointAt(s.hover, px, py, 10 * k)?.name ?? spawnAreasAt(s.hover, px, py);
    });
}

function pointAt(targets, px, py, radius) {
    let best = null, bestDistance = radius;
    for (const h of targets) {
        if (h.left != null) continue;
        const d = Math.hypot(h.x - px, h.y - py);
        if (d < bestDistance) { bestDistance = d; best = h; }
    }
    return best;
}

// Overlapping spawn zones can cover most of the map: prefer the smallest, and list a few.
function spawnAreasAt(targets, px, py) {
    const areas = targets.filter(h => h.left != null
        && px >= h.left && px <= h.left + h.width && py >= h.top && py <= h.top + h.height)
        .sort((a, b) => a.width * a.height - b.width * b.height);
    if (!areas.length) return "";
    return areas.slice(0, 3).map(h => h.name).join("\n") + (areas.length > 3 ? `\n+${areas.length - 3} more spawn areas` : "");
}
