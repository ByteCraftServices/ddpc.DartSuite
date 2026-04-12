// DartSuite Tournaments — Background Service Worker v0.4.0
// Icon badge management, SignalR WebSocket relay, proxy fetch.
// Dynamic trophy icon with DST status + match status overlays.

importScripts("icon-generator.js");

const DEFAULT_API_BASE_URL = "http://localhost:5290";

// ─── Status Model ───
// DST Status (Tournament Status): "connected" | "ready" | "offline"
// Match Status: "available" | "idle" | "scheduled" | "waitForPlayer" | "waitForMatch" | "playing" | "listening" | "disconnected" | "ended"

let dstStatus = "offline";   // current DST status
let matchStatus = "available"; // current match status
let dartsuiteEnabled = true;

function sanitizeOptionsForLog(options) {
    if (!options || typeof options !== "object") return null;
    const safe = { ...options };

    if (safe.headers && typeof safe.headers === "object") {
        const headers = { ...safe.headers };
        if (headers.Authorization || headers.authorization) {
            headers.Authorization = "<redacted>";
            headers.authorization = "<redacted>";
        }
        safe.headers = headers;
    }

    if (typeof safe.body === "string") {
        try {
            const parsed = JSON.parse(safe.body);
            if (parsed && typeof parsed === "object" && typeof parsed.accessToken === "string") {
                parsed.accessToken = `${parsed.accessToken.slice(0, 10)}...`;
            }
            safe.body = parsed;
        } catch {
            // Keep non-JSON payload as text.
        }
    }

    return safe;
}

function logTraffic(direction, message, details) {
    const prefix = `[DartSuite] [${direction}]: ${message}`;
    if (details !== undefined) {
        console.log(prefix, details);
    } else {
        console.log(prefix);
    }

    broadcastTrafficLog(direction, message, details).catch(() => { });
}

async function broadcastTrafficLog(direction, message, details) {
    const tabs = await chrome.tabs.query({ url: "https://play.autodarts.io/*" });
    for (const tab of tabs) {
        chrome.tabs.sendMessage(tab.id, {
            action: "debugTrafficLog",
            direction,
            message,
            details
        }).catch(() => { });
    }
}

async function loggedFetch(url, options) {
    const method = (options?.method || "GET").toUpperCase();
    logTraffic("OUT", `${method} ${url}`, sanitizeOptionsForLog(options));

    try {
        const response = await fetch(url, options || {});
        logTraffic("IN", `${method} ${url} -> ${response.status}`);
        return response;
    } catch (error) {
        logTraffic("IN", `${method} ${url} -> ERROR`, { error: error?.message || String(error) });
        throw error;
    }
}

async function refreshExtensionEnabledState() {
    try {
        const stored = await chrome.storage.sync.get({ dartsuiteEnabled: true });
        dartsuiteEnabled = stored.dartsuiteEnabled !== false;
    } catch {
        dartsuiteEnabled = true;
    }

    if (!dartsuiteEnabled) {
        chrome.alarms.clear(POLL_ALARM_NAME);
        chrome.alarms.clear(STATUS_ALARM_NAME);
        setDstStatus("offline");
        setMatchStatus("available");
        return;
    }

    await scheduleStatusPolling();
    await resumeBoardPollingIfNeeded();
}

async function resumeBoardPollingIfNeeded() {
    const [{ pollingBoardId }, managed] = await Promise.all([
        chrome.storage.local.get("pollingBoardId"),
        chrome.storage.sync.get({ managedBoardId: null, managedTournamentId: null })
    ]);

    const boardId = managed.managedBoardId || null;
    const tournamentId = managed.managedTournamentId || null;
    if (!boardId || !tournamentId) {
        chrome.alarms.clear(POLL_ALARM_NAME);
        await chrome.storage.local.remove("pollingBoardId");
        await setLastHandledCurrentMatchId(null);
        return;
    }

    if (pollingBoardId !== boardId) {
        await chrome.storage.local.set({ pollingBoardId: boardId });
        await setLastHandledCurrentMatchId(null);
    }

    chrome.alarms.create(POLL_ALARM_NAME, { periodInMinutes: 10 / 60 });
    await doPollCycle(boardId);
}

chrome.runtime.onInstalled.addListener(() => {
    console.log("DartSuite Tournaments extension installed v0.4.0");
    chrome.storage.sync.get("dartsuiteEnabled").then(stored => {
        if (typeof stored.dartsuiteEnabled !== "boolean") {
            chrome.storage.sync.set({ dartsuiteEnabled: true });
        }
    }).catch(() => { });

    updateIcon();
    refreshExtensionEnabledState();
});

chrome.runtime.onStartup.addListener(() => {
    refreshExtensionEnabledState();
});

chrome.storage.onChanged.addListener((changes, area) => {
    if (area !== "sync") return;
    if (changes.dartsuiteEnabled) {
        refreshExtensionEnabledState().catch(() => { });
        return;
    }

    if (changes.managedBoardId || changes.managedTournamentId) {
        if (!dartsuiteEnabled) return;
        resumeBoardPollingIfNeeded().catch(() => { });
    }
});

// When a play.autodarts.io tab finishes loading, notify content script
chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
    if (changeInfo.status !== "complete") return;
    if (!tab?.url?.startsWith("https://play.autodarts.io/")) return;
    chrome.tabs.sendMessage(tabId, { action: "pageLoaded", url: tab.url }).catch(() => { });
});

