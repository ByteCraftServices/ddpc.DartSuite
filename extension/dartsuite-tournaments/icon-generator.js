// DartSuite Tournaments — Dynamic Icon Generator
// Generates trophy icons with status coloring and match status overlays.
// Uses OffscreenCanvas (available in MV3 service workers).

// ─── DST Status (Tournament Status) ───
// "connected" = Green trophy: API + tournament connected
// "ready"     = Yellow trophy: API connected, no tournament selected
// "offline"   = Red trophy: API not reachable

// ─── Match Status ───
// "available"       = No overlay (idle, no match context)
// "idle"            = Heartbeat/EKG: managed mode idle (menu, lobby creation)
// "scheduled"       = Clock: a scheduled match detected at the board
// "waitForPlayer"   = Incognito: lobby started, waiting for players
// "waitForMatch"    = Hourglass: full lobby, waiting for match start
// "playing"         = Play triangle: match is running
// "listening"       = Headphones: DartSuite listener active for match data
// "disconnected"    = Red stop hand: connection to DartSuite lost
// "ended"           = Pause: match ended

const ICON_SIZES = [16, 32, 48, 128];

// Trophy SVG path (simplified trophy shape)
function drawTrophy(ctx, size, color) {
    const s = size / 128; // scale factor
    ctx.save();
    ctx.scale(s, s);

    // Trophy cup body
    ctx.beginPath();
    ctx.moveTo(32, 16);
    ctx.lineTo(96, 16);
    ctx.quadraticCurveTo(100, 16, 100, 24);
    ctx.lineTo(96, 64);
    ctx.quadraticCurveTo(64, 88, 32, 64);
    ctx.lineTo(28, 24);
    ctx.quadraticCurveTo(28, 16, 32, 16);
    ctx.closePath();
    ctx.fillStyle = color;
    ctx.fill();

    // Trophy cup highlight
    ctx.beginPath();
    ctx.moveTo(40, 22);
    ctx.lineTo(50, 22);
    ctx.lineTo(46, 52);
    ctx.lineTo(38, 48);
    ctx.closePath();
    ctx.fillStyle = "rgba(255,255,255,0.3)";
    ctx.fill();

    // Left handle
    ctx.beginPath();
    ctx.moveTo(28, 24);
    ctx.quadraticCurveTo(10, 24, 12, 44);
    ctx.quadraticCurveTo(14, 56, 30, 56);
    ctx.lineWidth = 6;
    ctx.strokeStyle = color;
    ctx.stroke();

    // Right handle
    ctx.beginPath();
    ctx.moveTo(100, 24);
    ctx.quadraticCurveTo(118, 24, 116, 44);
    ctx.quadraticCurveTo(114, 56, 98, 56);
    ctx.lineWidth = 6;
    ctx.strokeStyle = color;
    ctx.stroke();

    // Stem
    ctx.fillStyle = color;
    ctx.fillRect(56, 72, 16, 20);

    // Base
    ctx.beginPath();
    ctx.moveTo(36, 92);
    ctx.lineTo(92, 92);
    ctx.lineTo(96, 104);
    ctx.lineTo(32, 104);
    ctx.closePath();
    ctx.fill();

    // Base plate
    ctx.beginPath();
    ctx.moveTo(28, 104);
    ctx.lineTo(100, 104);
    ctx.lineTo(100, 112);
    ctx.lineTo(28, 112);
    ctx.closePath();
    ctx.fill();

    ctx.restore();
}

