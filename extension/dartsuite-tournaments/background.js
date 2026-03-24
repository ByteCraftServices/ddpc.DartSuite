// DartSuite Tournaments — Background Service Worker v0.3.0
// Icon badge management, SignalR WebSocket relay, proxy fetch.

const DEFAULT_API_BASE_URL = "http://localhost:5290";

// Icon status tracking
let currentIconStatus = "default";

chrome.runtime.onInstalled.addListener(() => {
    console.log("DartSuite Tournaments extension installed v0.3.0");
    updateIconBadge("default");
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

        case "setIconStatus":
            updateIconBadge(message.status);
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

        default:
            break;
    }
});

// ─── Icon Badge Management ───

function updateIconBadge(status) {
    currentIconStatus = status;

    const badges = {
        default: { text: "", color: "#888" },
        error: { text: "!", color: "#f44336" },
        warning: { text: "?", color: "#ff9800" },
        configured: { text: "", color: "#888" },
        connected: { text: "✓", color: "#4caf50" },
        active: { text: "▶", color: "#4caf50" }
    };

    const badge = badges[status] || badges.default;
    chrome.action.setBadgeText({ text: badge.text });
    chrome.action.setBadgeBackgroundColor({ color: badge.color });
}

// ─── Tournament Events ───

function handleTournamentSelected(tournament) {
    if (!tournament) {
        updateIconBadge("warning");
        return;
    }

    // Check if tournament is currently running
    const today = new Date().toISOString().split("T")[0];
    const isActive = tournament.startDate <= today && tournament.endDate >= today;
    updateIconBadge(isActive ? "active" : "connected");

    console.log("DartSuite BG: Tournament selected", tournament.name, tournament.joinCode);
}

async function handleManagedModeChanged(message) {
    const { boardId, mode, tournamentId, tournamentName, host, boardName } = message;

    if (mode === "Auto") {
        updateIconBadge("active");
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
        updateIconBadge("connected");
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
        if (!response.ok) return;
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

        // Live match sync is handled server-side by AutodartsMatchListenerService

        if (board && board.currentMatchId && board.currentMatchId !== lastHandledCurrentMatchId) {
            lastHandledCurrentMatchId = board.currentMatchId;
            // Resolve full match data and send prepareMatch
            await resolveAndSendPrepareMatch(apiBaseUrl, board.tournamentId, board.currentMatchId, tabs);
        } else if (board && !board.currentMatchId) {
            lastHandledCurrentMatchId = null;
        }
    } catch { /* API may be offline */ }
}

async function syncBoardStatuses(apiBaseUrl, dsBoards, tabs) {
    // Request captured Autodarts boards from content script
    let adBoards = [];
    for (const tab of tabs) {
        try {
            const result = await chrome.tabs.sendMessage(tab.id, { action: "getLocalBoards" });
            if (result?.ok && Array.isArray(result.boards) && result.boards.length > 0) {
                adBoards = result.boards;
                break;
            }
        } catch { /* tab may not have content script */ }
    }
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