// ─── Message Handler ───

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (!message?.action) return;

    const allowedWhileDisabled = new Set(["getStatus", "getApiBaseUrl", "updateStatusPolling"]);
    if (!dartsuiteEnabled && !allowedWhileDisabled.has(message.action)) {
        sendResponse({ ok: false, disabled: true });
        return;
    }

    switch (message.action) {
        case "reportUrl":
            reportPageToApi(message.url).then(sendResponse).catch(() => sendResponse({ ok: false }));
            return true;

        case "getApiBaseUrl":
            getApiBaseUrl().then(url => sendResponse({ apiBaseUrl: url }));
            return true;

        case "proxyFetch":
            proxyFetch(message.url, message.options)
                .then(sendResponse)
                .catch(err => sendResponse({ ok: false, error: err?.message }));
            return true;

        case "setDstStatus":
            setDstStatus(message.status);
            sendResponse({ ok: true });
            break;

        case "setMatchStatus":
            setMatchStatus(message.status);
            sendResponse({ ok: true });
            break;

        // Legacy: map old setIconStatus to new DST status
        case "setIconStatus":
            {
                const map = {
                    "connected": "connected",
                    "active": "connected",
                    "configured": "ready",
                    "warning": "ready",
                    "error": "offline",
                    "default": "offline"
                };
                setDstStatus(map[message.status] || "offline");
            }
            sendResponse({ ok: true });
            break;

        case "tournamentSelected":
            handleTournamentSelected(message.tournament);
            sendResponse({ ok: true });
            break;

        case "tournamentContextChanged":
            broadcastTournamentContext(message.payload || null);
            sendResponse({ ok: true });
            break;

        case "managedModeChanged":
            handleManagedModeChanged(message);
            sendResponse({ ok: true });
            break;

        case "requestNextMatch":
            handleRequestNextMatch(message);
            sendResponse({ ok: true });
            break;

        case "getStatus":
            sendResponse({ dstStatus: dartsuiteEnabled ? dstStatus : "offline", matchStatus, enabled: dartsuiteEnabled });
            break;

        case "updateStatusPolling":
            scheduleStatusPolling();
            sendResponse({ ok: true });
            break;

        default:
            break;
    }
});

// ─── Dynamic Icon Management ───

function setDstStatus(newStatus) {
    if (dstStatus === newStatus) return;
    dstStatus = newStatus;
    updateIcon();
}

function setMatchStatus(newStatus) {
    if (matchStatus === newStatus) return;
    matchStatus = newStatus;
    updateIcon();
    if (newStatus === "waitForPlayer" || newStatus === "waitForMatch") {
        autoSyncLobbyStatusAsync(newStatus).catch(() => { });
    }
}

async function autoSyncLobbyStatusAsync(status) {
    try {
        const [apiBaseUrl, local, sync] = await Promise.all([
            getApiBaseUrl(),
            chrome.storage.local.get("pollingBoardId"),
            chrome.storage.sync.get({ managedTournamentId: null })
        ]);
        const boardId = local.pollingBoardId;
        const tournamentId = sync.managedTournamentId;
        if (!boardId) return;

        const tabs = await chrome.tabs.query({ url: "https://play.autodarts.io/*" });
        const pageState = await fetchFirstPageState(tabs);
        const sourceUrl = pageState?.url || null;

        await loggedFetch(`${apiBaseUrl}/api/boards/${boardId}/extension-sync/report`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                requestId: null,
                tournamentId: tournamentId || null,
                sourceUrl,
                externalMatchId: null,
                player1: null,
                player2: null,
                matchStatus: status
            })
        });
        logTraffic("OUT", `autoSyncLobbyStatus board=${boardId} status=${status}`);
    } catch { /* silent */ }
}

function updateIcon() {
    try {
        const imageData = generateIconImageData(dstStatus, matchStatus);
        chrome.action.setIcon({ imageData });

        // Also set tooltip
        const dstLabel = { connected: "Verbunden", ready: "Bereit", offline: "Offline" }[dstStatus] || "Unbekannt";
        const matchLabel = {
            available: "", idle: "Idle", scheduled: "Geplant", waitForPlayer: "Warte auf Spieler",
            waitForMatch: "Warte auf Match", playing: "Spiel läuft", listening: "Listener aktiv",
            disconnected: "Getrennt", ended: "Beendet"
        }[matchStatus] || "";
        const title = `DartSuite Tournaments — ${dstLabel}${matchLabel ? ` | ${matchLabel}` : ""}`;
        chrome.action.setTitle({ title });
        broadcastStatus();
    } catch (err) {
        console.warn("DartSuite BG: Icon update failed, using fallback badge", err);
        // Fallback to badge text
        const badges = {
            connected: { text: "✓", color: "#4caf50" },
            ready: { text: "?", color: "#ff9800" },
            offline: { text: "!", color: "#f44336" }
        };
        const badge = badges[dstStatus] || { text: "", color: "#888" };
        chrome.action.setBadgeText({ text: badge.text });
        chrome.action.setBadgeBackgroundColor({ color: badge.color });
        broadcastStatus();
    }
}

async function broadcastStatus() {
    const tabs = await chrome.tabs.query({ url: "https://play.autodarts.io/*" });
    for (const tab of tabs) {
        chrome.tabs.sendMessage(tab.id, {
            action: "dstStatusUpdate",
            dstStatus,
            matchStatus
        }).catch(() => { });
    }
}

// ─── Periodic Status Polling ───

const STATUS_ALARM_NAME = "dsStatusCheck";

async function scheduleStatusPolling() {
    if (!dartsuiteEnabled) {
        chrome.alarms.clear(STATUS_ALARM_NAME);
        return;
    }

    const { statusPollSeconds = 30 } = await chrome.storage.sync.get({ statusPollSeconds: 30 });
    const seconds = Math.max(10, Number(statusPollSeconds) || 30);
    const minutes = Math.max(seconds / 60, 0.5);
    chrome.alarms.create(STATUS_ALARM_NAME, { periodInMinutes: minutes });
    await checkApiHealth();
}

