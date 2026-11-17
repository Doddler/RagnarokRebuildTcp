// Live top-down bot map. The component pushes a fresh payload ~4x/sec; we just paint it.
// World Y increases UPWARD, so canvas Y is flipped: canvasY = (size-1 - localY) * cell.

const COLORS = {
    bg: "#10131a",
    walkable: "#3a4254",
    blocked: "#191c24",
    grid: "#00000022",
    self: "#3b9dff",
    monster: "#e5484d",
    npc: "#9aa0ab",
    player: "#38c172",
    targetRing: "#ffd23f",
    text: "#cbd2dc",
    textDim: "#7c8493",
};

// Images by URL (minimap art, monster atlases): undefined while loading, null if missing.
const minimaps = new Map();

function minimap(url) {
    if (!url) return null;
    if (!minimaps.has(url)) {
        minimaps.set(url, undefined);
        const img = new Image();
        img.onload = () => minimaps.set(url, img);
        img.onerror = () => minimaps.set(url, null);
        img.src = url;
    }
    return minimaps.get(url) || null;
}

function fitCanvas(canvas) {
    // Backing store matches the CSS box (any aspect), crisp on HiDPI.
    const dpr = window.devicePixelRatio || 1;
    const pw = Math.round((canvas.clientWidth || canvas.width || 420) * dpr);
    const ph = Math.round((canvas.clientHeight || canvas.height || 420) * dpr);
    if (canvas.width !== pw || canvas.height !== ph) {
        canvas.width = pw;
        canvas.height = ph;
    }
    return { px: Math.min(pw, ph), pw, ph, dpr };
}

function message(ctx, px, text) {
    ctx.fillStyle = COLORS.bg;
    ctx.fillRect(0, 0, ctx.canvas.width, ctx.canvas.height);
    ctx.fillStyle = COLORS.textDim;
    ctx.font = `${Math.max(12, Math.round(px * 0.04))}px system-ui, sans-serif`;
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(text || "no data", ctx.canvas.width / 2, ctx.canvas.height / 2);
    ctx.textAlign = "start";
    ctx.textBaseline = "alphabetic";
}

// Whole-map overview: every bot on one map. The terrain arrives once per map as a "0101..." string
// (row-major, world y=0 = bottom row) and is cached as a 1px-per-cell image; later ticks only send bots.
const worldTerrain = new WeakMap(); // canvas -> { map, img, w, h }

function rgb(hex) {
    return [1, 3, 5].map(i => parseInt(hex.slice(i, i + 2), 16));
}

// Hover: the native tooltip names the nearest dot under the pointer (monster or bot).
const hoverTargets = new WeakMap(); // canvas -> [{ x, y, name }] in canvas pixels
const hoverAttached = new WeakSet();

function attachHover(canvas) {
    if (hoverAttached.has(canvas)) return;
    hoverAttached.add(canvas);
    canvas.addEventListener("mousemove", e => {
        const rect = canvas.getBoundingClientRect();
        const k = canvas.width / rect.width;
        const px = (e.clientX - rect.left) * k, py = (e.clientY - rect.top) * k;
        const targets = hoverTargets.get(canvas) || [];
        let best = null, bestDistance = 10 * k; // within 10 css px
        for (const h of targets) {
            if (h.left != null) continue;
            const d = Math.hypot(h.x - px, h.y - py);
            if (d < bestDistance) { bestDistance = d; best = h; }
        }
        if (best) {
            canvas.title = best.name;
            return;
        }

        // Overlapping spawn zones can cover most of the map. Prefer the most specific (smallest) zones,
        // and show a few when multiple monsters share the hovered cell instead of whichever drew first.
        const areas = targets.filter(h => h.left != null
            && px >= h.left && px <= h.left + h.width && py >= h.top && py <= h.top + h.height)
            .sort((a, b) => a.width * a.height - b.width * b.height);
        canvas.title = areas.length
            ? areas.slice(0, 3).map(h => h.name).join("\n") + (areas.length > 3 ? `\n+${areas.length - 3} more spawn areas` : "")
            : "";
    });
}

// HP bar "damage trail", copied from the Unity client's SliderBar: on a hit the bar drops at once and a white chunk
// (ProgressDelayed) stays behind, then drains with accelerating speed (accel += 20/s each second) until it meets the bar.
const hpTrails = new WeakMap(); // canvas -> Map(monster id -> { delayed, accel, moving })
const lastPayload = new WeakMap();
const animating = new WeakSet();

