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
    // Keep the backing store square and crisp on HiDPI without depending on CSS layout size.
    const cssSize = canvas.clientWidth || canvas.width || 420;
    const dpr = window.devicePixelRatio || 1;
    const px = Math.round(cssSize * dpr);
    if (canvas.width !== px || canvas.height !== px) {
        canvas.width = px;
        canvas.height = px;
    }
    return { px, dpr, css: cssSize };
}

function message(ctx, px, text) {
    ctx.fillStyle = COLORS.bg;
    ctx.fillRect(0, 0, px, px);
    ctx.fillStyle = COLORS.textDim;
    ctx.font = `${Math.max(12, Math.round(px * 0.04))}px system-ui, sans-serif`;
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(text || "no data", px / 2, px / 2);
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

export function drawWorld(canvas, payload) {
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;
    const { px } = fitCanvas(canvas);

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

    // Fit the (possibly non-square) map inside the square canvas.
    const scale = px / Math.max(t.w, t.h);
    const ox = (px - t.w * scale) / 2, oy = (px - t.h * scale) / 2;
    ctx.fillStyle = COLORS.bg;
    ctx.fillRect(0, 0, px, px);
    const art = minimap(payload.minimap);
    ctx.imageSmoothingEnabled = !!art;
    ctx.drawImage(art || t.img, ox, oy, t.w * scale, t.h * scale);

    // Fixed monster spawn zones are inclusive world-cell rectangles. Draw them lightly so the map stays
    // readable, and retain their bounds for hover names/counts.
    const spawnHits = [];
    if (payload.showSpawnAreas !== false) {
        for (const area of t.spawns || []) {
            const left = ox + area.minX * scale;
            const top = oy + (t.h - 1 - area.maxY) * scale;
            const width = (area.maxX - area.minX + 1) * scale;
            const height = (area.maxY - area.minY + 1) * scale;
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
    // Monsters as their first sprite frame, feet on their cell, ~22 css px tall; nearer the bottom drawn on top.
    // A red dot until the atlas loads, or when the monster has no sprite.
    const mr = Math.max(3.5, r * 0.8);
    const spriteHeight = 22 * (window.devicePixelRatio || 1);
    const hits = [...spawnHits]; // rectangles and point positions for hover tooltips
    const mobs = [...(payload.mobs || [])].sort((a, b) => b.y - a.y);
    for (const m of mobs) {
        const mx = ox + (m.x + 0.5) * scale, my = oy + (t.h - 1 - m.y + 0.5) * scale;
        const s = payload.sprites && payload.sprites[m.name];
        const atlas = s ? minimap(s.atlas) : null;
        if (atlas) {
            const k = Math.min(1, spriteHeight / Math.max(s.w, s.h));
            const dw = s.w * k, dh = s.h * k;
            ctx.imageSmoothingEnabled = true;
            ctx.drawImage(atlas, s.x, s.y, s.w, s.h, mx - dw / 2, my - dh + mr, dw, dh);
            hits.push({ x: mx, y: my - dh / 2 + mr, name: m.name || "monster" });
        } else {
            ctx.fillStyle = COLORS.monster;
            ctx.strokeStyle = "#000000cc";
            ctx.lineWidth = 1.5;
            ctx.beginPath();
            ctx.arc(mx, my, mr, 0, Math.PI * 2);
            ctx.fill();
            ctx.stroke();
            hits.push({ x: mx, y: my, name: m.name || "monster" });
        }
    }
    for (const b of payload.bots || []) {
        hits.push({ x: ox + (b.x + 0.5) * scale, y: oy + (t.h - 1 - b.y + 0.5) * scale, name: b.name || "bot" });
        if (b.target) hits.push({ x: ox + (b.target.x + 0.5) * scale, y: oy + (t.h - 1 - b.target.y + 0.5) * scale, name: `${b.name || "bot"} A* target` });
    }
    hoverTargets.set(canvas, hits);
    attachHover(canvas);
    // Planned walk of traveling bots: dashed line from the bot along its A* path, ring on the goal cell.
    const toX = x => ox + (x + 0.5) * scale, toY = y => oy + (t.h - 1 - y + 0.5) * scale;
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
        const cx = ox + (b.x + 0.5) * scale;
        const cy = oy + (t.h - 1 - b.y + 0.5) * scale;
        ctx.fillStyle = COLORS.self;
        ctx.beginPath();
        ctx.arc(cx, cy, r, 0, Math.PI * 2);
        ctx.fill();
        ctx.strokeStyle = "#ffffffaa";
        ctx.lineWidth = 1.5;
        ctx.stroke();

        const label = b.name || "?";
        const lw = ctx.measureText(label).width;
        ctx.fillStyle = "#000000aa";
        ctx.fillRect(cx + r + 3, cy - fs / 2 - 3, lw + 8, fs + 6);
        ctx.fillStyle = COLORS.text;
        ctx.fillText(label, cx + r + 7, cy);
    }

    const title = `${payload.map}  ${t.w}×${t.h}  ·  ${(payload.bots || []).length} bot(s)  ·  ${(payload.mobs || []).length} mob(s) in view`;
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