chrome.alarms.onAlarm.addListener(async (alarm) => {
    if (alarm.name === STATUS_ALARM_NAME) {
        if (!dartsuiteEnabled) return;
        await checkApiHealth();
    }
});

async function checkApiHealth() {
    if (!dartsuiteEnabled) {
        setDstStatus("offline");
        return;
    }

    const apiBaseUrl = await getApiBaseUrl();
    try {
        const response = await loggedFetch(`${apiBaseUrl}/api/boards`, { signal: AbortSignal.timeout(3000) });
        if (!response.ok) {
            await chrome.storage.local.set({
                apiLastError: `HTTP ${response.status}`,
                apiLastErrorUtc: new Date().toISOString()
            });
            setDstStatus("offline");
            return;
        }

        const boards = await response.json();

        const { tournamentId } = await chrome.storage.sync.get({ tournamentId: "" });
        await chrome.storage.local.remove(["apiLastError", "apiLastErrorUtc"]);
        setDstStatus(tournamentId ? "connected" : "ready");

        // Safety net: if managed mode is configured but polling was lost,
        // restore polling without requiring popup interaction.
        await resumeBoardPollingIfNeeded();

        // Allow manual board sync even without managed polling.
        const tabs = await chrome.tabs.query({ url: "https://play.autodarts.io/*" });
        await tryManualSyncSweep(apiBaseUrl, boards, tabs, null, { force: true });
    } catch {
        await chrome.storage.local.set({
            apiLastError: "API nicht erreichbar",
            apiLastErrorUtc: new Date().toISOString()
        });
        setDstStatus("offline");
    }
}

// ─── Tournament Events ───

function handleTournamentSelected(tournament) {
    if (!dartsuiteEnabled) return;

    if (!tournament) {
        setDstStatus("ready");
        broadcastTournamentContext(null);
        return;
    }

    // Check if tournament is currently running
    const today = new Date().toISOString().split("T")[0];
    const isActive = tournament.startDate <= today && tournament.endDate >= today;
    setDstStatus("connected");
    broadcastTournamentContext({
        tournamentId: tournament.id,
        tournamentName: tournament.name || "",
        host: tournament.organizerAccount || ""
    });

    console.log("DartSuite BG: Tournament selected", tournament.name, tournament.joinCode);
}

async function broadcastTournamentContext(payload) {
    const tabs = await chrome.tabs.query({ url: "https://play.autodarts.io/*" });
    for (const tab of tabs) {
        chrome.tabs.sendMessage(tab.id, {
            action: "tournamentContextChanged",
            payload
        }).catch(() => { });
    }
}

async function handleManagedModeChanged(message) {
    if (!dartsuiteEnabled) return;

    const { boardId, mode, tournamentId, tournamentName, host, boardName } = message;

    if (mode === "Auto") {
        setDstStatus("connected");
        setMatchStatus("idle");
        // Notify all play.autodarts.io tabs about managed mode
        const tabs = await chrome.tabs.query({ url: "https://play.autodarts.io/*" });
        for (const tab of tabs) {
            chrome.tabs.sendMessage(tab.id, {
                action: "setManagedMode",
                payload: { mode: "Auto", boardId, tournamentId, tournamentName: tournamentName || "", host: host || "", boardName: boardName || "" }
            }).catch(() => { });
        }

        // Set managed mode on the API
        const apiBaseUrl = await getApiBaseUrl();
        try {
            await loggedFetch(`${apiBaseUrl}/api/boards/${boardId}/managed?mode=Auto&tournamentId=${tournamentId}`, {
                method: "PATCH"
            });
        } catch { /* silent */ }

        // Start polling for this board
        await startSignalRConnection(boardId);
    } else {
        setDstStatus("ready");
        setMatchStatus("available");
        const tabs = await chrome.tabs.query({ url: "https://play.autodarts.io/*" });
        for (const tab of tabs) {
            chrome.tabs.sendMessage(tab.id, {
                action: "setManagedMode",
                payload: { mode: "Manual", boardId }
            }).catch(() => { });
        }

        // Clear managed mode on API
        const apiBaseUrl = await getApiBaseUrl();
        try {
            await loggedFetch(`${apiBaseUrl}/api/boards/${boardId}/managed?mode=Manual`, { method: "PATCH" });
        } catch { /* silent */ }

        await stopSignalRConnection();
    }
}

// ─── Board Polling (reliable via chrome.alarms) ───
// Service workers can be terminated at any time by Chrome, killing setInterval.
// chrome.alarms survives service worker restarts and wakes it back up.

const POLL_ALARM_NAME = "dsPolling";
const LAST_HANDLED_MATCH_ID_STORAGE_KEY = "lastHandledCurrentMatchId";
let lastHandledCurrentMatchId = null;
let hasLoadedLastHandledCurrentMatchId = false;
let lastSyncedBoardStatuses = new Map(); // externalBoardId -> status
let lastManualSyncSweepMs = 0;
let lastApiSessionSyncMs = 0;
let lastApiSessionToken = null;

const MANUAL_SYNC_SWEEP_THROTTLE_MS = 2500;
const API_SESSION_SYNC_INTERVAL_MS = 60 * 1000;

async function ensureLastHandledCurrentMatchIdLoaded() {
    if (hasLoadedLastHandledCurrentMatchId) return;

    const stored = await chrome.storage.local.get({ [LAST_HANDLED_MATCH_ID_STORAGE_KEY]: null });
    const storedMatchId = stored?.[LAST_HANDLED_MATCH_ID_STORAGE_KEY];
    lastHandledCurrentMatchId = typeof storedMatchId === "string" && storedMatchId.trim()
        ? storedMatchId
        : null;
    hasLoadedLastHandledCurrentMatchId = true;
}

