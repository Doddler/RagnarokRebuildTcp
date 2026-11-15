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

    // Faint grid only when cells are large enough to be useful.
    if (cell >= 6) {
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
