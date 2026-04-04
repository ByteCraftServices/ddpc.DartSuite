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

chrome.runtime.onInstalled.addListener(() => {
    console.log("DartSuite Tournaments extension installed v0.4.0");
    updateIcon();
    scheduleStatusPolling();
});

chrome.runtime.onStartup.addListener(() => {
    scheduleStatusPolling();
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

        case "managedModeChanged":
            handleManagedModeChanged(message);
            sendResponse({ ok: true });
            break;

        case "requestNextMatch":
            handleRequestNextMatch(message);
            sendResponse({ ok: true });
            break;

        case "getStatus":
            sendResponse({ dstStatus, matchStatus });
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
    const { statusPollSeconds = 30 } = await chrome.storage.sync.get({ statusPollSeconds: 30 });
    const seconds = Math.max(10, Number(statusPollSeconds) || 30);
    const minutes = Math.max(seconds / 60, 0.5);
    chrome.alarms.create(STATUS_ALARM_NAME, { periodInMinutes: minutes });
    await checkApiHealth();
}

chrome.alarms.onAlarm.addListener(async (alarm) => {
    if (alarm.name === STATUS_ALARM_NAME) {
        await checkApiHealth();
    }
});

async function checkApiHealth() {
    const apiBaseUrl = await getApiBaseUrl();
    try {
        const response = await fetch(`${apiBaseUrl}/api/boards`, { signal: AbortSignal.timeout(3000) });
        if (!response.ok) {
            await chrome.storage.local.set({
                apiLastError: `HTTP ${response.status}`,
                apiLastErrorUtc: new Date().toISOString()
            });
            setDstStatus("offline");
            return;
        }
        const { tournamentId } = await chrome.storage.sync.get({ tournamentId: "" });
        await chrome.storage.local.remove(["apiLastError", "apiLastErrorUtc"]);
        setDstStatus(tournamentId ? "connected" : "ready");
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
    if (!tournament) {
        setDstStatus("ready");
        return;
    }

    // Check if tournament is currently running
    const today = new Date().toISOString().split("T")[0];
    const isActive = tournament.startDate <= today && tournament.endDate >= today;
    setDstStatus("connected");

    console.log("DartSuite BG: Tournament selected", tournament.name, tournament.joinCode);
}

async function handleManagedModeChanged(message) {
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
            await fetch(`${apiBaseUrl}/api/boards/${boardId}/managed?mode=Auto&tournamentId=${tournamentId}`, {
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
            await fetch(`${apiBaseUrl}/api/boards/${boardId}/managed?mode=Manual`, { method: "PATCH" });
        } catch { /* silent */ }

        await stopSignalRConnection();
    }
}

// ─── Board Polling (reliable via chrome.alarms) ───
// Service workers can be terminated at any time by Chrome, killing setInterval.
// chrome.alarms survives service worker restarts and wakes it back up.

const POLL_ALARM_NAME = "dsPolling";
let lastHandledCurrentMatchId = null;
let lastSyncedBoardStatuses = new Map(); // externalBoardId -> status

async function startSignalRConnection(boardId) {
    lastHandledCurrentMatchId = null;
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
    const { pollingBoardId } = await chrome.storage.local.get("pollingBoardId");
    if (!pollingBoardId) {
        chrome.alarms.clear(POLL_ALARM_NAME);
        return;
    }
    await doPollCycle(pollingBoardId);
});

// On service worker startup, resume polling if it was active
chrome.runtime.onStartup.addListener(async () => {
    const { pollingBoardId } = await chrome.storage.local.get("pollingBoardId");
    if (pollingBoardId) {
        console.log("DartSuite BG: Resuming polling after restart for", pollingBoardId);
        chrome.alarms.create(POLL_ALARM_NAME, { periodInMinutes: 10 / 60 });
    }
});

async function doPollCycle(boardId) {
    try {
        const apiBaseUrl = await getApiBaseUrl();
        const response = await fetch(`${apiBaseUrl}/api/boards`);
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
                await fetch(`${apiBaseUrl}/api/boards/${boardId}/heartbeat`, { method: "PATCH" });
            } catch { /* silent */ }
        }

        // Sync Autodarts board status to DartSuite
        await syncBoardStatuses(apiBaseUrl, boards, tabs);

        // Update match status based on board state
        if (board) {
            await updateMatchStatusFromBoard(apiBaseUrl, board, tabs);
            await handleManualBoardSyncRequest(apiBaseUrl, board, tabs);
        }

        // Live match sync is handled server-side by AutodartsMatchListenerService

        if (board && board.currentMatchId && board.currentMatchId !== lastHandledCurrentMatchId) {
            lastHandledCurrentMatchId = board.currentMatchId;
            // Resolve full match data and send prepareMatch
            await resolveAndSendPrepareMatch(apiBaseUrl, board.tournamentId, board.currentMatchId, tabs);
        } else if (board && !board.currentMatchId) {
            lastHandledCurrentMatchId = null;
        }
    } catch {
        // API may be offline
        setDstStatus("offline");
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
            const matchResp = await fetch(`${apiBaseUrl}/api/autodarts/matches/${externalMatchId}`);
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
            fetch(`${apiBaseUrl}/api/matches/${board.tournamentId}`),
            fetch(`${apiBaseUrl}/api/tournaments/${board.tournamentId}/participants`)
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
        const consumeResp = await fetch(`${apiBaseUrl}/api/boards/${board.id}/extension-sync/consume`, {
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

        const reportResp = await fetch(`${apiBaseUrl}/api/boards/${board.id}/extension-sync/report`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        const responseText = await reportResp.text();
        console.log(`DartSuite BG: Manual board sync response (${reportResp.status})`, responseText);
    } catch { /* silent */ }
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
            await fetch(url, {
                method: "PATCH"
            });
            lastSyncedBoardStatuses.set(dsBoard.externalBoardId, newStatus);
            console.log(`DartSuite BG: Board ${dsBoard.name} status → ${newStatus}${adBoard.matchId ? ` (matchId: ${adBoard.matchId})` : ''}`);
        } catch { /* silent */ }
    }
}