async function setLastHandledCurrentMatchId(matchId) {
    const normalized = typeof matchId === "string" && matchId.trim()
        ? matchId
        : null;

    if (hasLoadedLastHandledCurrentMatchId && lastHandledCurrentMatchId === normalized) {
        return;
    }

    lastHandledCurrentMatchId = normalized;
    hasLoadedLastHandledCurrentMatchId = true;

    if (normalized) {
        await chrome.storage.local.set({ [LAST_HANDLED_MATCH_ID_STORAGE_KEY]: normalized });
    } else {
        await chrome.storage.local.remove(LAST_HANDLED_MATCH_ID_STORAGE_KEY);
    }
}

async function startSignalRConnection(boardId) {
    if (!dartsuiteEnabled) return;

    const { pollingBoardId } = await chrome.storage.local.get({ pollingBoardId: null });
    const alreadyPollingSameBoard = pollingBoardId === boardId;
    if (alreadyPollingSameBoard) {
        // Keep current handled command state so a duplicate start does not re-dispatch.
        chrome.alarms.create(POLL_ALARM_NAME, { periodInMinutes: 10 / 60 });
        await doPollCycle(boardId);
        return;
    }

    await setLastHandledCurrentMatchId(null);
    lastSyncedBoardStatuses.clear();

    // Persist polling state so it survives service worker restarts
    await chrome.storage.local.set({ pollingBoardId: boardId });

    console.log("DartSuite BG: Starting board command polling for", boardId);

    // Create alarm that fires every 10 seconds (Chrome enforces min ~30s for published extensions)
    chrome.alarms.create(POLL_ALARM_NAME, { periodInMinutes: 10 / 60 });

    // Run one poll immediately
    await doPollCycle(boardId);
}

// Alarm handler — Chrome wakes the service worker to run this
chrome.alarms.onAlarm.addListener(async (alarm) => {
    if (alarm.name !== POLL_ALARM_NAME) return;
    if (!dartsuiteEnabled) return;

    const [{ pollingBoardId }, { managedBoardId }] = await Promise.all([
        chrome.storage.local.get("pollingBoardId"),
        chrome.storage.sync.get({ managedBoardId: null })
    ]);

    const boardId = managedBoardId || pollingBoardId;
    if (!boardId) {
        chrome.alarms.clear(POLL_ALARM_NAME);
        return;
    }

    if (managedBoardId && pollingBoardId !== managedBoardId) {
        await chrome.storage.local.set({ pollingBoardId: managedBoardId });
    }

    await doPollCycle(boardId);
});

async function doPollCycle(boardId) {
    if (!dartsuiteEnabled) return;

    await ensureLastHandledCurrentMatchIdLoaded();

    try {
        const apiBaseUrl = await getApiBaseUrl();
        const response = await loggedFetch(`${apiBaseUrl}/api/boards`);
        if (!response.ok) {
            setDstStatus("offline");
            return;
        }
        // API reachable — ensure DST status reflects connected
        if (dstStatus === "offline") setDstStatus("ready");
        const boards = await response.json();
        const board = boards.find(b => b.id === boardId);

        // Broadcast board status to content script
        const tabs = await chrome.tabs.query({ url: "https://play.autodarts.io/*" });
        await ensureApiAutodartsSessionFromTabs(apiBaseUrl, tabs);
        if (board) {
            for (const tab of tabs) {
                chrome.tabs.sendMessage(tab.id, {
                    action: "boardStatusUpdate",
                    externalBoardId: board.externalBoardId,
                    currentMatchId: board.currentMatchId
                }).catch(() => { });
            }
            // Send heartbeat so DartSuite knows extension is connected
            try {
                await loggedFetch(`${apiBaseUrl}/api/boards/${boardId}/heartbeat`, { method: "PATCH" });
            } catch { /* silent */ }
        }

        // Sync Autodarts board status to DartSuite
        await syncBoardStatuses(apiBaseUrl, boards, tabs);

        // Process pending manual sync requests across all boards.
        await tryManualSyncSweep(apiBaseUrl, boards, tabs, boardId);

        // Update match status based on board state
        if (board) {
            await updateMatchStatusFromBoard(apiBaseUrl, board, tabs);
        }

        // Live match sync is handled server-side by AutodartsMatchListenerService.
        // A board current-match change from DartSuite is treated as explicit start command.
        if (board?.currentMatchId && board.currentMatchId !== lastHandledCurrentMatchId && board.tournamentId) {
            // Guard: never interrupt a live match — check both in-memory status and live tab URL.
            // Tab URL is ground truth: matchStatus resets on service worker restart (MV3).
            const hasActiveMatchTab = tabs.some(tab => /\/matches\/[\w-]+/i.test(tab.url || ""));
            const blockedStatuses = new Set(["playing", "waitForPlayer", "waitForMatch"]);
            if (!blockedStatuses.has(matchStatus) && !hasActiveMatchTab) {
                const commandInfo = parseCommandInfoFromMatchLabel(board.currentMatchLabel);
                const sent = await resolveAndSendPrepareMatch(
                    apiBaseUrl,
                    board.tournamentId,
                    board.currentMatchId,
                    tabs,
                    commandInfo
                );
                if (sent) {
                    setMatchStatus("waitForPlayer");
                    await setLastHandledCurrentMatchId(board.currentMatchId);
                }
            } else {
                const reason = hasActiveMatchTab ? "active match tab detected" : "board not idle";
                logTraffic("IN", `prepareMatch skipped (${reason})`, {
                    boardId,
                    currentMatchId: board.currentMatchId,
                    matchStatus,
                    hasActiveMatchTab
                });
                // Do NOT update lastHandledCurrentMatchId — retry after match ends.
            }
        } else if (board && !board.currentMatchId) {
            await setLastHandledCurrentMatchId(null);
        }
    } catch {
        // API may be offline
        setDstStatus("offline");
    }
}