// Overlay icons (drawn in bottom-right quadrant)
function drawOverlay(ctx, size, matchStatus) {
    if (!matchStatus || matchStatus === "available") return;

    const overlaySize = Math.max(Math.round(size * 0.45), 8);
    const ox = size - overlaySize;
    const oy = size - overlaySize;
    const cx = ox + overlaySize / 2;
    const cy = oy + overlaySize / 2;
    const r = overlaySize / 2;

    // Background circle
    ctx.beginPath();
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.fillStyle = getOverlayBgColor(matchStatus);
    ctx.fill();
    ctx.lineWidth = Math.max(1, size / 32);
    ctx.strokeStyle = "#1a1a2e";
    ctx.stroke();

    // Icon drawing
    ctx.save();
    ctx.translate(cx, cy);
    const is = r * 0.6; // inner scale

    switch (matchStatus) {
        case "playing":
            // Play triangle
            ctx.beginPath();
            ctx.moveTo(-is * 0.6, -is);
            ctx.lineTo(is, 0);
            ctx.lineTo(-is * 0.6, is);
            ctx.closePath();
            ctx.fillStyle = "#fff";
            ctx.fill();
            break;

        case "listening":
            // Headphones
            ctx.lineWidth = Math.max(1, is * 0.3);
            ctx.strokeStyle = "#fff";
            ctx.lineCap = "round";
            // Headband
            ctx.beginPath();
            ctx.arc(0, is * 0.1, is * 0.8, Math.PI, 0);
            ctx.stroke();
            // Left ear
            ctx.fillStyle = "#fff";
            ctx.fillRect(-is * 0.95, -is * 0.1, is * 0.4, is * 0.8);
            // Right ear
            ctx.fillRect(is * 0.55, -is * 0.1, is * 0.4, is * 0.8);
            break;

        case "disconnected":
            // Stop hand / X
            ctx.lineWidth = Math.max(1.5, is * 0.35);
            ctx.strokeStyle = "#fff";
            ctx.lineCap = "round";
            ctx.beginPath();
            ctx.moveTo(-is * 0.6, -is * 0.6);
            ctx.lineTo(is * 0.6, is * 0.6);
            ctx.stroke();
            ctx.beginPath();
            ctx.moveTo(is * 0.6, -is * 0.6);
            ctx.lineTo(-is * 0.6, is * 0.6);
            ctx.stroke();
            break;

        case "idle":
            // Heartbeat EKG line
            ctx.lineWidth = Math.max(1, is * 0.25);
            ctx.strokeStyle = "#fff";
            ctx.lineCap = "round";
            ctx.lineJoin = "round";
            ctx.beginPath();
            ctx.moveTo(-is, 0);
            ctx.lineTo(-is * 0.4, 0);
            ctx.lineTo(-is * 0.2, -is * 0.7);
            ctx.lineTo(is * 0.1, is * 0.5);
            ctx.lineTo(is * 0.3, -is * 0.3);
            ctx.lineTo(is * 0.5, 0);
            ctx.lineTo(is, 0);
            ctx.stroke();
            break;

        case "scheduled":
            // Clock
            ctx.lineWidth = Math.max(1, is * 0.2);
            ctx.strokeStyle = "#fff";
            ctx.beginPath();
            ctx.arc(0, 0, is * 0.75, 0, Math.PI * 2);
            ctx.stroke();
            // Clock hands
            ctx.beginPath();
            ctx.moveTo(0, 0);
            ctx.lineTo(0, -is * 0.5);
            ctx.stroke();
            ctx.beginPath();
            ctx.moveTo(0, 0);
            ctx.lineTo(is * 0.35, 0);
            ctx.stroke();
            break;

        case "waitForPlayer":
            // Incognito / person silhouette with ?
            ctx.fillStyle = "#fff";
            ctx.font = `bold ${Math.round(is * 1.4)}px sans-serif`;
            ctx.textAlign = "center";
            ctx.textBaseline = "middle";
            ctx.fillText("?", 0, 0);
            break;

        case "waitForMatch":
            // Hourglass
            ctx.lineWidth = Math.max(1, is * 0.2);
            ctx.strokeStyle = "#fff";
            ctx.fillStyle = "#fff";
            // Top triangle
            ctx.beginPath();
            ctx.moveTo(-is * 0.5, -is * 0.8);
            ctx.lineTo(is * 0.5, -is * 0.8);
            ctx.lineTo(0, 0);
            ctx.closePath();
            ctx.stroke();
            // Bottom triangle
            ctx.beginPath();
            ctx.moveTo(-is * 0.5, is * 0.8);
            ctx.lineTo(is * 0.5, is * 0.8);
            ctx.lineTo(0, 0);
            ctx.closePath();
            ctx.stroke();
            // Sand in bottom
            ctx.beginPath();
            ctx.moveTo(-is * 0.25, is * 0.8);
            ctx.lineTo(is * 0.25, is * 0.8);
            ctx.lineTo(0, is * 0.3);
            ctx.closePath();
            ctx.fill();
            break;

        case "ended":
            // Pause bars
            ctx.fillStyle = "#fff";
            const bw = is * 0.3;
            const bh = is * 1.2;
            ctx.fillRect(-is * 0.5, -bh / 2, bw, bh);
            ctx.fillRect(is * 0.2, -bh / 2, bw, bh);
            break;
    }

    ctx.restore();
}

function getOverlayBgColor(matchStatus) {
    switch (matchStatus) {
        case "playing": return "#4caf50";
        case "listening": return "#2196f3";
        case "disconnected": return "#f44336";
        case "idle": return "#4caf50";
        case "scheduled": return "#ff9800";
        case "waitForPlayer": return "#9c27b0";
        case "waitForMatch": return "#ff9800";
        case "ended": return "#607d8b";
        default: return "#888";
    }
}

function getTrophyColor(dstStatus) {
    switch (dstStatus) {
        case "connected": return "#4caf50"; // green
        case "ready": return "#ff9800";     // yellow/amber
        case "offline": return "#f44336";   // red
        default: return "#888";
    }
}

/**
 * Generate icon ImageData for all required sizes.
 * @param {string} dstStatus - "connected" | "ready" | "offline"
 * @param {string} matchStatus - "available" | "playing" | "listening" | ... | null
 * @returns {Object} { 16: ImageData, 32: ImageData, 48: ImageData, 128: ImageData }
 */
function generateIconImageData(dstStatus, matchStatus) {
    const result = {};
    const color = getTrophyColor(dstStatus);

    for (const size of ICON_SIZES) {
        const canvas = new OffscreenCanvas(size, size);
        const ctx = canvas.getContext("2d");
        ctx.clearRect(0, 0, size, size);
        drawTrophy(ctx, size, color);
        drawOverlay(ctx, size, matchStatus);
        result[size] = ctx.getImageData(0, 0, size, size);
    }

    return result;
}

// Export for use in background.js
// (In MV3 service workers, we use importScripts or just include in the same file scope)
if (typeof globalThis !== "undefined") {
    globalThis.generateIconImageData = generateIconImageData;
    globalThis.ICON_SIZES = ICON_SIZES;
}