function trailFor(canvas, m, pct) {
    let trails = hpTrails.get(canvas);
    if (!trails) hpTrails.set(canvas, trails = new Map());
    let st = trails.get(m.id);
    if (!st) trails.set(m.id, st = { delayed: pct, accel: 0, moving: false, seen: 0 });
    st.seen = performance.now();
    st.target = pct;
    if (pct >= st.delayed) { st.delayed = pct; st.moving = false; }
    else if (!st.moving) { st.moving = true; st.accel = 0; }
    return st;
}

// The server sends cell positions ~10x/s, so drawn as-is bots hop cell to cell. Each payload is only stored; a
// per-canvas animation-frame loop redraws it with every bot/monster gliding toward its latest cell (and drains the
// HP trails). Jumps over 12 cells (warp, Fly Wing, a new mob) snap instead of sliding across the map.
const glides = new WeakMap(); // canvas -> Map(key -> { x, y, seen })

function smooth(canvas, p, dt, now) {
    let pos = glides.get(canvas);
    if (!pos) glides.set(canvas, pos = new Map());
    const glide = (key, x, y) => {
        let g = pos.get(key);
        if (!g || Math.max(Math.abs(x - g.x), Math.abs(y - g.y)) > 12) pos.set(key, g = { x, y });
        else {
            const dx = x - g.x, dy = y - g.y, d = Math.hypot(dx, dy);
            const len = Math.max(7, d / 0.2) * dt; // walking pace (~7 cells/s), faster if we fell behind
            if (d <= len) { g.x = x; g.y = y; } else { g.x += dx / d * len; g.y += dy / d * len; }
        }
        g.seen = now;
        return { x: g.x, y: g.y };
    };
    const out = {
        ...p,
        bots: (p.bots || []).map(b => ({ ...b, ...glide("b:" + b.name, b.x, b.y) })),
        mobs: (p.mobs || []).map(m => ({ ...m, ...glide("m:" + m.id, m.x, m.y) })),
    };
    for (const [k, g] of pos) if (now - g.seen > 2000) pos.delete(k);
    return out;
}

// Name-plate opacity per bot, eased toward its target each frame (~0.15 s) so the fade isn't a flicker.
const plateAlphas = new WeakMap();
function plateFade(canvas, key, target) {
    let m = plateAlphas.get(canvas);
    if (!m) plateAlphas.set(canvas, m = new Map());
    const a = (m.get(key) ?? 1) + (target - (m.get(key) ?? 1)) * 0.2;
    m.set(key, a);
    return a;
}

// Damage numbers, ported from the Unity client: DamageIndicator.OnUpdate driven by Data/FlyingText/DamagePath.asset.
// Keys are Unity AnimationCurve keys [time, value, inSlope, outSlope]; lifetime 0..1 over TweenTime = 1 s. The number
// starts at its spawn point (not attached to the monster), drifts sideways away from the attacker (DistanceMultiplier)
// while arcing up and back down (Trajectory x HeightMultiplier), punches in at 5x size then settles, and fades out.
const DAMAGE_PATH = {
    trajectory: [[0.0019874573, -0.0014190674, 4.052195, 4.052195], [0.9942106, 0.011132479, -3.9042158, -3.9042158]],
    size: [[-0.0009186715, 5.0193124, -14.1374855, -14.1374855], [0.3964636, 0.99757385, -0.60127693, -0.3272466], [1, 0.8000686, -0.3272466, -0.3272466]],
    alpha: [[0, 1, -0.00026645948, -0.00026645948], [0.11452991, 0.9999695, -0.00026645948, -0.075074494], [0.56060225, 0.70585805, -1.3047085, -1.4544252], [1.0042865, 0.006022349, -0.2006653, -0.2006653]],
    tween: 1, height: 4, distance: 4, color: "#ffffff",
};
// Multi-hit (Double Attack): EntityMessageQueue.SendDamageEvent shows each hit 0.2 s apart, and ServerControllable
// .AttachDamageIndicator adds a yellow (#FFFF00) running total on ComboPath.asset that rides with the monster, each
// new hit replacing the previous total (ComboIndicator.EndDamageIndicator).
const COMBO_PATH = {
    trajectory: [[0.0039138794, 0.51712036, 0.4858895, 0.4858895], [0.9932951, 0.9978503, 0.4858895, 0.4858895]],
    size: [[0.0007741172, 0.0036451616, 18.02707, 18.02707], [0.10320092, 1.4825153, 2.0349398, 0.004584544], [0.9957344, 1.4866072, 0.004584544, 0.004584544]],
    alpha: [[0, 1, -0.00006110869, -0.00006110869], [0.49939838, 0.9999695, -0.00006110869, -0.075074494], [0.75252, 0.70585805, -2.4358597, -2.8889782], [1.0042865, 0.006022349, -0.2006653, -0.2006653]],
    tween: 3, height: 6, distance: 0, color: "#ffff00",
};
function evalCurve(keys, t) { // Unity's cubic Hermite AnimationCurve.Evaluate, clamped at the ends
    if (t <= keys[0][0]) return keys[0][1];
    for (let i = 0; i < keys.length - 1; i++) {
        const [t0, v0, , o0] = keys[i], [t1, v1, i1] = keys[i + 1];
        if (t > t1) continue;
        const d = t1 - t0, u = (t - t0) / d, u2 = u * u, u3 = u2 * u;
        return (2 * u3 - 3 * u2 + 1) * v0 + (u3 - 2 * u2 + u) * d * o0 + (-2 * u3 + 3 * u2) * v1 + (u3 - u2) * d * i1;
    }
    return keys[keys.length - 1][1];
}
const lastHp = new WeakMap();   // canvas -> Map(monster id -> hp)
const floaters = new WeakMap(); // canvas -> [{ value, x, y, dir, born }]