async function fetchAutodartsAccessToken(tabs) {
    for (const tab of tabs) {
        try {
            const result = await chrome.tabs.sendMessage(tab.id, { action: "getAutodartsAccessToken" });
            if (result?.ok && result.accessToken) {
                return result.accessToken;
            }
        } catch { /* tab may not have content script */ }
    }

    return null;
}

async function ensureApiAutodartsSessionFromTabs(apiBaseUrl, tabs) {
    const nowMs = Date.now();
    if (nowMs - lastApiSessionSyncMs < API_SESSION_SYNC_INTERVAL_MS) {
        return;
    }

    const accessToken = await fetchAutodartsAccessToken(tabs);
    if (!accessToken) {
        return;
    }

    if (lastApiSessionToken === accessToken && nowMs - lastApiSessionSyncMs < API_SESSION_SYNC_INTERVAL_MS * 5) {
        return;
    }

    try {
        const response = await loggedFetch(`${apiBaseUrl}/api/autodarts/token-login`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ accessToken })
        });

        if (response.ok) {
            lastApiSessionSyncMs = nowMs;
            lastApiSessionToken = accessToken;
            return;
        }

        if (response.status === 401) {
            lastApiSessionToken = null;
        }
    } catch {
        // API may be unavailable; regular polling handles offline status.
    }
}

async function fetchAutodartsBoards(tabs) {
    for (const tab of tabs) {
        try {
            const result = await chrome.tabs.sendMessage(tab.id, { action: "getLocalBoards" });
            if (result?.ok && Array.isArray(result.boards) && result.boards.length > 0) {
                return result.boards;
            }
        } catch { /* tab may not have content script */ }
    }

    return [];
}

async function fetchFirstPageState(tabs) {
    for (const tab of tabs) {
        try {
            const state = await chrome.tabs.sendMessage(tab.id, { action: "getPageState" });
            if (state?.url) {
                return state;
            }
        } catch { /* silent */ }
    }

    return null;
}

function extractPlayerNames(rawPlayers) {
    if (!Array.isArray(rawPlayers)) return [];

    return rawPlayers
        .map((player, arrayIndex) => {
            if (!player || typeof player !== "object") return { player, sortIndex: arrayIndex };
            const explicitIndex = Number.isInteger(player.index) ? player.index : arrayIndex;
            return { player, sortIndex: explicitIndex };
        })
        .sort((left, right) => left.sortIndex - right.sortIndex)
        .map(entry => entry.player)
        .map(player => {
            if (typeof player === "string") return player;
            if (player && typeof player === "object") return player.name || player.displayName || player.accountName || "";
            return "";
        })
        .map(name => (name || "").trim())
        .filter(Boolean);
}

function extractPlayersFromAutodartsMatch(rawMatch) {
    if (!rawMatch || !Array.isArray(rawMatch.players)) return [];

    return rawMatch.players
        .map((player, arrayIndex) => {
            if (!player || typeof player !== "object") return { player, sortIndex: arrayIndex };
            const explicitIndex = Number.isInteger(player.index) ? player.index : arrayIndex;
            return { player, sortIndex: explicitIndex };
        })
        .sort((left, right) => left.sortIndex - right.sortIndex)
        .map(entry => entry.player)
        .map(player => {
            if (typeof player === "string") return player;
            if (!player || typeof player !== "object") return "";
            if (typeof player.name === "string") return player.name;
            if (typeof player.displayName === "string") return player.displayName;
            if (typeof player.accountName === "string") return player.accountName;

            const user = player.user;
            if (user && typeof user === "object") {
                return user.name || user.displayName || user.accountName || user.username || "";
            }

            return "";
        })
        .map(name => (name || "").trim())
        .filter(Boolean);
}

async function resolvePlayersForSync(apiBaseUrl, board, pageState) {
    const pagePlayers = extractPlayerNames(pageState?.currentMatchPlayers || []);
    if (pagePlayers.length >= 2) {
        return [pagePlayers[0], pagePlayers[1]];
    }

    const externalMatchId = pageState?.matchId || null;
    if (externalMatchId) {
        try {
            const matchResp = await loggedFetch(`${apiBaseUrl}/api/autodarts/matches/${externalMatchId}`);
            if (matchResp.ok) {
                const rawMatch = await matchResp.json();
                const externalPlayers = extractPlayersFromAutodartsMatch(rawMatch);
                if (externalPlayers.length >= 2) {
                    return [externalPlayers[0], externalPlayers[1]];
                }
            }
        } catch { /* silent */ }
    }

    if (!board?.tournamentId || !board?.currentMatchId) {
        return [pagePlayers[0] || null, pagePlayers[1] || null];
    }

    try {
        const [matchesResp, participantsResp] = await Promise.all([
            loggedFetch(`${apiBaseUrl}/api/matches/${board.tournamentId}`),
            loggedFetch(`${apiBaseUrl}/api/tournaments/${board.tournamentId}/participants`)
        ]);
        if (!matchesResp.ok || !participantsResp.ok) {
            return [pagePlayers[0] || null, pagePlayers[1] || null];
        }

        const [matches, participants] = await Promise.all([matchesResp.json(), participantsResp.json()]);
        const currentMatch = Array.isArray(matches) ? matches.find(m => m.id === board.currentMatchId) : null;
        if (!currentMatch) {
            return [pagePlayers[0] || null, pagePlayers[1] || null];
        }

        const home = Array.isArray(participants)
            ? participants.find(p => p.id === currentMatch.homeParticipantId)
            : null;
        const away = Array.isArray(participants)
            ? participants.find(p => p.id === currentMatch.awayParticipantId)
            : null;

        const player1 = home?.displayName || home?.accountName || pagePlayers[0] || null;
        const player2 = away?.displayName || away?.accountName || pagePlayers[1] || null;
        return [player1, player2];
    } catch {
        return [pagePlayers[0] || null, pagePlayers[1] || null];
    }
}

