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

// In-game minimap art by URL: undefined while loading, null if missing (then we draw the walk grid).
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

export function draw(canvas, payload) {
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    const { px } = fitCanvas(canvas);

    if (!payload || !payload.ok) {
        message(ctx, px, payload && payload.message ? payload.message : "offline");
        return;
    }

    const size = payload.size | 0;
    if (size <= 0 || !payload.cells) {
        message(ctx, px, payload.message || "no map data");
        return;
    }

    const cell = px / size;
    const originX = payload.origin ? payload.origin.x : 0;
    const originY = payload.origin ? payload.origin.y : 0;

    // Background (also covers any sub-pixel gaps between cells).
    ctx.fillStyle = COLORS.bg;
    ctx.fillRect(0, 0, px, px);

    const art = minimap(payload.minimap);
    if (art) {
        // Image row 0 is the map's top (highest world y), so the window's top edge is row h - originY - size.
        ctx.drawImage(art, originX, art.height - originY - size, size, size, 0, 0, px, px);
    } else {
        // Walkability window. cells is row-major from origin (local y 0 = bottom row in world space).
        const cells = payload.cells;
        for (let ly = 0; ly < size; ly++) {
            const cy = (size - 1 - ly) * cell; // flip Y
            const rowBase = ly * size;
            for (let lx = 0; lx < size; lx++) {
                ctx.fillStyle = cells[rowBase + lx] ? COLORS.walkable : COLORS.blocked;
                ctx.fillRect(lx * cell, cy, cell + 0.6, cell + 0.6);
            }
        }
    }

    // Faint grid only when cells are large enough to be useful.
    if (!art && cell >= 6) {
        ctx.strokeStyle = COLORS.grid;
        ctx.lineWidth = 1;
        ctx.beginPath();
        for (let i = 0; i <= size; i++) {
            const p = Math.round(i * cell) + 0.5;
            ctx.moveTo(p, 0); ctx.lineTo(p, px);
            ctx.moveTo(0, p); ctx.lineTo(px, p);
        }
        ctx.stroke();
    }

    // Convert a world coord to a canvas pixel center inside the window.
    const toPx = (wx, wy) => ({
        cx: (wx - originX + 0.5) * cell,
        cy: (size - 1 - (wy - originY) + 0.5) * cell,
    });

    const dotR = Math.max(2.5, cell * 0.42);
    const entities = payload.entities || [];

    // Draw non-self/non-target first so the player and target sit on top.
    for (const e of entities) {
        if (e.kind === "self" || e.kind === "target") continue;
        const { cx, cy } = toPx(e.x, e.y);
        ctx.fillStyle = e.kind === "player" ? COLORS.player
            : e.kind === "npc" ? COLORS.npc
            : COLORS.monster;
        ctx.beginPath();
        ctx.arc(cx, cy, dotR, 0, Math.PI * 2);
        ctx.fill();
    }

    // Target: its base dot plus a bright ring.
    for (const e of entities) {
        if (e.kind !== "target") continue;
        const { cx, cy } = toPx(e.x, e.y);
        ctx.fillStyle = COLORS.monster;
        ctx.beginPath();
        ctx.arc(cx, cy, dotR, 0, Math.PI * 2);
        ctx.fill();
        ctx.strokeStyle = COLORS.targetRing;
        ctx.lineWidth = Math.max(2, cell * 0.18);
        ctx.beginPath();
        ctx.arc(cx, cy, dotR + Math.max(3, cell * 0.45), 0, Math.PI * 2);
        ctx.stroke();
    }

    // Self last, on top of everything.
    if (payload.self) {
        const { cx, cy } = toPx(payload.self.x, payload.self.y);
        ctx.fillStyle = COLORS.self;
        ctx.beginPath();
        ctx.arc(cx, cy, Math.max(3, dotR * 1.15), 0, Math.PI * 2);
        ctx.fill();
        ctx.strokeStyle = "#ffffffaa";
        ctx.lineWidth = 1.5;
        ctx.stroke();
    }

    // Overlay: map + coords (top-left) and a compact legend (bottom-left).
    const fs = Math.max(11, Math.round(px * 0.032));
    ctx.font = `${fs}px system-ui, sans-serif`;
    ctx.textBaseline = "top";
    const label = payload.self
        ? `${payload.map || "?"}  (${payload.self.x}, ${payload.self.y})`
        : (payload.map || "?");
    ctx.fillStyle = "#000000aa";
    const lw = ctx.measureText(label).width;
    ctx.fillRect(4, 4, lw + 10, fs + 8);
    ctx.fillStyle = COLORS.text;
    ctx.fillText(label, 9, 8);

    drawLegend(ctx, px, fs);
    ctx.textBaseline = "alphabetic";
}

// Whole-map overview: every bot on one map. The terrain arrives once per map as a "0101..." string
// (row-major, world y=0 = bottom row) and is cached as a 1px-per-cell image; later ticks only send bots.
const worldTerrain = new WeakMap(); // canvas -> { map, img, w, h }

function rgb(hex) {
    return [1, 3, 5].map(i => parseInt(hex.slice(i, i + 2), 16));
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
        worldTerrain.set(canvas, { map: payload.map, img, w, h });
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

    const fs = Math.max(11, Math.round(px * 0.022));
    const r = Math.max(4, scale * 1.6);
    ctx.font = `${fs}px system-ui, sans-serif`;
    ctx.textBaseline = "middle";
    // Monsters (only those inside some bot's view) under the bots, smaller and with a dark ring.
    const mr = Math.max(3.5, r * 0.8);
    ctx.fillStyle = COLORS.monster;
    ctx.strokeStyle = "#000000aa";
    ctx.lineWidth = 1;
    for (const m of payload.mobs || []) {
        ctx.beginPath();
        ctx.arc(ox + (m.x + 0.5) * scale, oy + (t.h - 1 - m.y + 0.5) * scale, mr, 0, Math.PI * 2);
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
    ctx.textBaseline = "top";
    ctx.fillStyle = "#000000aa";
    ctx.fillRect(4, 4, ctx.measureText(title).width + 10, fs + 8);
    ctx.fillStyle = COLORS.text;
    ctx.fillText(title, 9, 8);
    ctx.textBaseline = "alphabetic";
}

function drawLegend(ctx, px, fs) {
    const items = [
        ["self", COLORS.self],
        ["target", COLORS.targetRing],
        ["mob", COLORS.monster],
        ["npc", COLORS.npc],
        ["player", COLORS.player],
    ];
    const pad = 6;
    const lh = fs + 4;
    const boxH = items.length * lh + pad * 2;
    const boxW = 78;
    const x = 4;
    const y = px - boxH - 4;

    ctx.fillStyle = "#000000aa";
    ctx.fillRect(x, y, boxW, boxH);
    ctx.font = `${Math.max(10, fs - 1)}px system-ui, sans-serif`;
    ctx.textBaseline = "middle";
    for (let i = 0; i < items.length; i++) {
        const cy = y + pad + i * lh + lh / 2;
        ctx.fillStyle = items[i][1];
        ctx.beginPath();
        ctx.arc(x + pad + 5, cy, 4, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = COLORS.text;
        ctx.fillText(items[i][0], x + pad + 16, cy);
    }
    ctx.textBaseline = "top";
}