async function resolveAndSendPrepareMatch(apiBaseUrl, tournamentId, matchId, tabs) {
    if (!tournamentId) return;
    try {
        const response = await fetch(`${apiBaseUrl}/api/matches/${tournamentId}`);
        if (!response.ok) return;
        const matches = await response.json();
        const match = matches.find(m => m.id === matchId);
        if (!match) return;

        let participants = [];
        try {
            const pResp = await fetch(`${apiBaseUrl}/api/tournaments/${tournamentId}/participants`);
            if (pResp.ok) participants = await pResp.json();
        } catch { /* silent */ }

        const homeP = participants.find(p => p.id === match.homeParticipantId);
        const awayP = participants.find(p => p.id === match.awayParticipantId);
        const homeName = homeP?.displayName || "Spieler 1";
        const awayName = awayP?.displayName || "Spieler 2";

        let roundSettings = null;
        try {
            const rsResp = await fetch(`${apiBaseUrl}/api/tournaments/${tournamentId}/rounds`);
            if (rsResp.ok) {
                const rounds = await rsResp.json();
                roundSettings = rounds.find(r => r.phase === match.phase && r.roundNumber === match.round);
            }
        } catch { /* silent */ }

        for (const tab of tabs) {
            chrome.tabs.sendMessage(tab.id, {
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
                    isPrivate: true
                }
            }).catch(() => { });
        }
    } catch { /* silent */ }
}

async function stopSignalRConnection() {
    chrome.alarms.clear(POLL_ALARM_NAME);
    await chrome.storage.local.remove("pollingBoardId");
}

// ─── Request Next Match ───

async function handleRequestNextMatch(message) {
    const { tournamentId, boardId } = message;
    if (!tournamentId || !boardId) return;

    const apiBaseUrl = await getApiBaseUrl();
    try {
        const response = await fetch(`${apiBaseUrl}/api/matches/${tournamentId}`);
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
            const pResp = await fetch(`${apiBaseUrl}/api/tournaments/${tournamentId}/participants`);
            if (pResp.ok) participants = await pResp.json();
        } catch { /* silent */ }

        const homeP = participants.find(p => p.id === next.homeParticipantId);
        const awayP = participants.find(p => p.id === next.awayParticipantId);
        const homeName = homeP?.displayName || "Spieler 1";
        const awayName = awayP?.displayName || "Spieler 2";

        // Get round settings for this match
        let roundSettings = null;
        try {
            const rsResp = await fetch(`${apiBaseUrl}/api/tournaments/${tournamentId}/rounds`);
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
        const response = await fetch(url, options || {});
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
    const apiBaseUrl = await getApiBaseUrl();
    const matchId = extractId(url, /\/matches\/([\w-]+)/i);
    const lobbyId = extractId(url, /\/lobbies\/([\w-]+)/i);

    if (!matchId && !lobbyId) return { ok: true, skipped: true };

    try {
        const response = await fetch(`${apiBaseUrl}/api/autodarts/page-event`, {
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