async function handleManualBoardSyncRequest(apiBaseUrl, board, tabs) {
    try {
        const consumeResp = await loggedFetch(`${apiBaseUrl}/api/boards/${board.id}/extension-sync/consume`, {
            method: "POST"
        });
        if (!consumeResp.ok) return;

        const consume = await consumeResp.json();
        console.log("DartSuite BG: Manual board sync consume response", consume);
        if (!consume?.shouldSync) return;

        const [pageState, adBoards] = await Promise.all([
            fetchFirstPageState(tabs),
            fetchAutodartsBoards(tabs)
        ]);

        const adBoard = Array.isArray(adBoards)
            ? adBoards.find(x => x.id === board.externalBoardId)
            : null;
        const [player1, player2] = await resolvePlayersForSync(apiBaseUrl, board, pageState);

        const payload = {
            requestId: consume.requestId || null,
            tournamentId: board.tournamentId || pageState?.tournamentId || null,
            sourceUrl: pageState?.url || null,
            externalMatchId: adBoard?.matchId || pageState?.matchId || null,
            player1,
            player2,
            matchStatus
        };

        console.log("DartSuite BG: Manual board sync payload", payload);

        const reportResp = await loggedFetch(`${apiBaseUrl}/api/boards/${board.id}/extension-sync/report`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        const responseText = await reportResp.text();
        console.log(`DartSuite BG: Manual board sync response (${reportResp.status})`, responseText);
        if (!reportResp.ok) {
            console.warn("DartSuite BG: Manual board sync report failed", {
                boardId: board.id,
                status: reportResp.status,
                responseText
            });
        }
    } catch (error) {
        console.warn("DartSuite BG: Manual board sync request failed", {
            boardId: board?.id,
            error: error?.message || String(error)
        });
    }
}

async function tryManualSyncSweep(apiBaseUrl, boards, tabs, preferredBoardId, options) {
    if (!Array.isArray(boards) || boards.length === 0) return;

    const force = !!options?.force;
    const nowMs = Date.now();
    if (!force && nowMs - lastManualSyncSweepMs < MANUAL_SYNC_SWEEP_THROTTLE_MS) {
        return;
    }

    lastManualSyncSweepMs = nowMs;

    const orderedBoards = preferredBoardId
        ? [
            ...boards.filter(board => board.id === preferredBoardId),
            ...boards.filter(board => board.id !== preferredBoardId)
        ]
        : boards;

    for (const board of orderedBoards) {
        await handleManualBoardSyncRequest(apiBaseUrl, board, tabs);
    }
}

// Derive match status from board and Autodarts state
async function updateMatchStatusFromBoard(apiBaseUrl, board, tabs) {
    // Check if there's a running match on the Autodarts side
    const adBoards = await fetchAutodartsBoards(tabs);

    const adBoard = adBoards.find(b => b.id === board.externalBoardId);

    // Check if there's a match running on Autodarts
    if (adBoard?.matchId) {
        setMatchStatus("playing");
        return;
    }

    // Check the page URL for lobby/match states via content script
    const pageState = await fetchFirstPageState(tabs);
    if (pageState?.url) {
        if (pageState.url.includes("/lobbies/") && !pageState.url.includes("/lobbies/new")) {
            setMatchStatus("waitForMatch");
            return;
        }

        if (pageState.matchId) {
            // On a match page but no local board match id yet
            setMatchStatus("ended");
            return;
        }
    }

    // Check if the board has a scheduled match in DartSuite
    if (board.currentMatchId) {
        // A match is assigned but not running on Autodarts yet
        setMatchStatus("scheduled");
        return;
    }

    // Check if there's a next scheduled match at this board
    if (board.schedulingStatus === "Scheduled" || board.schedulingStatus === "Geplant") {
        setMatchStatus("scheduled");
        return;
    }

    // Default: idle in managed mode
    setMatchStatus("idle");
}

async function syncBoardStatuses(apiBaseUrl, dsBoards, tabs) {
    // Request captured Autodarts boards from content script
    const adBoards = await fetchAutodartsBoards(tabs);
    if (adBoards.length === 0) return;

    for (const dsBoard of dsBoards) {
        const adBoard = adBoards.find(b => b.id === dsBoard.externalBoardId);
        if (!adBoard) continue;

        // Derive status from Autodarts board state
        let newStatus;
        if (!adBoard.state?.connected) {
            newStatus = "Offline";
        } else if (adBoard.matchId) {
            newStatus = "Running";
        } else {
            newStatus = "Online";
        }

        const lastStatus = lastSyncedBoardStatuses.get(dsBoard.externalBoardId);

        // Board status transitions are tracked for logging; match sync handled server-side

        // Always send externalMatchId when Running, even if status hasn't changed,
        // because the match might not have been linked yet on a previous update
        const shouldUpdate = lastStatus !== newStatus
            || (newStatus === "Running" && adBoard.matchId);

        if (!shouldUpdate) continue;

        try {
            let url = `${apiBaseUrl}/api/boards/${dsBoard.id}/status?status=${encodeURIComponent(newStatus)}`;
            // When a match is running, pass the Autodarts match ID so the API can store it
            if (adBoard.matchId) {
                url += `&externalMatchId=${encodeURIComponent(adBoard.matchId)}`;
            }
            await loggedFetch(url, {
                method: "PATCH"
            });
            lastSyncedBoardStatuses.set(dsBoard.externalBoardId, newStatus);
            console.log(`DartSuite BG: Board ${dsBoard.name} status → ${newStatus}${adBoard.matchId ? ` (matchId: ${adBoard.matchId})` : ''}`);
        } catch { /* silent */ }
    }
}

function parseCommandInfoFromMatchLabel(currentMatchLabel) {
    if (typeof currentMatchLabel !== "string" || !currentMatchLabel.trim()) return null;

    const initAndCode = currentMatchLabel.match(/\[ds:init=([^;\]]+);code=([^\]]+)\]/i);
    if (initAndCode) {
        const initiatorName = (initAndCode[1] || "").trim();
        const commandLabel = (initAndCode[2] || "").trim();
        return {
            initiatorName: initiatorName || null,
            commandLabel: commandLabel || null
        };
    }

    const codeOnly = currentMatchLabel.match(/\[ds:code=([^\]]+)\]/i);
    if (codeOnly) {
        const commandLabel = (codeOnly[1] || "").trim();
        return {
            initiatorName: null,
            commandLabel: commandLabel || null
        };
    }

    return null;
}