function startLoop(canvas) {
    animating.add(canvas);
    let last = performance.now();
    const step = now => {
        const p = lastPayload.get(canvas);
        if (!p || !canvas.isConnected) { animating.delete(canvas); return; }
        const dt = Math.min(0.1, (now - last) / 1000);
        last = now;
        const trails = hpTrails.get(canvas);
        for (const [id, st] of trails || []) {
            if (now - st.seen > 2000) { trails.delete(id); continue; }
            if (!st.moving) continue;
            st.delayed -= st.accel * dt;
            st.accel += dt * 20;
            if (st.delayed < st.target) st.moving = false; // caught up with the bar, like SliderBar.Update
        }
        render(canvas, smooth(canvas, p, dt, now));
        requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
}

export function drawWorld(canvas, payload) {
    if (!canvas) return;
    if (!payload || !payload.ok || payload.cells) render(canvas, payload); // messages, and the one-off terrain build
    if (payload && payload.ok) {
        lastPayload.set(canvas, { ...payload, cells: undefined });
        if (!animating.has(canvas)) startLoop(canvas);
    } else lastPayload.delete(canvas);
}

function render(canvas, payload) {
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;
    const { px, pw, ph } = fitCanvas(canvas);

    if (!payload || !payload.ok) {
        message(ctx, px, payload && payload.message ? payload.message : "no data");
        return;
    }

    if (payload.cells) {
        const w = payload.width, h = payload.height, cells = payload.cells;
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
                const i = (row + x) * 4;
                data.data[i] = c[0]; data.data[i + 1] = c[1]; data.data[i + 2] = c[2]; data.data[i + 3] = 255;
            }
        }
        ictx.putImageData(data, 0, 0);
        worldTerrain.set(canvas, { map: payload.map, img, w, h, spawns: payload.spawns || [] });
    }

    const t = worldTerrain.get(canvas);
    if (!t || t.map !== payload.map) {
        message(ctx, px, "loading map…");
        return;
    }

    // Stretch the map to fill the canvas: x and y scale independently (a 400x400 map fills a wide card).
    const sx = pw / t.w, sy = ph / t.h, scale = Math.min(sx, sy);
    const ox = 0, oy = 0;
    ctx.fillStyle = COLORS.bg;
    ctx.fillRect(0, 0, pw, ph);
    const art = minimap(payload.minimap);
    ctx.imageSmoothingEnabled = !!art;
    ctx.drawImage(art || t.img, ox, oy, t.w * sx, t.h * sy);

    // Fixed monster spawn zones are inclusive world-cell rectangles. Draw them lightly so the map stays
    // readable, and retain their bounds for hover names/counts.
    const spawnHits = [];
    if (payload.showSpawnAreas !== false) {
        for (const area of t.spawns || []) {
            const left = ox + area.minX * sx;
            const top = oy + (t.h - 1 - area.maxY) * sy;
            const width = (area.maxX - area.minX + 1) * sx;
            const height = (area.maxY - area.minY + 1) * sy;
            let hash = 0;
            for (let i = 0; i < (area.name || "").length; i++) hash = (hash * 31 + area.name.charCodeAt(i)) | 0;
            const hue = (hash >>> 0) % 360;
            ctx.fillStyle = `hsla(${hue}, 85%, 60%, 0.10)`;
            ctx.strokeStyle = `hsla(${hue}, 90%, 68%, 0.82)`;
            ctx.lineWidth = Math.max(1, window.devicePixelRatio || 1);
            ctx.fillRect(left, top, width, height);
            ctx.strokeRect(left + 0.5, top + 0.5, Math.max(0, width - 1), Math.max(0, height - 1));
            spawnHits.push({ left, top, width, height, name: `${area.name} spawn area · ${area.count} mob(s)` });
        }
    }

    const fs = Math.max(11, Math.round(px * 0.022));
    const r = Math.max(4, scale * 1.6);
    ctx.font = `${fs}px system-ui, sans-serif`;
    ctx.textBaseline = "middle";
    // Monsters (only those inside some bot's view) under the bots, smaller and with a dark ring.
    // Monsters as their first sprite frame, feet on their cell, ~30 css px tall; nearer the bottom drawn on top.
    // A red dot until the atlas loads, or when the monster has no sprite.
    const mr = Math.max(3.5, r * 0.8);
    const spriteHeight = 30 * (window.devicePixelRatio || 1);
    const hits = [...spawnHits]; // rectangles and point positions for hover tooltips
    const mobs = [...(payload.mobs || [])].sort((a, b) => b.y - a.y);
    // Dueled monsters only (hp is null otherwise): a small bar above the sprite, coloured like the bots' HP bars.
    const hpBars = [];
    function hpBar(m, cx, bottom) {
        if (m.hp == null) return;
        const w = Math.max(18, spriteHeight * 1.2), h = Math.max(3, spriteHeight * 0.16);
        const pct = Math.max(0, Math.min(1, m.hp / m.maxHp));
        const st = trailFor(canvas, m, pct);
        ctx.fillStyle = "#000000cc";
        ctx.fillRect(cx - w / 2 - 1, bottom - h - 1, w + 2, h + 2);
        if (st.moving && st.delayed > pct) {
            ctx.fillStyle = "#ffffff";
            ctx.fillRect(cx - w / 2, bottom - h, w * st.delayed, h);
        }
        ctx.fillStyle = pct <= 0.25 ? "#dc3545" : pct <= 0.5 ? "#ffc107" : "#198754";
        ctx.fillRect(cx - w / 2, bottom - h, w * pct, h);
    }
    const mobRects = []; // [left, top, right, bottom] of each sprite/dot + its HP bar, for name-plate fading
    for (const m of mobs) {
        const mx = ox + (m.x + 0.5) * sx, my = oy + (t.h - 1 - m.y + 0.5) * sy;
        const s = payload.sprites && payload.sprites[m.name];
        const atlas = s ? minimap(s.atlas) : null;
        if (atlas) {
            const k = Math.min(1, spriteHeight / Math.max(s.w, s.h));
            const dw = s.w * k, dh = s.h * k;
            ctx.imageSmoothingEnabled = true;
            ctx.drawImage(atlas, s.x, s.y, s.w, s.h, mx - dw / 2, my - dh + mr, dw, dh);
            hits.push({ x: mx, y: my - dh / 2 + mr, name: m.name || "monster" });
            hpBars.push([m, mx, my - dh + mr - 3]); // drawn last, over bot name plates
            mobRects.push([mx - dw / 2, my - dh + mr - (m.hp != null ? 12 : 0), mx + dw / 2, my + mr]);
        } else {
            ctx.fillStyle = COLORS.monster;
            ctx.strokeStyle = "#000000cc";
            ctx.lineWidth = 1.5;
            ctx.beginPath();
            ctx.arc(mx, my, mr, 0, Math.PI * 2);
            ctx.fill();
            ctx.stroke();
            hits.push({ x: mx, y: my, name: m.name || "monster" });
            hpBars.push([m, mx, my - mr - 3]);
            mobRects.push([mx - mr, my - mr - (m.hp != null ? 12 : 0), mx + mr, my + mr]);
        }
    }
    for (const b of payload.bots || []) {
        hits.push({ x: ox + (b.x + 0.5) * sx, y: oy + (t.h - 1 - b.y + 0.5) * sy, name: b.name || "bot" });
        if (b.target) hits.push({ x: ox + (b.target.x + 0.5) * sx, y: oy + (t.h - 1 - b.target.y + 0.5) * sy, name: `${b.name || "bot"} A* target` });
    }
    hoverTargets.set(canvas, hits);
    attachHover(canvas);
    // Planned walk of traveling bots: dashed line from the bot along its A* path, ring on the goal cell.
    const toX = x => ox + (x + 0.5) * sx, toY = y => oy + (t.h - 1 - y + 0.5) * sy;
    for (const b of payload.bots || []) {
        const p = b.path || [];
        if (p.length < 4) continue;
        ctx.strokeStyle = "#ffe066";
        ctx.lineWidth = 2;
        ctx.setLineDash([6, 4]);
        ctx.beginPath();
        ctx.moveTo(toX(b.x), toY(b.y));
        for (let i = 0; i < p.length; i += 2) ctx.lineTo(toX(p[i]), toY(p[i + 1]));
        ctx.stroke();
        ctx.setLineDash([]);
        ctx.beginPath();
        ctx.arc(toX(p[p.length - 2]), toY(p[p.length - 1]), Math.max(4, r), 0, Math.PI * 2);
        ctx.stroke();
    }
    // Wander A* destinations: one gold dot per bot, separate from the bot's current position.
    for (const b of payload.bots || []) {
        if (!b.target) continue;
        const tx = toX(b.target.x), ty = toY(b.target.y);
        ctx.fillStyle = "#ffe066";
        ctx.strokeStyle = "#171717";
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.arc(tx, ty, Math.max(4, r * 0.9), 0, Math.PI * 2);
        ctx.fill();
        ctx.stroke();
    }
    for (const b of payload.bots || []) {
        const cx = ox + (b.x + 0.5) * sx;
        const cy = oy + (t.h - 1 - b.y + 0.5) * sy;
        ctx.fillStyle = COLORS.self;
        ctx.beginPath();
        ctx.arc(cx, cy, r, 0, Math.PI * 2);
        ctx.fill();
        ctx.strokeStyle = "#ffffffaa";
        ctx.lineWidth = 1.5;
        ctx.stroke();

        const label = b.name || "?";
        const nfs = Math.max(9, Math.round(fs * 0.75)); // name plate smaller than the title text
        ctx.font = `${nfs}px system-ui, sans-serif`;
        const lw = ctx.measureText(label).width;
        const ly = cy + r + 3 + nfs / 2 + 2; // centred under the dot
        const pl = cx - lw / 2 - 3, pt = ly - nfs / 2 - 2, pr = pl + lw + 6, pb = pt + nfs + 4;
        // A monster under the plate: fade it so the monster shows through, fade back in once it's clear.
        const covered = mobRects.some(([l, t2, rr, bb]) => l < pr && rr > pl && t2 < pb && bb > pt);
        const fade = plateFade(canvas, b.name, covered ? 0.65 : 1);
        ctx.globalAlpha = fade;
        ctx.fillStyle = "#000000aa";
        ctx.fillRect(pl, pt, pr - pl, pb - pt);
        ctx.fillStyle = COLORS.text;
        ctx.fillText(label, cx - lw / 2, ly);
        ctx.globalAlpha = 1;
        ctx.font = `${fs}px system-ui, sans-serif`;
    }
    for (const [m, x, y] of hpBars) hpBar(m, x, y);

    // Spawn a number whenever a dueled monster's HP drops; it flies away from the nearest bot (the attacker).
    const now = performance.now();
    let seenHp = lastHp.get(canvas);
    if (!seenHp) lastHp.set(canvas, seenHp = new Map());
    let flying = floaters.get(canvas);
    if (!flying) floaters.set(canvas, flying = []);
    for (const [m, x, y] of hpBars) {
        if (m.hp == null) continue;
        const before = seenHp.get(m.id);
        seenHp.set(m.id, m.hp);
        if (before == null || m.hp >= before) continue;
        let near = null, best = Infinity;
        for (const b of payload.bots || []) {
            const d = Math.hypot(b.x - m.x, b.y - m.y);
            if (d < best) { best = d; near = b; }
        }
        const dir = near && near.x !== m.x ? Math.sign(m.x - near.x) : (Math.random() < 0.5 ? -1 : 1);
        const hits = Math.max(1, m.hits || 1), per = Math.round((before - m.hp) / hits);
        for (let h = 0; h < hits; h++) {
            const born = now + 200 * h;
            flying.push({ path: DAMAGE_PATH, value: per, x, y: y - 6, dir, born });
            if (hits > 1) flying.push({ path: COMBO_PATH, value: per * (h + 1), id: m.id, born });
        }
    }
    const barAt = new Map(hpBars.map(([m, x, y]) => [m.id, [x, y - 6]]));
    const unit = spriteHeight * 0.5; // one Unity world unit on the minimap, relative to the ~30 px sprite
    ctx.textAlign = "center";
    ctx.textBaseline = "alphabetic";
    // A combo total that has started ends the older totals on the same monster.
    const newestCombo = new Map();
    for (const f of flying) if (f.id != null && f.born <= now && f.born > (newestCombo.get(f.id) ?? -1)) newestCombo.set(f.id, f.born);
    for (let i = flying.length - 1; i >= 0; i--) {
        const f = flying[i], P = f.path, life = (now - f.born) / 1000 / P.tween;
        if (life > 1 || (f.id != null && f.born < (newestCombo.get(f.id) ?? -1))) { flying.splice(i, 1); continue; }
        if (life < 0) continue; // a later hit of a multi-hit, not shown yet
        if (f.id != null && barAt.has(f.id)) [f.x, f.y] = barAt.get(f.id); // the total rides with the monster
        const size = evalCurve(P.size, life);
        const fx = f.x + (f.dir || 0) * P.distance * unit * life * 0.5;
        const fy = f.y - evalCurve(P.trajectory, life) * P.height * unit;
        ctx.globalAlpha = Math.max(0, Math.min(1, evalCurve(P.alpha, life)));
        ctx.font = `bold ${Math.round(9 * (window.devicePixelRatio || 1) * size)}px system-ui, sans-serif`;
        ctx.lineWidth = Math.max(2, 2.5 * size);
        ctx.strokeStyle = "#000000";
        ctx.strokeText(String(f.value), fx, fy);
        ctx.fillStyle = P.color;
        ctx.fillText(String(f.value), fx, fy);
    }
    ctx.globalAlpha = 1;
    ctx.textAlign = "start";
    ctx.font = `${fs}px system-ui, sans-serif`;

    const title = `${payload.map}  ${t.w}×${t.h} cells  ·  shown ${canvas.clientWidth}×${canvas.clientHeight}px (${(canvas.clientWidth / Math.max(1, canvas.clientHeight)).toFixed(2)}:1)  ·  ${(payload.bots || []).length} bot(s)  ·  ${(payload.mobs || []).length} mob(s) in view`;
    // Under the title, one row per bot: name plus tiny HP (green/yellow/red like the dashboard) and SP bars.
    const bots = payload.bots || [];
    const rowHeight = fs + 6;
    const barWidth = Math.round(fs * 6);
    const barHeight = Math.max(4, Math.round(fs * 0.4));
    const nameWidth = Math.max(0, ...bots.map(b => ctx.measureText(b.name || "?").width));
    const numbers = b => `${b.hp}/${b.maxHp}  ${b.sp}/${b.maxSp}`;
    const numbersWidth = Math.max(0, ...bots.map(b => ctx.measureText(numbers(b)).width));
    const boxWidth = Math.max(ctx.measureText(title).width, bots.length ? nameWidth + barWidth + numbersWidth + 20 : 0) + 10;
    ctx.textBaseline = "top";
    ctx.fillStyle = "#000000aa";
    ctx.fillRect(4, 4, boxWidth, fs + 8 + bots.length * rowHeight);
    ctx.fillStyle = COLORS.text;
    ctx.fillText(title, 9, 8);
    bots.forEach((b, i) => {
        const top = 8 + fs + 4 + i * rowHeight;
        ctx.fillStyle = COLORS.text;
        ctx.fillText(b.name || "?", 9, top);
        const barX = 9 + nameWidth + 8;
        const hpPercent = b.maxHp > 0 ? Math.min(1, b.hp / b.maxHp) : 0;
        const spPercent = b.maxSp > 0 ? Math.min(1, b.sp / b.maxSp) : 0;
        const hpColor = hpPercent <= 0.25 ? "#dc3545" : hpPercent <= 0.5 ? "#ffc107" : "#198754";
        const bar = (y, percent, color) => {
            ctx.fillStyle = "#ffffff22";
            ctx.fillRect(barX, y, barWidth, barHeight);
            ctx.fillStyle = color;
            ctx.fillRect(barX, y, barWidth * percent, barHeight);
        };
        bar(top + 1, hpPercent, hpColor);
        bar(top + 1 + barHeight + 2, spPercent, "#0dcaf0");
        ctx.fillStyle = COLORS.text;
        ctx.fillText(numbers(b), barX + barWidth + 8, top);
    });
    ctx.textBaseline = "alphabetic";
}