async function resolveAndSendPrepareMatch(apiBaseUrl, tournamentId, matchId, tabs, commandInfo) {
    if (!dartsuiteEnabled || !tournamentId) return false;
    try {
        const response = await loggedFetch(`${apiBaseUrl}/api/matches/${tournamentId}`);
        if (!response.ok) return false;
        const matches = await response.json();
        const match = matches.find(m => m.id === matchId);
        if (!match) return false;

        let participants = [];
        try {
            const pResp = await loggedFetch(`${apiBaseUrl}/api/tournaments/${tournamentId}/participants`);
            if (pResp.ok) participants = await pResp.json();
        } catch { /* silent */ }

        const homeP = participants.find(p => p.id === match.homeParticipantId);
        const awayP = participants.find(p => p.id === match.awayParticipantId);
        const homeName = homeP?.displayName || "Spieler 1";
        const awayName = awayP?.displayName || "Spieler 2";

        let roundSettings = null;
        try {
            const rsResp = await loggedFetch(`${apiBaseUrl}/api/tournaments/${tournamentId}/rounds`);
            if (rsResp.ok) {
                const rounds = await rsResp.json();
                roundSettings = rounds.find(r => r.phase === match.phase && r.roundNumber === match.round);
            }
        } catch { /* silent */ }

        let sent = false;

        for (const tab of tabs) {
            logTraffic("OUT", "prepareMatch", { tabId: tab.id, matchId: match.id, tournamentId });
            try {
                const response = await chrome.tabs.sendMessage(tab.id, {
                    action: "prepareMatch",
                    payload: {
                        matchId: match.id,
                        players: [
                            { name: homeName, isAutodarts: homeP?.isAutodartsAccount !== false },
                            { name: awayName, isAutodarts: awayP?.isAutodartsAccount !== false }
                        ],
                        plannedStartUtc: match.plannedStartUtc || null,
                        variant: "X01",
                        settings: {
                            baseScore: roundSettings?.baseScore || 501,
                            inMode: roundSettings?.inMode || "Straight",
                            outMode: roundSettings?.outMode || "Double",
                            maxRounds: roundSettings?.maxRounds || 50,
                            bullMode: roundSettings?.bullMode || "25/50"
                        },
                        bullOffMode: roundSettings?.bullOffMode || "Normal",
                        gameMode: roundSettings?.gameMode || "Legs",
                        legs: roundSettings?.legs || 3,
                        sets: roundSettings?.sets || null,
                        isPrivate: true,
                        commandInfo: {
                            initiatorName: commandInfo?.initiatorName || null,
                            commandLabel: commandInfo?.commandLabel || null
                        }
                    }
                });

                if (response?.ok) {
                    logTraffic("IN", "prepareMatch acknowledged", { tabId: tab.id, matchId: match.id });
                    sent = true;
                } else {
                    logTraffic("IN", "prepareMatch no-ack", { tabId: tab.id, matchId: match.id, response: response || null });
                }
            } catch (error) {
                logTraffic("IN", "prepareMatch dispatch failed", {
                    tabId: tab.id,
                    matchId: match.id,
                    error: error?.message || String(error)
                });
            }
        }

        if (!sent) {
            logTraffic("IN", "prepareMatch not delivered; retry on next poll", { matchId: match.id, tournamentId });
        }

        return sent;
    } catch {
        return false;
    }
}

async function stopSignalRConnection() {
    chrome.alarms.clear(POLL_ALARM_NAME);
    await chrome.storage.local.remove("pollingBoardId");
    await setLastHandledCurrentMatchId(null);
}

// ─── Request Next Match ───

async function handleRequestNextMatch(message) {
    if (!dartsuiteEnabled) return;

    const { tournamentId, boardId } = message;
    if (!tournamentId || !boardId) return;

    const apiBaseUrl = await getApiBaseUrl();
    try {
        const response = await loggedFetch(`${apiBaseUrl}/api/matches/${tournamentId}`);
        if (!response.ok) return;
        const matches = await response.json();

        // Find next unplayed match assigned to this board
        const next = matches.find(m =>
            m.boardId === boardId && !m.finishedUtc && !m.startedUtc
        );

        if (!next) return;

        // Get participants to resolve names
        let participants = [];
        try {
            const pResp = await loggedFetch(`${apiBaseUrl}/api/tournaments/${tournamentId}/participants`);
            if (pResp.ok) participants = await pResp.json();
        } catch { /* silent */ }

        const homeP = participants.find(p => p.id === next.homeParticipantId);
        const awayP = participants.find(p => p.id === next.awayParticipantId);
        const homeName = homeP?.displayName || "Spieler 1";
        const awayName = awayP?.displayName || "Spieler 2";

        // Get round settings for this match
        let roundSettings = null;
        try {
            const rsResp = await loggedFetch(`${apiBaseUrl}/api/tournaments/${tournamentId}/rounds`);
            if (rsResp.ok) {
                const rounds = await rsResp.json();
                roundSettings = rounds.find(r => r.phase === next.phase && r.roundNumber === next.round);
            }
        } catch { /* silent */ }

        const tabs = await chrome.tabs.query({ url: "https://play.autodarts.io/*" });
        for (const tab of tabs) {
            chrome.tabs.sendMessage(tab.id, {
                action: "prepareMatch",
                payload: {
                    matchId: next.id,
                    players: [
                        { name: homeName, isAutodarts: homeP?.isAutodartsAccount !== false },
                        { name: awayName, isAutodarts: awayP?.isAutodartsAccount !== false }
                    ],
                    plannedStartUtc: next.plannedStartUtc || null,
                    variant: "X01",
                    settings: {
                        baseScore: roundSettings?.baseScore || 501,
                        inMode: roundSettings?.inMode || "Straight",
                        outMode: roundSettings?.outMode || "Double",
                        maxRounds: roundSettings?.maxRounds || 50,
                        bullMode: roundSettings?.bullMode || "25/50"
                    },
                    bullOffMode: roundSettings?.bullOffMode || "Normal",
                    gameMode: roundSettings?.gameMode || "Legs",
                    legs: roundSettings?.legs || 3,
                    sets: roundSettings?.sets || null,
                    isPrivate: true
                }
            }).catch(() => { });
        }
    } catch { /* silent */ }
}

// ─── Sync Match Result via Autodarts API ───

// handleSyncMatchResult removed: live match sync is now handled server-side
// by AutodartsMatchListenerService polling the Autodarts API directly.

// ─── Proxy Fetch ───

async function proxyFetch(url, options) {
    try {
        const response = await loggedFetch(url, options || {});
        const contentType = response.headers.get("content-type") || "";
        let body;
        if (contentType.includes("application/json")) {
            body = await response.json();
        } else {
            body = await response.text();
        }
        return { ok: response.ok, status: response.status, body };
    } catch (error) {
        return { ok: false, error: error?.message };
    }
}

// ─── Page Reporting ───

async function reportPageToApi(url) {
    if (!dartsuiteEnabled) return { ok: false, disabled: true };

    const apiBaseUrl = await getApiBaseUrl();
    const matchId = extractId(url, /\/matches\/([\w-]+)/i);
    const lobbyId = extractId(url, /\/lobbies\/([\w-]+)/i);

    // Quick manual-sync sweep triggered by page activity.
    try {
        const [boardsResponse, tabs] = await Promise.all([
            loggedFetch(`${apiBaseUrl}/api/boards`),
            chrome.tabs.query({ url: "https://play.autodarts.io/*" })
        ]);

        if (boardsResponse.ok) {
            const boards = await boardsResponse.json();
            await tryManualSyncSweep(apiBaseUrl, boards, tabs, null);
        }
    } catch {
        // Optional optimization only.
    }

    // Auto-sync board status from URL
    if ((matchId || lobbyId) && dartsuiteEnabled) {
        try {
            const [local, sync] = await Promise.all([
                chrome.storage.local.get("pollingBoardId"),
                chrome.storage.sync.get({ managedTournamentId: null })
            ]);
            const boardId = local.pollingBoardId;
            if (boardId) {
                if (matchId) {
                    // On match page → report Running + externalMatchId immediately
                    await loggedFetch(
                        `${apiBaseUrl}/api/boards/${boardId}/status?status=Running&externalMatchId=${encodeURIComponent(matchId)}`,
                        { method: "PATCH" }
                    );
                } else if (lobbyId && !url.includes("/lobbies/new")) {
                    // On lobby page → report Warten
                    const tournamentId = sync.managedTournamentId;
                    await loggedFetch(`${apiBaseUrl}/api/boards/${boardId}/extension-sync/report`, {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({
                            requestId: null,
                            tournamentId: tournamentId || null,
                            sourceUrl: url,
                            externalMatchId: null,
                            player1: null,
                            player2: null,
                            matchStatus: matchStatus
                        })
                    });
                }
            }
        } catch { /* silent — optional optimization */ }
    }

    if (!matchId && !lobbyId) return { ok: true, skipped: true };

    try {
        const response = await loggedFetch(`${apiBaseUrl}/api/autodarts/page-event`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ sourceUrl: url, matchId, lobbyId })
        });
        return { ok: response.ok, status: response.status };
    } catch (error) {
        return { ok: false, error: error?.message };
    }
}

// ─── Helpers ───

function extractId(url, pattern) {
    return url?.match(pattern)?.[1] || null;
}

async function getApiBaseUrl() {
    try {
        const stored = await chrome.storage.sync.get({ apiBaseUrl: DEFAULT_API_BASE_URL });
        return (stored.apiBaseUrl || DEFAULT_API_BASE_URL).replace(/\/$/, "");
    } catch {
        return DEFAULT_API_BASE_URL;
    }
}