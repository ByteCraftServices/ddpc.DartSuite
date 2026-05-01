// DartSuite Tournaments — Content Script v0.4.0
// Runs on play.autodarts.io pages.
// Role: Menu injection, managed mode (lobby automation, gameshot/matchshot reporting),
//       board capture via MAIN world bridge, info bar display.
// Lobby flow: ALWAYS navigates via homescreen before creating a new lobby.

console.log("DartSuite content script loaded", location.href);

const DEFAULT_API_BASE_URL = "http://localhost:5290";
let lastReportedUrl = "";
let capturedBoards = [];
let capturedFriends = [];
let lastBoardsError = null;
let managedState = { active: false, boardId: null, tournamentId: null, tournamentName: "", host: "", boardName: "" };
let selectedTournamentContext = { tournamentId: null, tournamentName: "", host: "" };
let currentMatchPlayers = [];
let matchStartTime = null;
let cachedParticipants = [];
let participantsCacheTime = 0;
let externalBoardId = null;
let lobbyPrepareActive = false;
let currentDstStatus = "offline";
let currentMatchStatus = "available";
let statusBarMode = "always";
let debugModeEnabled = false;
let apiReachable = false;
let lastContextToastKey = "";
let lastContextToastAtMs = 0;
let dartsuiteEnabled = true;
let activeStartNotice = null;
let pendingMatchStatusTimer = null;
const MATCH_STATUS_TRANSITION_HOLD_MS = 1200;
const ACTIVE_MATCH_STATES = new Set(["playing", "waitForPlayer", "waitForMatch", "listening"]);
const PASSIVE_MATCH_STATES = new Set(["available", "idle", "scheduled", "ended"]);

function logTraffic(direction, message, details) {
    const prefix = `[DartSuite] [${direction}]: ${message}`;
    if (details !== undefined) {
        console.log(prefix, details);
    } else {
        console.log(prefix);
    }
}

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
            // Keep plain text body unchanged.
        }
    }

    return safe;
}

function refreshStatusPresentation() {
    updateInfoBarStatusTag();
    refreshInfoBarVisibility();
    refreshPanelVisibilityDuringMatch();
    if (debugModeEnabled) {
        showStatusToast(currentDstStatus, currentMatchStatus);
    }
}

/**
 * Auto-minimizes the DartSuite panel when a match starts ("playing" state) so the
 * Autodarts game screen is fully visible. Restores it when the match ends.
 * Issue #74: "Keine Panels/Overlays während eines laufenden Matches."
 */
function refreshPanelVisibilityDuringMatch() {
    const panel = document.getElementById("dartsuite-panel");
    if (!panel) return;

    if (currentMatchStatus === "playing") {
        // Minimize panel if not already minimized so Autodarts screen is unobstructed
        if (panel.dataset.minimized !== "true") {
            minimizeDartSuitePanel();
            panel.dataset.autoMinimized = "true";
        }
    } else if (panel.dataset.autoMinimized === "true") {
        // Restore panel when match ends (only if we were the one who minimized it)
        delete panel.dataset.autoMinimized;
        restoreDartSuitePanel();
    }
}

function applyIncomingMatchStatus(nextStatus) {
    if (!nextStatus || typeof nextStatus !== "string") return;

    if (ACTIVE_MATCH_STATES.has(currentMatchStatus) && PASSIVE_MATCH_STATES.has(nextStatus)) {
        clearTimeout(pendingMatchStatusTimer);
        pendingMatchStatusTimer = setTimeout(() => {
            currentMatchStatus = nextStatus;
            refreshStatusPresentation();
        }, MATCH_STATUS_TRANSITION_HOLD_MS);
        return;
    }

    clearTimeout(pendingMatchStatusTimer);
    pendingMatchStatusTimer = null;
    currentMatchStatus = nextStatus;
}

// Proxy fetch through background script to bypass mixed-content (HTTPS→HTTP)
async function apiFetch(url, options) {
    const method = (options?.method || "GET").toUpperCase();
    logTraffic("OUT", `${method} ${url}`, sanitizeOptionsForLog(options));
    const result = await chrome.runtime.sendMessage({ action: "proxyFetch", url, options });
    logTraffic("IN", `${method} ${url}`, result);
    return result;
}

// ─── Initialization ───

reportCurrentUrl("initial");
loadManagedState();
loadStatusBarSettings();
loadDebugModeSettings();
loadDartSuiteEnabledSetting();

chrome.storage.onChanged.addListener((changes, area) => {
    if (area !== "sync") return;
    if (changes.statusBarMode) {
        statusBarMode = changes.statusBarMode.newValue || "always";
        refreshInfoBarVisibility();
    }
    if (changes.debugMode) {
        debugModeEnabled = changes.debugMode.newValue === true;
    }
    if (changes.dartsuiteEnabled) {
        dartsuiteEnabled = changes.dartsuiteEnabled.newValue !== false;
        applyEnabledState();
    }
    if (changes.managedBoardId || changes.managedTournamentId || changes.managedTournamentName || changes.managedHost || changes.managedBoardName) {
        loadManagedState().then(() => {
            if (managedState.active) {
                refreshInfoBarVisibility();
                startSchedulePolling();
                updateInfoBarManagedContext();
                updateInfoBarSchedule();
            } else {
                removeInfoBar();
            }
        }).catch(() => { });
    }
});

// Listen for boards data from MAIN world bridge
window.addEventListener("message", (event) => {
    if (event.source !== window) return;
    if (event.data?.type === "dartsuite-boards-response" && Array.isArray(event.data.boards)) {
        capturedBoards = event.data.boards;
        lastBoardsError = null;
        console.log("DartSuite: Captured", capturedBoards.length, "boards from Autodarts API");
    }
    if (event.data?.type === "dartsuite-boards-error") {
        lastBoardsError = event.data.error || { message: "Boards konnten nicht geladen werden." };
        console.warn("DartSuite: Boards fetch error", lastBoardsError);
    }
    if (event.data?.type === "dartsuite-friends-response" && Array.isArray(event.data.friends)) {
        capturedFriends = event.data.friends;
        console.log("DartSuite: Captured", capturedFriends.length, "friends from Autodarts API");
    }
});

// SPA route watcher + periodic UI injection
setInterval(() => {
    if (!dartsuiteEnabled) {
        removeMenuEntry();
        removeInfoBar();
        closeDartSuitePanel();
        return;
    }

    if (location.href !== lastReportedUrl) {
        reportCurrentUrl("route-change");
        if (location.pathname.startsWith("/boards") && capturedBoards.length === 0) {
            window.postMessage({ type: "dartsuite-request-boards" }, "*");
        }
    }
    injectMenuEntry();
    if (managedState.active) {
        injectInfoBar();
    }
}, 2000);

// Initial board request
if (location.pathname.startsWith("/boards")) {
    setTimeout(() => {
        if (capturedBoards.length === 0) {
            window.postMessage({ type: "dartsuite-request-boards" }, "*");
        }
    }, 3000);
}

// ─── Message Handler ───

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
    switch (message.action) {
        case "debugTrafficLog":
            logTraffic(message.direction || "IN", message.message || "", message.details);
            sendResponse({ ok: true });
            break;

        case "ping":
            sendResponse({ ok: true, url: location.href });
            break;

        case "pageLoaded":
            reportCurrentUrl("page-loaded");
            sendResponse({ ok: true });
            break;

        case "getPageState":
            sendResponse(getPageState());
            break;

        case "getLocalBoards":
            // Always re-fetch fresh board data to get updated matchId
            window.postMessage({ type: "dartsuite-request-boards" }, "*");
            {
                const boardHandler = (event) => {
                    if (event.source !== window) return;
                    if (event.data?.type === "dartsuite-boards-response") {
                        window.removeEventListener("message", boardHandler);
                        sendResponse({ ok: true, boards: capturedBoards, boardError: lastBoardsError });
                    }
                };
                window.addEventListener("message", boardHandler);
                // Timeout fallback — return whatever we have
                setTimeout(() => {
                    window.removeEventListener("message", boardHandler);
                    sendResponse({ ok: true, boards: capturedBoards, boardError: lastBoardsError });
                }, 5000);
                return true; // async response
            }
            break;

        case "getLocalFriends":
            if (capturedFriends.length > 0) {
                sendResponse({ ok: true, friends: capturedFriends });
            } else {
                // Request from bridge and wait briefly
                window.postMessage({ type: "dartsuite-request-friends" }, "*");
                const friendHandler = (event) => {
                    if (event.source !== window) return;
                    if (event.data?.type === "dartsuite-friends-response") {
                        window.removeEventListener("message", friendHandler);
                        sendResponse({ ok: true, friends: capturedFriends });
                    }
                };
                window.addEventListener("message", friendHandler);
                // Timeout fallback
                setTimeout(() => {
                    window.removeEventListener("message", friendHandler);
                    sendResponse({ ok: true, friends: capturedFriends });
                }, 5000);
                return true; // async response
            }
            break;

        case "getAutodartsAccessToken":
            window.postMessage({ type: "dartsuite-request-auth-token" }, "*");
            {
                const tokenHandler = (event) => {
                    if (event.source !== window) return;
                    if (event.data?.type === "dartsuite-auth-token-response") {
                        window.removeEventListener("message", tokenHandler);
                        sendResponse({ ok: true, accessToken: event.data.token || null });
                    }
                };
                window.addEventListener("message", tokenHandler);
                setTimeout(() => {
                    window.removeEventListener("message", tokenHandler);
                    sendResponse({ ok: true, accessToken: null });
                }, 2000);
                return true;
            }
            break;

        case "prepareMatch":
            if (!dartsuiteEnabled) {
                sendResponse({ ok: false, disabled: true });
                break;
            }
            logTraffic("IN", "prepareMatch", message.payload);
            showStartExecutionOverlay(message.payload);
            handlePrepareMatch(message.payload);
            sendResponse({ ok: true });
            break;

        case "upcomingMatch":
            if (!dartsuiteEnabled) {
                sendResponse({ ok: false, disabled: true });
                break;
            }
            logTraffic("IN", "upcomingMatch", message.payload);
            handleUpcomingMatch(message.payload);
            sendResponse({ ok: true });
            break;

        case "refreshSchedule":
            pollSchedule();
            sendResponse({ ok: true });
            break;

        case "boardStatusUpdate":
            externalBoardId = message.externalBoardId;
            updatePlayButtons();
            sendResponse({ ok: true });
            break;

        case "setDebugMode":
            debugModeEnabled = message.enabled === true;
            sendResponse({ ok: true });
            break;

        case "dstStatusUpdate":
            currentDstStatus = message.dstStatus || currentDstStatus;
            applyIncomingMatchStatus(message.matchStatus);
            apiReachable = currentDstStatus !== "offline";
            refreshStatusPresentation();
            sendResponse({ ok: true });
            break;

        case "tournamentContextChanged":
            applyTournamentContext(message.payload);
            sendResponse({ ok: true });
            break;

        case "setManagedMode":
            setManagedMode(message.payload);
            sendResponse({ ok: true });
            break;

        default:
            break;
    }
});

// ─── Managed State ───

async function loadManagedState() {
    try {
        const stored = await chrome.storage.sync.get(["managedBoardId", "managedTournamentId", "managedTournamentName", "managedHost", "managedBoardName"]);
        selectedTournamentContext = {
            tournamentId: stored.managedTournamentId || null,
            tournamentName: stored.managedTournamentName || "",
            host: stored.managedHost || ""
        };
        if (stored.managedBoardId && stored.managedTournamentId) {
            managedState = {
                active: true,
                boardId: stored.managedBoardId,
                tournamentId: stored.managedTournamentId,
                tournamentName: stored.managedTournamentName || "",
                host: stored.managedHost || "",
                boardName: stored.managedBoardName || ""
            };
            // Resolve externalBoardId so isBoardFree() works on page load
            resolveExternalBoardId(stored.managedBoardId);
        } else {
            managedState = {
                active: false,
                boardId: null,
                tournamentId: selectedTournamentContext.tournamentId,
                tournamentName: selectedTournamentContext.tournamentName,
                host: selectedTournamentContext.host,
                boardName: ""
            };
            externalBoardId = null;
        }
    } catch { /* no state */ }
}

function setManagedMode(payload) {
    payload = payload || {};
    if (payload.mode === "Auto") {
        selectedTournamentContext = {
            tournamentId: payload.tournamentId || null,
            tournamentName: payload.tournamentName || "",
            host: payload.host || ""
        };

        managedState = {
            active: true,
            boardId: payload.boardId,
            tournamentId: payload.tournamentId,
            tournamentName: payload.tournamentName || "",
            host: payload.host || "",
            boardName: payload.boardName || ""
        };
        chrome.storage.sync.set({
            managedBoardId: payload.boardId,
            managedTournamentId: payload.tournamentId,
            managedTournamentName: payload.tournamentName,
            managedHost: payload.host,
            managedBoardName: payload.boardName
        });
        // Resolve externalBoardId from DartSuite API so isBoardFree() works
        resolveExternalBoardId(payload.boardId);
        refreshInfoBarVisibility();
        startSchedulePolling();
        updateInfoBarManagedContext();
        updateInfoBarSchedule();
        showManagedContextToast("Auswahl aktualisiert");
    } else {
        const keepTournamentContext = payload.keepTournamentContext === true;
        if (keepTournamentContext) {
            selectedTournamentContext = {
                tournamentId: payload.tournamentId || selectedTournamentContext.tournamentId,
                tournamentName: payload.tournamentName || selectedTournamentContext.tournamentName,
                host: payload.host || selectedTournamentContext.host
            };
        } else {
            selectedTournamentContext = { tournamentId: null, tournamentName: "", host: "" };
        }

        managedState = {
            active: false,
            boardId: null,
            tournamentId: keepTournamentContext ? selectedTournamentContext.tournamentId : null,
            tournamentName: keepTournamentContext ? selectedTournamentContext.tournamentName : "",
            host: keepTournamentContext ? selectedTournamentContext.host : "",
            boardName: ""
        };
        externalBoardId = null;
        if (keepTournamentContext) {
            chrome.storage.sync.remove(["managedBoardId", "managedBoardName"]);
            chrome.storage.sync.set({
                managedTournamentId: selectedTournamentContext.tournamentId,
                managedTournamentName: selectedTournamentContext.tournamentName,
                managedHost: selectedTournamentContext.host
            });
            showManagedContextToast("Turnier aktualisiert");
        } else {
            chrome.storage.sync.remove(["managedBoardId", "managedTournamentId", "managedTournamentName", "managedHost", "managedBoardName"]);
            showManagedContextToast("Managed Mode beendet");
        }
        hideStartExecutionOverlay();
        removeInfoBar();
    }
}

function applyTournamentContext(payload) {
    if (!payload) return;

    const nextTournamentId = payload.tournamentId || null;
    const nextTournamentName = payload.tournamentName || "";
    const nextHost = payload.host || "";

    const changed = selectedTournamentContext.tournamentId !== nextTournamentId
        || selectedTournamentContext.tournamentName !== nextTournamentName
        || selectedTournamentContext.host !== nextHost;

    selectedTournamentContext = {
        tournamentId: nextTournamentId,
        tournamentName: nextTournamentName,
        host: nextHost
    };

    chrome.storage.sync.set({
        managedTournamentId: selectedTournamentContext.tournamentId,
        managedTournamentName: selectedTournamentContext.tournamentName,
        managedHost: selectedTournamentContext.host
    });

    managedState.tournamentId = selectedTournamentContext.tournamentId;
    managedState.tournamentName = selectedTournamentContext.tournamentName;
    managedState.host = selectedTournamentContext.host;

    if (managedState.active) {
        refreshInfoBarVisibility();
        startSchedulePolling();
        updateInfoBarManagedContext();
        updateInfoBarSchedule();
    }

    if (changed) {
        showManagedContextToast("Turnier ausgewählt");
    }
}

async function resolveExternalBoardId(dsBoardId) {
    if (!dsBoardId) return;
    try {
        const apiBaseUrl = await getApiBaseUrl();
        const result = await apiFetch(`${apiBaseUrl}/api/boards`, { method: "GET" });
        if (result?.ok && Array.isArray(result.body)) {
            const board = result.body.find(b => b.id === dsBoardId);
            if (board?.externalBoardId) {
                externalBoardId = board.externalBoardId;
                console.log("DartSuite: Resolved externalBoardId", externalBoardId);
                updatePlayButtons();
            }
        }
    } catch { /* silent */ }
}

async function loadDartSuiteEnabledSetting() {
    try {
        const stored = await chrome.storage.sync.get({ dartsuiteEnabled: true });
        dartsuiteEnabled = stored.dartsuiteEnabled !== false;
    } catch {
        dartsuiteEnabled = true;
    }

    applyEnabledState();
}

function applyEnabledState() {
    if (!dartsuiteEnabled) {
        removeMenuEntry();
        removeInfoBar();
        hideStartExecutionOverlay();
        closeDartSuitePanel();
        return;
    }

    if (managedState.active) {
        refreshInfoBarVisibility();
    }
}

function removeMenuEntry() {
    const existing = document.getElementById("dartsuite-menu-entry");
    if (existing) {
        existing.remove();
    }
}

// ─── Menu Entry Injection ───

function injectMenuEntry() {
    if (!dartsuiteEnabled) {
        removeMenuEntry();
        return;
    }

    const existing = document.getElementById("dartsuite-menu-entry");

    // Find the sidebar navigation
    const nav = document.querySelector("nav") ||
        document.querySelector('[class*="sidebar"]') ||
        document.querySelector('[class*="css-"] > div > div > a')?.closest('[class*="css-"]')?.parentElement;
    if (!nav) return;

    // Detect collapsed sidebar: look for hidden text or narrow width
    const isCollapsed = nav.offsetWidth < 100;

    // Find the last nav link to insert after
    const navLinks = nav.querySelectorAll("a");
    if (navLinks.length === 0) return;
    const lastLink = navLinks[navLinks.length - 1];

    // Update label visibility on existing entry
    if (existing) {
        const labelSpan = existing.querySelector(".dartsuite-menu-label");
        const liveSpan = existing.querySelector(".dartsuite-menu-live");
        if (labelSpan) labelSpan.style.display = isCollapsed ? "none" : "inline";
        if (liveSpan) liveSpan.style.display = isCollapsed ? "none" : "inline";
        // Update live indicator
        if (liveSpan) liveSpan.innerHTML = managedState.active ? '● LIVE' : '';
        return;
    }

    const menuEntry = document.createElement("a");
    menuEntry.id = "dartsuite-menu-entry";
    menuEntry.href = "#dartsuite";
    menuEntry.style.cssText = `
        display: flex; align-items: center; gap: 10px;
        padding: 8px 16px; color: #e0e0e0; text-decoration: none;
        font-size: 14px; border-left: 3px solid transparent;
        transition: all 0.2s;
    `;
    menuEntry.title = "DartSuite Tournaments";
    menuEntry.innerHTML = `
        <span style="font-size:18px">🏆</span>
        <span class="dartsuite-menu-label" style="${isCollapsed ? 'display:none' : ''}">DartSuite</span>
        <span class="dartsuite-menu-live" style="color:#4caf50;font-size:10px;${isCollapsed ? 'display:none' : ''}">${managedState.active ? '● LIVE' : ''}</span>
    `;
    menuEntry.addEventListener("mouseenter", () => {
        menuEntry.style.background = "rgba(233,69,96,0.1)";
        menuEntry.style.borderLeftColor = "#e94560";
    });
    menuEntry.addEventListener("mouseleave", () => {
        menuEntry.style.background = "transparent";
        menuEntry.style.borderLeftColor = "transparent";
    });
    menuEntry.addEventListener("click", (e) => {
        e.preventDefault();
        toggleDartSuitePanel();
    });

    lastLink.insertAdjacentElement("afterend", menuEntry);
    console.log("DartSuite: Menu entry injected");
}

// ─── DartSuite Panel (in-page overlay) ───

async function toggleDartSuitePanel() {
    if (!dartsuiteEnabled) return;

    let panel = document.getElementById("dartsuite-panel");
    if (panel) {
        panel.remove();
        return;
    }

    panel = document.createElement("div");
    panel.id = "dartsuite-panel";
    panel.style.cssText = `
        position: fixed; top: 60px; right: 20px; width: 360px;
        background: #1a1a2e; border: 1px solid #333; border-radius: 8px;
        box-shadow: 0 8px 32px rgba(0,0,0,0.5); z-index: 10000;
        padding: 16px; color: #e0e0e0; font-family: 'Segoe UI', Arial, sans-serif;
        max-height: 80vh; overflow-y: auto;
    `;

    let content = `
        <div id="dartsuite-panel-header" style="display:flex;justify-content:space-between;align-items:center;margin-bottom:12px;cursor:move;user-select:none">
            <h3 style="margin:0;color:#fff;font-size:16px;pointer-events:none">🏆 DartSuite</h3>
            <div style="display:flex;gap:4px;align-items:center">
                <button id="dartsuite-panel-minimize" title="Minimieren" style="background:none;border:none;color:#aaa;cursor:pointer;font-size:16px;line-height:1;padding:0 4px">─</button>
                <button id="dartsuite-panel-close" style="background:none;border:none;color:#888;cursor:pointer;font-size:18px">✕</button>
            </div>
        </div>
        <div id="dartsuite-panel-body">
    `;

    if (managedState.active) {
        const boardDisplay = managedState.boardName || "Nicht ausgewählt";
        const boardColor = managedState.boardName ? "#aaa" : "#ff9800";
        content += `
            <div style="background:#0f3460;border-radius:6px;padding:10px;margin-bottom:10px;border-left:3px solid #4caf50">
                <div style="font-weight:bold">Turnier: ${escapeHtml(managedState.tournamentName)}</div>
                <div style="font-size:11px;color:#aaa">Host: ${escapeHtml(managedState.host)} | <span style="color:${boardColor}">Board: ${escapeHtml(boardDisplay)}</span></div>
                <div style="font-size:11px;color:#4caf50;margin-top:4px">● Managed Mode aktiv</div>
            </div>
        `;
        content += `
            <div id="dartsuite-panel-start-hint" style="display:none;background:#2a1d00;border:1px solid #ffb300;border-radius:6px;padding:10px;margin-bottom:10px;color:#ffe5a8"></div>
        `;
        if (!managedState.boardName) {
            content += `<div style="background:#4a3000;border:1px solid #ff9800;border-radius:4px;padding:8px;margin-bottom:10px;font-size:12px;color:#ffcc80">⚠ Kein Board ausgewählt. Bitte im Popup ein Board wählen, damit Matches gestartet werden können.</div>`;
        }
        // Show upcoming matches at this board
        content += `<div id="dartsuite-panel-upcoming" style="font-size:12px;color:#aaa;margin-bottom:8px">Lade nächste Matches...</div>`;

        // Show start button if there's a waiting match
        if (lastScheduleInfo) {
            const home = resolveParticipantName(lastScheduleInfo.homeParticipantId);
            const away = resolveParticipantName(lastScheduleInfo.awayParticipantId);
            const boardConnected = isBoardConnected();
            const boardHasActiveMatch = hasActiveAutodartsMatch();
            const canStart = apiReachable && !isMatchActive() && !boardHasActiveMatch;
            if (canStart) {
                const btnColor = boardConnected ? "#4caf50" : "#ff9800";
                const btnTitle = boardConnected
                    ? "Lobby für dieses Match öffnen"
                    : "Lobby öffnen (API erreichbar, Board nicht verbunden)";
                content += `
                    <button id="dartsuite-panel-start" title="${btnTitle}"
                        style="width:100%;padding:8px;border-radius:4px;border:none;background:${btnColor};color:#fff;font-weight:bold;font-size:13px;margin-bottom:8px">
                        ▶ Match starten: ${escapeHtml(home)} vs ${escapeHtml(away)}
                    </button>
                `;
            }
        }
    } else {
        // Quick-config: tournament code + board selection
        content += `
            <div style="color:#ff9800;font-size:12px;margin-bottom:10px">Kein Turnier aktiv. Schnell-Konfiguration:</div>
            <div style="margin-bottom:8px">
                <label style="font-size:11px;color:#aaa;display:block;margin-bottom:3px">Turnier-Code (3-stellig)</label>
                <input id="dartsuite-quick-code" placeholder="z.B. 3ER" maxlength="3"
                    style="width:100%;padding:7px 8px;border-radius:4px;border:1px solid #333;background:#0f3460;color:#e0e0e0;font-size:16px;font-family:monospace;text-transform:uppercase;letter-spacing:4px;text-align:center" />
            </div>
            <div id="dartsuite-quick-board-section" style="display:none;margin-bottom:8px">
                <label style="font-size:11px;color:#aaa;display:block;margin-bottom:3px">Board auswählen</label>
                <select id="dartsuite-quick-board" style="width:100%;padding:7px 8px;border-radius:4px;border:1px solid #333;background:#0f3460;color:#e0e0e0;font-size:13px">
                    <option value="">Board wählen...</option>
                </select>
            </div>
            <div id="dartsuite-quick-status" style="font-size:11px;min-height:16px;margin-bottom:8px"></div>
            <button id="dartsuite-quick-join" disabled style="width:100%;padding:8px;border-radius:4px;border:none;background:#e94560;color:#fff;font-weight:bold;font-size:13px;cursor:pointer;opacity:0.5">
                Am Turnier teilnehmen
            </button>
        `;
    }

    const pageState = getPageState();
    if (pageState.matchId) {
        content += `<div style="font-size:12px;color:#aaa;margin-bottom:8px;margin-top:8px">Match: ${pageState.matchId.substring(0, 8)}...</div>`;
    }
    if (pageState.lobbyId) {
        content += `<div style="font-size:12px;color:#aaa;margin-bottom:8px">Lobby: ${pageState.lobbyId.substring(0, 8)}...</div>`;
    }

    content += `
        <div id="dartsuite-panel-log-footer" style="margin-top:10px;border-top:1px solid #333;padding-top:8px;height:18px;overflow:hidden;white-space:nowrap;color:#9ecfff;font-size:11px"></div>
        </div>
    `;

    panel.innerHTML = content;
    document.body.appendChild(panel);

    // Restore saved panel position
    try {
        const pos = await chrome.storage.local.get(["panelX", "panelY"]);
        if (typeof pos.panelX === "number" && typeof pos.panelY === "number") {
            panel.style.right = "auto";
            panel.style.left = Math.max(0, Math.min(window.innerWidth - 360, pos.panelX)) + "px";
            panel.style.top = Math.max(0, Math.min(window.innerHeight - 50, pos.panelY)) + "px";
        }
    } catch { /* silent */ }

    makePanelDraggable(panel);

    document.getElementById("dartsuite-panel-close")?.addEventListener("click", () => panel.remove());
    document.getElementById("dartsuite-panel-minimize")?.addEventListener("click", () => {
        const panelEl = document.getElementById("dartsuite-panel");
        if (!panelEl) return;
        if (panelEl.dataset.minimized === "true") {
            restoreDartSuitePanel();
        } else {
            minimizeDartSuitePanel();
        }
    });

    // Wire up quick-config if not managed
    if (!managedState.active) {
        setupQuickConfig();
    } else {
        loadUpcomingMatchesForPanel();
        document.getElementById("dartsuite-panel-start")?.addEventListener("click", () => {
            requestStartNextMatch();
        });
    }

    renderPanelStartHint();
}

function ensurePanelOpen() {
    if (!dartsuiteEnabled) return null;

    let panel = document.getElementById("dartsuite-panel");
    if (!panel) {
        toggleDartSuitePanel();
        panel = document.getElementById("dartsuite-panel");
    }

    return panel;
}

function closeDartSuitePanel() {
    const panel = document.getElementById("dartsuite-panel");
    if (panel) {
        panel.remove();
    }
}

function minimizeDartSuitePanel() {
    const panel = document.getElementById("dartsuite-panel");
    if (!panel) return;
    const body = panel.querySelector("#dartsuite-panel-body");
    const minBtn = panel.querySelector("#dartsuite-panel-minimize");
    if (body) body.style.display = "none";
    if (minBtn) { minBtn.textContent = "□"; minBtn.title = "Maximieren"; }
    panel.dataset.minimized = "true";
    panel.style.width = "220px";
}

function restoreDartSuitePanel() {
    const panel = document.getElementById("dartsuite-panel");
    if (!panel) return;
    const body = panel.querySelector("#dartsuite-panel-body");
    const minBtn = panel.querySelector("#dartsuite-panel-minimize");
    if (body) body.style.display = "";
    if (minBtn) { minBtn.textContent = "─"; minBtn.title = "Minimieren"; }
    delete panel.dataset.minimized;
    panel.style.width = "360px";
}

function makePanelDraggable(panel) {
    const header = panel.querySelector("#dartsuite-panel-header");
    if (!header) return;

    let isDragging = false;
    let startX, startY, startLeft, startTop;

    header.addEventListener("mousedown", (e) => {
        if (e.target.closest("button")) return;
        isDragging = true;
        startX = e.clientX;
        startY = e.clientY;
        const rect = panel.getBoundingClientRect();
        startLeft = rect.left;
        startTop = rect.top;
        panel.style.right = "auto";
        panel.style.left = startLeft + "px";
        panel.style.top = startTop + "px";

        const onMouseMove = (e) => {
            if (!isDragging) return;
            const dx = e.clientX - startX;
            const dy = e.clientY - startY;
            const newLeft = Math.max(0, Math.min(window.innerWidth - panel.offsetWidth, startLeft + dx));
            const newTop = Math.max(0, Math.min(window.innerHeight - 50, startTop + dy));
            panel.style.left = newLeft + "px";
            panel.style.top = newTop + "px";
        };

        const onMouseUp = async () => {
            if (!isDragging) return;
            isDragging = false;
            document.removeEventListener("mousemove", onMouseMove);
            document.removeEventListener("mouseup", onMouseUp);
            const rect = panel.getBoundingClientRect();
            try {
                await chrome.storage.local.set({ panelX: rect.left, panelY: rect.top });
            } catch { /* silent */ }
        };

        document.addEventListener("mousemove", onMouseMove);
        document.addEventListener("mouseup", onMouseUp);
        e.preventDefault();
    });
}

async function clickStartGameButton() {
    await sleep(800);
    const buttons = document.querySelectorAll("button.chakra-button, button");
    for (const btn of buttons) {
        if (btn.disabled) continue;
        const text = (btn.textContent || "").trim();
        if (/start/i.test(text)) {
            btn.click();
            console.log("DartSuite: Auto-clicked start game button:", text);
            return true;
        }
    }
    console.warn("DartSuite: Start game button not found");
    return false;
}

// ─── Info Bar (managed mode) ───

function injectInfoBar() {
    if (!managedState.active || !dartsuiteEnabled) return;
    if (!shouldShowInfoBar()) return;
    if (document.getElementById("dartsuite-info-bar")) {
        updateInfoBarManagedContext();
        updateInfoBarSchedule();
        updateInfoBarStatusTag();
        return;
    }

    const bar = document.createElement("div");
    bar.id = "dartsuite-info-bar";
    bar.style.cssText = `
        position: fixed; bottom: 0; left: 0; right: 0;
        background: linear-gradient(135deg, #0f3460, #16213e);
        border-top: 2px solid #e94560;
        padding: 8px 16px; display: flex; align-items: center;
        justify-content: space-between; z-index: 9999;
        font-family: 'Segoe UI', Arial, sans-serif; font-size: 13px; color: #e0e0e0;
    `;
    const boardLabel = managedState.boardName
        ? ` | Board: ${escapeHtml(managedState.boardName)}`
        : ' | <span style="color:#ff9800">⚠ Kein Board gewählt</span>';
    const tournamentLabel = managedState.tournamentName || selectedTournamentContext.tournamentName || "Kein Turnier";
    const hostLabel = managedState.host || selectedTournamentContext.host || "";
    bar.innerHTML = `
        <div style="display:flex;align-items:center;gap:8px;flex-wrap:wrap">
            <span style="color:#4caf50">●</span>
            <strong>DartSuite</strong> — <span id="dartsuite-info-tournament">${escapeHtml(tournamentLabel)}</span>
            <span id="dartsuite-info-meta" style="color:#aaa;font-size:11px">| Host: ${escapeHtml(hostLabel)}${boardLabel}</span>
            <span id="dartsuite-status-tag" style="padding:2px 6px;border-radius:999px;font-size:10px;font-weight:bold;background:#333;color:#fff">STATUS</span>
            <span class="dartsuite-schedule-info" style="margin-left:8px;color:#ff9800;font-size:12px"></span>
            <span id="dartsuite-info-log" style="margin-left:8px;color:#9ecfff;font-size:11px;max-width:42vw;overflow:hidden;text-overflow:ellipsis;white-space:nowrap"></span>
        </div>
        <button id="dartsuite-leave-btn" style="background:#c62828;border:none;color:#fff;padding:4px 12px;border-radius:4px;cursor:pointer;font-size:12px">
            Verlassen
        </button>
    `;
    document.body.appendChild(bar);
    document.body.style.paddingBottom = "42px";

    document.getElementById("dartsuite-leave-btn")?.addEventListener("click", () => {
        setManagedMode({ mode: "Manual" });
        chrome.runtime.sendMessage({ action: "managedModeChanged", boardId: managedState.boardId, mode: "Manual" });
    });

    // Start schedule polling
    startSchedulePolling();
    updateInfoBarStatusTag();
}

function removeInfoBar() {
    const bar = document.getElementById("dartsuite-info-bar");
    if (bar) {
        bar.remove();
        document.body.style.paddingBottom = "";
    }
    stopSchedulePolling();
}

function updateInfoBarStatusTag() {
    const tag = document.getElementById("dartsuite-status-tag");
    if (!tag) return;

    // DST connection status
    const dstLabel = currentDstStatus === "connected" ? "VERBUNDEN"
        : currentDstStatus === "ready" ? "BEREIT"
        : "OFFLINE";
    const dstColors = {
        connected: "#2e7d32",
        ready: "#ff9800",
        offline: "#c62828"
    };

    // Match status overlay (shown when there is an active match context)
    const matchLabels = {
        available:      "WARTEN",
        idle:           "WARTEN",
        scheduled:      "GEPLANT",
        waitForPlayer:  "WARTEN",
        waitForMatch:   "WARTEN",
        playing:        "AKTIV",
        listening:      "LISTENER",
        ended:          "BEENDET"
    };
    const matchColors = {
        available:      "#7b1fa2",
        idle:           "#7b1fa2",
        scheduled:      "#1565c0",
        waitForPlayer:  "#7b1fa2",
        waitForMatch:   "#7b1fa2",
        playing:        "#e65100",
        listening:      "#00695c",
        ended:          "#37474f"
    };

    const matchLabel = matchLabels[currentMatchStatus];
    if (matchLabel) {
        // Show combined tag: e.g. "VERBUNDEN · WARTEN"
        tag.textContent = `${dstLabel} · ${matchLabel}`;
        tag.style.background = matchColors[currentMatchStatus];
        tag.title = `DST: ${dstLabel} | Match: ${matchLabel}`;
    } else {
        tag.textContent = dstLabel;
        tag.style.background = dstColors[currentDstStatus] || "#333";
        tag.title = `DST: ${dstLabel}`;
    }
    tag.style.color = "#fff";
}

function shouldShowInfoBar() {
    if (statusBarMode === "off") return false;
    if (statusBarMode === "hideDuringMatch" && isMatchActive()) return false;
    return true;
}

function refreshInfoBarVisibility() {
    if (!managedState.active || !dartsuiteEnabled) return;
    if (shouldShowInfoBar()) {
        injectInfoBar();
    } else {
        removeInfoBar();
    }
}

function isMatchActive() {
    return ["playing", "waitForPlayer", "waitForMatch"].includes(currentMatchStatus);
}

async function loadStatusBarSettings() {
    try {
        const stored = await chrome.storage.sync.get({ statusBarMode: "always" });
        statusBarMode = stored.statusBarMode || "always";
    } catch { /* silent */ }
}

async function loadDebugModeSettings() {
    try {
        const stored = await chrome.storage.sync.get({ debugMode: false });
        debugModeEnabled = stored.debugMode === true;
    } catch { /* silent */ }
}

function showStatusToast(dst, match) {
    const colors = {
        connected: "#1b5e20",
        ready: "#e65100",
        offline: "#b71c1c"
    };
    const label = dst === "connected" ? "Verbunden" : dst === "ready" ? "Bereit" : "Offline";
    const matchLabel = match && match !== "available" ? ` | ${match}` : "";
    const tournamentName = managedState.tournamentName || selectedTournamentContext.tournamentName;
    const boardName = managedState.boardName || "";
    const contextLabel = tournamentName
        ? ` | Turnier: ${tournamentName}${boardName ? ` | Board: ${boardName}` : ""}`
        : "";
    showToastMessage(`Status: ${label}${matchLabel}${contextLabel}`, colors[dst] || "#333", 3200);
}

function updateInfoBarManagedContext() {
    const bar = document.getElementById("dartsuite-info-bar");
    if (!bar) return;
    const tournamentSpan = bar.querySelector("#dartsuite-info-tournament");
    const hostSpan = bar.querySelector("#dartsuite-info-meta");
    if (!hostSpan || !tournamentSpan) return;

    const tournamentName = managedState.tournamentName || selectedTournamentContext.tournamentName || "Kein Turnier";
    const hostName = managedState.host || selectedTournamentContext.host || "";
    const boardLabel = managedState.boardName
        ? ` | Board: ${escapeHtml(managedState.boardName)}`
        : ' | <span style="color:#ff9800">⚠ Kein Board gewählt</span>';
    tournamentSpan.textContent = tournamentName;
    hostSpan.innerHTML = `| Host: ${escapeHtml(hostName)}${boardLabel}`;
}

function showManagedContextToast(reason) {
    if (!debugModeEnabled) return;
    const tournamentName = managedState.tournamentName || selectedTournamentContext.tournamentName || "Kein Turnier";
    const boardName = managedState.boardName || "";
    const msg = `${reason}: ${tournamentName}${boardName ? ` | Board: ${boardName}` : ""}`;

    const key = `${reason}|${tournamentName}|${boardName}`;
    const now = Date.now();
    if (key === lastContextToastKey && now - lastContextToastAtMs < 1200) {
        return;
    }
    lastContextToastKey = key;
    lastContextToastAtMs = now;

    showToastMessage(msg, "#0f3460", 2600);
}

function showToastMessage(text, background, durationMs, options = {}) {
    if (!dartsuiteEnabled) return;

    const routeToManagedSurface = options.routeToManagedSurface !== false;
    if (routeToManagedSurface && managedState.active) {
        routeManagedLog(text, durationMs);
        return;
    }

    let toast = document.getElementById("dartsuite-status-toast");
    if (!toast) {
        toast = document.createElement("div");
        toast.id = "dartsuite-status-toast";
        toast.style.cssText = "position:fixed;right:16px;bottom:70px;z-index:10000;padding:8px 10px;border-radius:6px;color:#fff;font-size:12px;font-family:'Segoe UI',Arial,sans-serif;box-shadow:0 4px 16px rgba(0,0,0,0.4);opacity:0;transition:opacity 0.2s";
        document.body.appendChild(toast);
    }

    toast.textContent = text;
    toast.style.background = background || "#333";
    toast.style.opacity = "1";
    clearTimeout(toast._hideTimer);
    toast._hideTimer = setTimeout(() => { toast.style.opacity = "0"; }, durationMs || 3000);
}

let infoBarLogTimer = null;

function routeManagedLog(text, durationMs) {
    if (!dartsuiteEnabled) return;

    if (shouldShowInfoBar() && !document.getElementById("dartsuite-info-bar")) {
        injectInfoBar();
    }

    const bar = document.getElementById("dartsuite-info-bar");
    if (bar && shouldShowInfoBar()) {
        const logEl = bar.querySelector("#dartsuite-info-log");
        if (logEl) {
            logEl.textContent = text;
            clearTimeout(infoBarLogTimer);
            infoBarLogTimer = setTimeout(() => {
                if (logEl.textContent === text) {
                    logEl.textContent = "";
                }
            }, durationMs || 3200);
            return;
        }
    }

    const panel = ensurePanelOpen();
    if (!panel) return;

    const footer = panel.querySelector("#dartsuite-panel-log-footer");
    if (!footer) return;

    const safeText = escapeHtml(text);
    const durationSeconds = Math.max(10, Math.min(24, Math.ceil((text.length || 20) / 3)));
    footer.innerHTML = `<span style="display:inline-block;transform:translateX(100%);animation:dartsuitePanelLogScroll ${durationSeconds}s linear 1;white-space:nowrap">${safeText}</span>`;

    ensurePanelLogStyle();
}

function modeAbbreviation(mode, type) {
    const normalized = String(mode || "").toLowerCase();
    if (type === "in") {
        if (normalized === "double") return "DI";
        if (normalized === "master") return "MI";
        return "SI";
    }

    if (normalized === "double") return "DO";
    if (normalized === "master") return "MO";
    return "SO";
}

function buildStartNotice(payload) {
    const info = payload?.commandInfo || {};
    const initiatorName = (info.initiatorName || "").trim() || "DartSuite";
    const commandLabel = (info.commandLabel || "").trim() || "das Match";
    const players = Array.isArray(payload?.players)
        ? payload.players.map(p => (p?.name || "").trim()).filter(Boolean)
        : [];
    const playerLine = players.length >= 2
        ? `${players[0]} vs ${players[1]}`
        : players.length === 1
            ? players[0]
            : "Spieler werden vorbereitet";

    const baseScore = payload?.settings?.baseScore || 501;
    const inMode = modeAbbreviation(payload?.settings?.inMode || "Straight", "in");
    const outMode = modeAbbreviation(payload?.settings?.outMode || "Double", "out");
    const gameMode = payload?.gameMode === "Sets" ? "Sets" : "Legs";
    const firstTo = payload?.gameMode === "Sets" ? (payload?.sets || 3) : (payload?.legs || 3);
    const gameplayLine = `${baseScore} ${inMode} ${outMode} First to ${firstTo} ${gameMode}`;

    return {
        headline: `${initiatorName} hat ${commandLabel} gestartet!`,
        gameplayLine,
        playerLine
    };
}

function renderPanelStartHint() {
    const panelHint = document.getElementById("dartsuite-panel-start-hint");
    if (!panelHint) return;

    if (!activeStartNotice) {
        panelHint.style.display = "none";
        panelHint.innerHTML = "";
        return;
    }

    panelHint.style.display = "block";
    panelHint.innerHTML = `
        <div style="font-size:15px;font-weight:700;line-height:1.3">${escapeHtml(activeStartNotice.headline)}</div>
        <div style="font-size:13px;color:#ffd180;margin-top:4px">${escapeHtml(activeStartNotice.gameplayLine)}</div>
        <div style="font-size:12px;color:#fff8e1;margin-top:2px">${escapeHtml(activeStartNotice.playerLine)}</div>
    `;
}

function showStartExecutionOverlay(payload) {
    if (!dartsuiteEnabled) return;

    activeStartNotice = buildStartNotice(payload);
    ensurePanelOpen();
    renderPanelStartHint();

    let overlay = document.getElementById("dartsuite-start-overlay");
    if (!overlay) {
        overlay = document.createElement("div");
        overlay.id = "dartsuite-start-overlay";
        overlay.style.cssText = `
            position: fixed; inset: 0; z-index: 2147483646;
            background: rgba(7, 14, 28, 0.88);
            backdrop-filter: blur(2px);
            display: flex; align-items: center; justify-content: center;
            padding: 24px; cursor: pointer;
        `;
        overlay.innerHTML = `
            <div style="max-width:860px;width:min(92vw,860px);background:#101a2e;border:2px solid #e94560;border-radius:14px;box-shadow:0 16px 48px rgba(0,0,0,0.55);padding:28px;text-align:center;color:#fff;font-family:'Segoe UI',Arial,sans-serif">
                <div style="font-size:18px;letter-spacing:0.08em;color:#ffb4c1;margin-bottom:10px">DARTSUITE STEUERUNG AKTIV</div>
                <div id="dartsuite-start-headline" style="font-size:44px;line-height:1.15;font-weight:800;margin-bottom:14px"></div>
                <div id="dartsuite-start-gameplay" style="font-size:28px;line-height:1.2;color:#9ecfff;margin-bottom:10px"></div>
                <div id="dartsuite-start-players" style="font-size:34px;line-height:1.15;color:#f7f7f7"></div>
                <div style="margin-top:16px;font-size:14px;color:#b0bec5">Bitte waehrend der Vorbereitung keine manuellen Aktionen in Autodarts ausfuehren.</div>
            </div>
        `;
        overlay.addEventListener("click", () => {
            hideStartExecutionOverlay();
        });
        document.body.appendChild(overlay);
    }

    overlay.style.display = "flex";

    const headline = document.getElementById("dartsuite-start-headline");
    const gameplay = document.getElementById("dartsuite-start-gameplay");
    const players = document.getElementById("dartsuite-start-players");
    if (headline) headline.textContent = activeStartNotice.headline;
    if (gameplay) gameplay.textContent = activeStartNotice.gameplayLine;
    if (players) players.textContent = activeStartNotice.playerLine;
}

function hideStartExecutionOverlay() {
    const overlay = document.getElementById("dartsuite-start-overlay");
    if (overlay) {
        overlay.remove();
    }

    activeStartNotice = null;
    renderPanelStartHint();
}

function ensurePanelLogStyle() {
    if (document.getElementById("dartsuite-panel-log-style")) return;

    const style = document.createElement("style");
    style.id = "dartsuite-panel-log-style";
    style.textContent = `
        @keyframes dartsuitePanelLogScroll {
            0% { transform: translateX(100%); }
            100% { transform: translateX(-100%); }
        }
    `;
    document.head.appendChild(style);
}

// ─── Schedule Polling ───

let schedulePollInterval = null;
let lastScheduleInfo = null;

function startSchedulePolling() {
    stopSchedulePolling();
    if (!managedState.active || !managedState.boardId || !managedState.tournamentId) return;

    // Poll immediately, then every 15 seconds
    pollSchedule();
    schedulePollInterval = setInterval(pollSchedule, 15000);
}

function stopSchedulePolling() {
    if (schedulePollInterval) {
        clearInterval(schedulePollInterval);
        schedulePollInterval = null;
    }
}

async function pollSchedule() {
    if (!managedState.active || !managedState.boardId || !managedState.tournamentId) return;
    const apiBaseUrl = await getApiBaseUrl();
    try {
        const result = await apiFetch(`${apiBaseUrl}/api/matches/${managedState.tournamentId}`, { method: "GET" });
        if (!result?.ok || !Array.isArray(result?.body)) {
            apiReachable = false;
            lastScheduleInfo = null;
            updateInfoBarSchedule();
            return;
        }

        apiReachable = true;
        const allMatches = result.body;
        const boardMatches = allMatches.filter(m => m.boardId === managedState.boardId && !m.finishedUtc)
            .sort((a, b) => (a.plannedStartUtc || "").localeCompare(b.plannedStartUtc || ""));

        // Cache participants for name resolution (refresh every 60s)
        if (Date.now() - participantsCacheTime > 60000) {
            try {
                const pResult = await apiFetch(`${apiBaseUrl}/api/tournaments/${managedState.tournamentId}/participants`, { method: "GET" });
                if (pResult?.ok && Array.isArray(pResult?.body)) {
                    cachedParticipants = pResult.body;
                    participantsCacheTime = Date.now();
                }
            } catch { /* silent */ }
        }

        const next = boardMatches[0];
        lastScheduleInfo = next || null;
        updateInfoBarSchedule();
    } catch {
        apiReachable = false;
        lastScheduleInfo = null;
        updateInfoBarSchedule();
    }
}

function resolveParticipantName(id) {
    if (!id) return "Spieler";
    const p = cachedParticipants.find(p => p.id === id);
    const name = p?.displayName || p?.accountName || "Spieler";
    return name.toUpperCase();
}

function updateInfoBarSchedule() {
    const bar = document.getElementById("dartsuite-info-bar");
    if (!bar) return;
    const infoSpan = bar.querySelector(".dartsuite-schedule-info");
    if (!infoSpan) return;

    if (!lastScheduleInfo) {
        infoSpan.innerHTML = apiReachable ? "Keine anstehenden Matches" : "API nicht erreichbar";
        return;
    }

    const m = lastScheduleInfo;
    const time = m.plannedStartUtc
        ? new Date(m.plannedStartUtc).toLocaleTimeString("de-DE", { hour: "2-digit", minute: "2-digit" })
        : "—";
    const home = resolveParticipantName(m.homeParticipantId);
    const away = resolveParticipantName(m.awayParticipantId);
    const boardConnected = isBoardConnected();
    const boardHasActiveMatch = hasActiveAutodartsMatch();
    const isWaiting = currentMatchStatus === "waitForPlayer" || currentMatchStatus === "waitForMatch";
    const canStart = apiReachable && !isMatchActive() && !boardHasActiveMatch;
    const playBtnColor = boardConnected ? "#4caf50" : "#ff9800";
    const playBtnTitle = boardConnected
        ? "Match starten (Board verbunden)"
        : "Match starten (API erreichbar, Board nicht verbunden)";
    const playBtn = isWaiting
        ? `<button class="dartsuite-pause-btn" style="background:#9c27b0;border:none;color:#fff;padding:2px 8px;border-radius:4px;cursor:default;font-size:12px;margin-left:6px" title="Warte auf Spieler / Match-Start" disabled>⏸</button>`
        : canStart
        ? `<button class="dartsuite-play-btn" style="background:${playBtnColor};border:none;color:#fff;padding:2px 8px;border-radius:4px;cursor:pointer;font-size:12px;margin-left:6px" title="${playBtnTitle}">▶</button>`
        : "";
    infoSpan.innerHTML = `Nächstes Match: ${time} — ${escapeHtml(home)} vs ${escapeHtml(away)}${playBtn}`;

    // Attach click handler for play button
    const btn = infoSpan.querySelector(".dartsuite-play-btn");
    if (btn) {
        btn.addEventListener("click", (e) => {
            e.stopPropagation();
            requestStartNextMatch();
        });
    }
}

function requestStartNextMatch() {
    if (!dartsuiteEnabled || !managedState.active || !managedState.boardId || !managedState.tournamentId) return;
    const payload = {
        action: "requestNextMatch",
        tournamentId: managedState.tournamentId,
        boardId: managedState.boardId
    };
    logTraffic("OUT", "requestNextMatch", payload);
    chrome.runtime.sendMessage({
        action: "requestNextMatch",
        tournamentId: managedState.tournamentId,
        boardId: managedState.boardId
    });
}

function updatePlayButtons() {
    // Re-render info bar schedule to reflect current board status
    updateInfoBarSchedule();
    // Re-render panel upcoming matches if panel is open
    const panelUpcoming = document.getElementById("dartsuite-panel-upcoming");
    if (panelUpcoming) {
        loadUpcomingMatchesForPanel();
    }
}

function isBoardFree() {
    if (!externalBoardId) return false;
    const adBoard = capturedBoards.find(b => b.id === externalBoardId);
    // matchId is null when no match is running on the Autodarts board
    return adBoard ? !adBoard.matchId : false;
}

function hasActiveAutodartsMatch() {
    if (!externalBoardId) return false;
    const adBoard = capturedBoards.find(b => b.id === externalBoardId);
    return !!adBoard?.matchId;
}

function isBoardConnected() {
    if (!externalBoardId) return false;
    const adBoard = capturedBoards.find(b => b.id === externalBoardId);
    return !!adBoard?.state?.connected;
}

// ─── Prepare Match (Lobby Automation) ───

async function handlePrepareMatch(payload) {
    if (!payload) return;

    // Safety guard: never interrupt an active match (e.g. after SW restart with stale state).
    if (location.pathname.match(/^\/matches\/[\w-]+/)) {
        console.log("DartSuite: prepareMatch aborted — active match in progress at", location.pathname);
        return;
    }

    showStartExecutionOverlay(payload);

    // Guard: prevent re-entry while lobby preparation is in progress
    if (lobbyPrepareActive) {
        console.log("DartSuite: Lobby preparation already in progress, skipping");
        return;
    }
    lobbyPrepareActive = true;
    showToastMessage("Match wird erstellt...", "#0f3460", 60000);
    console.log("DartSuite: Prepare match", payload);

    matchStartTime = new Date();
    currentMatchPlayers = payload.players || [];

    try {
        // Step 0: ALWAYS navigate via homescreen first.
        // This ensures the board is in a clean state, even if still on match stats from a previous game.
        const homescreenUrl = "https://play.autodarts.io/";
        if (location.pathname !== "/" && !location.pathname.startsWith("/lobbies/new")) {
            console.log("DartSuite: Navigating to homescreen first");
            location.href = homescreenUrl;
            await chrome.storage.sync.set({ pendingPrepareMatch: JSON.stringify({ ...payload, _savedAt: Date.now() }) });
            return; // lobbyPrepareActive stays true — will be reset on next page load
        }

        // If we're on the homescreen, navigate to lobby creation
        if (location.pathname === "/" || location.pathname === "") {
            console.log("DartSuite: On homescreen, navigating to lobby creation");
            location.href = "https://play.autodarts.io/lobbies/new/x01";
            await chrome.storage.sync.set({ pendingPrepareMatch: JSON.stringify({ ...payload, _savedAt: Date.now() }) });
            return;
        }

        // Notify background about match status transition
        chrome.runtime.sendMessage({ action: "setMatchStatus", status: "waitForPlayer" });

        // If we're on the lobby creation page (/lobbies/new/...), configure and open
        if (location.pathname.startsWith("/lobbies/new")) {
            await sleep(1500);

            // Step 2: Apply gameplay settings (includes setting lobby to private)
            await applyGameplaySettings(payload);

            // Step 3: Open the lobby
            await sleep(500);
            const opened = await clickButtonByText("Lobby öffnen") || await clickButtonByText("Open Lobby");
            if (!opened) {
                console.warn("DartSuite: Could not find 'Lobby öffnen' button");
            }

            // Step 4: Wait for navigation to the lobby page (/lobbies/{id})
            await waitForLobbyNavigation();
            showToastMessage("Lobby erstellt. Warte auf Spieler...", "#1b5e20", 5000);
            minimizeDartSuitePanel();
            hideStartExecutionOverlay(); // Release scroll lock — user may need to interact with lobby
        }

        // Now we're on the lobby page — continue with lobby management
        await sleep(1500);

        // Step 5: Add local (non-Autodarts) players before showing QR code
        const localPlayers = (payload.players || []).filter(p => p.isAutodarts === false);
        if (localPlayers.length > 0) {
            await addLocalPlayersToLobby(localPlayers);
            await sleep(500);
        }

        // Step 6: Capture pre-existing lobby players (host + local players) before showing QR code
        const preExistingPlayers = getLobbyPlayerNames();

        // Step 7: Show QR code (only if there are Autodarts players who need to scan)
        const autodartsPlayers = (payload.players || []).filter(p => p.isAutodarts !== false);
        if (autodartsPlayers.length > 0) {
            // Do not overlap fullscreen execution overlay with QR routines.
            hideStartExecutionOverlay();
            await sleep(500);
            await clickButtonByAriaLabel("Show QR code");
            // Lobby created, waiting for players to join
            chrome.runtime.sendMessage({ action: "setMatchStatus", status: "waitForPlayer" });
        }

        // Step 8: Inject match info into QR dialog & monitor for players
        await sleep(500);
        if (autodartsPlayers.length > 0) {
            injectQrMatchInfo(payload);
            startLobbyPlayerMonitor(payload, preExistingPlayers);
        } else {
            hideStartExecutionOverlay();
            // No Autodarts players — remove the host immediately since only local players are in the lobby
            const expectedPlayers = (payload.players || []).map(p => (p.name || "").toLowerCase());
            await removeNonMatchPlayers(expectedPlayers);
        }

        // Step 9: Handle fullscreen
        const settings = await chrome.storage.sync.get({ fullscreen: false });
        if (settings.fullscreen) {
            try { await document.documentElement.requestFullscreen(); } catch { /* ignore */ }
        }

        console.log("DartSuite: Match prepared, waiting for players");
    } catch (error) {
        console.warn("DartSuite: Prepare match failed", error);
    } finally {
        hideStartExecutionOverlay();
        lobbyPrepareActive = false;
    }
}

async function waitForLobbyNavigation() {
    // Wait up to 10 seconds for URL to change from /lobbies/new/... to /lobbies/{id}
    for (let i = 0; i < 20; i++) {
        await sleep(500);
        if (location.pathname.match(/^\/lobbies\/[\w-]+$/) && !location.pathname.startsWith("/lobbies/new")) {
            console.log("DartSuite: Lobby opened, navigated to", location.pathname);
            return;
        }
    }
    console.warn("DartSuite: Timeout waiting for lobby navigation");
}

async function addLocalPlayersToLobby(localPlayers) {
    // Add non-Autodarts players via the local player input form.
    // The Autodarts lobby has: <input placeholder="Enter name for local player"> + <button aria-label="add-player">
    for (const player of localPlayers) {
        const name = player.name || "";
        if (!name) continue;

        // First check if player is already shown as a tag (previously added local player)
        const tags = document.querySelectorAll('span[class*="css-"]');
        let alreadyAdded = false;
        for (const tag of tags) {
            if (tag.textContent?.trim().toLowerCase() === name.toLowerCase()) {
                tag.click();
                alreadyAdded = true;
                console.log(`DartSuite: Selected existing local player tag: ${name}`);
                await sleep(500);
                break;
            }
        }

        if (!alreadyAdded) {
            // Type the name into the local player input
            const input = document.querySelector('input[placeholder*="local player"], input[placeholder*="lokalen Spieler"]');
            if (!input) {
                console.warn("DartSuite: Local player input not found");
                continue;
            }
            // Clear and type the name
            const nativeInputValueSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
            nativeInputValueSetter.call(input, name);
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.dispatchEvent(new Event('change', { bubbles: true }));
            await sleep(300);

            // Click the add button
            const addBtn = document.querySelector('button[aria-label="add-player"], button[aria-label="Spieler hinzufügen"]');
            if (addBtn) {
                addBtn.click();
                console.log(`DartSuite: Added local player: ${name}`);
            } else {
                // Try pressing Enter on the input
                input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
                console.log(`DartSuite: Added local player via Enter: ${name}`);
            }
            await sleep(500);
        }
    }
}

async function removeNonMatchPlayers(expectedPlayerNames) {
    // Remove lobby players whose name is NOT in the expected match players list.
    // This removes the auto-added host after all real players have joined.
    const expectedSet = new Set(expectedPlayerNames);
    let removed = 0;
    for (let attempt = 0; attempt < 10; attempt++) {
        let foundOne = false;
        const rows = document.querySelectorAll('tr');
        for (const row of rows) {
            const deleteBtn = row.querySelector('button[aria-label="Delete player"]');
            if (!deleteBtn) continue;
            const tds = row.querySelectorAll('td');
            if (tds.length < 2) continue;
            const nameEl = tds[1].querySelector('p');
            const name = nameEl?.textContent?.trim()?.toLowerCase();
            if (name && !expectedSet.has(name)) {
                deleteBtn.click();
                removed++;
                foundOne = true;
                await sleep(300);
                break; // DOM changed, restart scan
            }
        }
        if (!foundOne) break;
    }
    if (removed > 0) {
        console.log(`DartSuite: Removed ${removed} non-match player(s) from lobby`);
    }
}

async function clickButtonByAriaLabel(ariaLabel) {
    const btn = document.querySelector(`button[aria-label="${ariaLabel}"]`);
    if (btn) {
        btn.click();
        console.log(`DartSuite: Clicked button [aria-label="${ariaLabel}"]`);
        return true;
    }
    console.warn(`DartSuite: Button [aria-label="${ariaLabel}"] not found`);
    return false;
}

function injectQrMatchInfo(payload) {
    const modal = document.querySelector('.chakra-modal__body, [id*="chakra-modal--body"]');
    if (!modal) {
        console.warn("DartSuite: QR modal not found for match info injection");
        return;
    }

    // Remove any existing match info
    const existing = modal.querySelector("#dartsuite-qr-match-info");
    if (existing) existing.remove();

    const players = payload.players || [];
    const now = new Date();
    let startTimeText = now.toLocaleTimeString("de-DE", { hour: "2-digit", minute: "2-digit" });
    let timingHtml = "";

    if (payload.plannedStartUtc) {
        const planned = new Date(payload.plannedStartUtc);
        startTimeText = planned.toLocaleTimeString("de-DE", { hour: "2-digit", minute: "2-digit" });
        const diffMs = now - planned;

        if (diffMs <= 0) {
            // On time or early
            timingHtml = `<span style="color:#4caf50;font-weight:bold">✓ Im Zeitplan</span>`;
        } else {
            // Late — show delay
            const delayMin = Math.ceil(diffMs / 60000);
            timingHtml = `<span style="color:#f44336;font-weight:bold">⚠ Verspätet (+${delayMin} min)</span>`;
        }
    }

    const infoDiv = document.createElement("div");
    infoDiv.id = "dartsuite-qr-match-info";
    infoDiv.style.cssText = "text-align:center;padding:16px 8px;font-family:'Segoe UI',Arial,sans-serif";
    infoDiv.innerHTML = `
        <div style="margin-bottom:8px">
            ${players.map((p, i) => {
        const sep = i < players.length - 1 ? '<div style="font-size:18px;color:#888;margin:4px 0">vs.</div>' : '';
        const icon = p.isAutodarts === false ? ' <span style="font-size:14px;color:#888" title="Lokal">👤</span>' : '';
        return `<div id="dartsuite-qr-player-${i}" style="font-size:28px;font-weight:bold;color:#e0e0e0;position:relative;display:inline-block;padding:4px 8px">${escapeHtml((p.name || "Spieler").toUpperCase())}${icon}</div>${sep}`;
    }).join("")}
        </div>
        <div style="font-size:14px;color:#aaa;margin-bottom:4px">Startzeit: ${startTimeText}</div>
        ${timingHtml ? `<div style="font-size:13px;margin-bottom:4px">${timingHtml}</div>` : ""}
        <div id="dartsuite-qr-wrong-player" style="display:none;background:#4a0000;border:1px solid #f44336;border-radius:4px;padding:8px;margin-top:10px;font-size:13px;color:#ff8a80"></div>
    `;
    modal.appendChild(infoDiv);
    console.log("DartSuite: Injected match info into QR dialog");
}

let lobbyPlayerMonitorInterval = null;

function startLobbyPlayerMonitor(payload, preExistingPlayers) {
    stopLobbyPlayerMonitor();
    const allPlayers = (payload.players || []);
    const expectedPlayers = allPlayers.map(p => (p.name || "").toLowerCase());
    // Only Autodarts-account players need to scan QR and join the lobby
    const autodartsPlayers = allPlayers.filter(p => p.isAutodarts !== false).map(p => (p.name || "").toLowerCase());
    if (expectedPlayers.length === 0) return;
    // Pre-existing players (e.g., the host) are not flagged as "wrong player"
    const ignoredPlayers = preExistingPlayers || new Set();

    // Immediately mark local (non-Autodarts) players with checkmarks
    for (let i = 0; i < allPlayers.length; i++) {
        if (allPlayers[i].isAutodarts === false) {
            const playerDiv = document.getElementById(`dartsuite-qr-player-${i}`);
            if (playerDiv && !playerDiv.querySelector(".dartsuite-check")) {
                const check = document.createElement("span");
                check.className = "dartsuite-check";
                check.style.cssText = "position:absolute;top:-2px;right:-20px;font-size:22px;color:#4caf50";
                check.textContent = "✓";
                playerDiv.appendChild(check);
                console.log(`DartSuite: Local player pre-checked: ${allPlayers[i].name}`);
            }
        }
    }

    lobbyPlayerMonitorInterval = setInterval(async () => {
        // Find player names currently in the lobby by scanning player slots
        const lobbyPlayers = getLobbyPlayerNames();
        const result = checkAndMarkPlayers(autodartsPlayers, lobbyPlayers, allPlayers, ignoredPlayers);

        if (result.allJoined) {
            console.log("DartSuite: All players joined, closing QR dialog");
            stopLobbyPlayerMonitor();
            // All players in lobby, waiting for match to start
            chrome.runtime.sendMessage({ action: "setMatchStatus", status: "waitForMatch" });
            // Close the QR modal
            const closeBtn = document.querySelector('.chakra-modal__close-btn, button[aria-label="Close"]');
            if (closeBtn) closeBtn.click();
            // Remove players not part of this match (e.g., the auto-added host)
            await sleep(500);
            await removeNonMatchPlayers(expectedPlayers);
            hideStartExecutionOverlay();
            // Auto-click "Spiel starten" button
            await clickStartGameButton();
        }
    }, 2000);
}

function stopLobbyPlayerMonitor() {
    if (lobbyPlayerMonitorInterval) {
        clearInterval(lobbyPlayerMonitorInterval);
        lobbyPlayerMonitorInterval = null;
    }
}

function getLobbyPlayerNames() {
    // Scan lobby table rows that contain a delete-player button.
    // The player name sits in the second <td> as a <p> element.
    const names = new Set();
    const rows = document.querySelectorAll('tr');
    for (const row of rows) {
        if (!row.querySelector('button[aria-label="Delete player"]')) continue;
        const tds = row.querySelectorAll('td');
        if (tds.length < 2) continue;
        const nameEl = tds[1].querySelector('p');
        const text = nameEl?.textContent?.trim();
        if (text && text.length > 1) {
            names.add(text.toLowerCase());
        }
    }
    return names;
}

function checkAndMarkPlayers(expectedPlayers, lobbyPlayers, playersArray, ignoredPlayers) {
    let allJoined = true;
    const expectedSet = new Set(expectedPlayers);
    const ignoreSet = ignoredPlayers || new Set();

    for (let i = 0; i < expectedPlayers.length; i++) {
        const playerName = expectedPlayers[i];
        const joined = lobbyPlayers.has(playerName);
        if (!joined) allJoined = false;

        // Update checkmark overlay in QR dialog
        const playerDiv = document.getElementById(`dartsuite-qr-player-${i}`);
        if (playerDiv) {
            let check = playerDiv.querySelector(".dartsuite-check");
            if (joined && !check) {
                check = document.createElement("span");
                check.className = "dartsuite-check";
                check.style.cssText = "position:absolute;top:-2px;right:-20px;font-size:22px;color:#4caf50";
                check.textContent = "✓";
                playerDiv.appendChild(check);
                console.log(`DartSuite: Player joined: ${playerName}`);
            } else if (!joined && check) {
                check.remove();
            }
        }
    }

    // Check for unexpected players in the lobby (ignore pre-existing ones like the host)
    const wrongPlayers = [];
    for (const name of lobbyPlayers) {
        if (!expectedSet.has(name) && !ignoreSet.has(name)) {
            wrongPlayers.push(name);
        }
    }
    const wrongDiv = document.getElementById("dartsuite-qr-wrong-player");
    if (wrongDiv) {
        if (wrongPlayers.length > 0) {
            wrongDiv.style.display = "block";
            wrongDiv.innerHTML = `⚠ Falsches Match? <strong>${wrongPlayers.map(n => escapeHtml(n)).join(", ")}</strong> ist nicht in diesem Match.`;
        } else {
            wrongDiv.style.display = "none";
        }
    }

    return { allJoined };
}

async function applyGameplaySettings(payload) {
    const settings = payload.settings || {};

    // Base Score (Startpunkte / Base score)
    if (settings.baseScore) {
        await clickSettingButton("Startpunkte", String(settings.baseScore));
        await sleep(200);
    }

    // In mode
    if (settings.inMode) {
        await clickSettingButton("In mode", settings.inMode);
        await sleep(200);
    }

    // Out mode
    if (settings.outMode) {
        await clickSettingButton("Out mode", settings.outMode);
        await sleep(200);
    }

    // Max Rounds (Max Runden / Max rounds)
    if (settings.maxRounds) {
        await clickSettingButton("Max Runden", String(settings.maxRounds));
        await sleep(200);
    }

    // Bull Mode
    if (settings.bullMode) {
        await clickSettingButton("Bull Mode", settings.bullMode);
        await sleep(200);
    }

    // Bull-off
    if (payload.bullOffMode !== undefined) {
        await clickSettingButton("Bull-off", payload.bullOffMode);
        await sleep(200);
    }

    // Game mode (Spielmodus / Match mode): Aus/Off, Legs, Sets
    const gameMode = payload.gameMode || "Legs";
    await clickSettingButton("Spielmodus", gameMode);
    await sleep(500);

    // Select leg/set count from dropdown
    const legOrSetCount = gameMode === "Sets" ? (payload.sets || 3) : (payload.legs || 3);
    const selects = document.querySelectorAll("select");
    for (const sel of selects) {
        const option = [...sel.options].find(o => o.value === String(legOrSetCount));
        if (option) {
            sel.value = String(legOrSetCount);
            sel.dispatchEvent(new Event("change", { bubbles: true }));
            break;
        }
    }

    // If Sets mode, also set the legs-per-set dropdown (second select)
    if (gameMode === "Sets" && payload.legs) {
        await sleep(300);
        const allSelects = document.querySelectorAll("select");
        let selectIndex = 0;
        for (const sel of allSelects) {
            const hasLegOption = [...sel.options].find(o => o.value === String(payload.legs));
            if (hasLegOption) {
                selectIndex++;
                if (selectIndex === 2) { // second matching select = legs per set
                    sel.value = String(payload.legs);
                    sel.dispatchEvent(new Event("change", { bubbles: true }));
                    break;
                }
            }
        }
    }
    await sleep(200);

    // Lobby must always be private (Privat / Private)
    await clickSettingButton("Lobby", "Privat");
    await sleep(200);
}

// Click a button within a specific settings group identified by its label text.
// The Autodarts lobby uses <p> labels followed by button groups inside a shared container.
// Supports both German and English UI labels.
async function clickSettingButton(groupLabel, buttonText) {
    // Map of label alternatives (DE/EN) to try
    const labelAliases = {
        "startpunkte": ["Startpunkte", "Base score"],
        "base score": ["Startpunkte", "Base score"],
        "max runden": ["Max Runden", "Max rounds"],
        "max rounds": ["Max Runden", "Max rounds"],
        "spielmodus": ["Spielmodus", "Match mode"],
        "match mode": ["Spielmodus", "Match mode"],
        "lobby": ["Lobby"],
        "bull mode": ["Bull Mode", "Bull mode"],
        "bull-off": ["Bull-off"],
        "in mode": ["In mode"],
        "out mode": ["Out mode"],
    };

    // Map of button text alternatives (DE/EN) to try
    const buttonAliases = {
        "privat": ["Privat", "Private"],
        "private": ["Privat", "Private"],
        "öffentlich": ["Öffentlich", "Public"],
        "public": ["Öffentlich", "Public"],
        "aus": ["Aus", "Off"],
        "off": ["Aus", "Off"],
    };

    const labelsToTry = labelAliases[groupLabel.toLowerCase()] || [groupLabel];
    const buttonsToTry = buttonAliases[buttonText.toLowerCase()] || [buttonText];

    for (const tryLabel of labelsToTry) {
        const labels = document.querySelectorAll("p, span, label, h2, h3");
        for (const label of labels) {
            const labelText = label.textContent?.trim();
            if (!labelText) continue;
            const isMatch = labelText.toLowerCase() === tryLabel.toLowerCase()
                || labelText.toLowerCase().includes(tryLabel.toLowerCase());
            if (!isMatch) continue;

            let container = label.parentElement;
            while (container && container !== document.body) {
                const buttons = container.querySelectorAll('button');
                if (buttons.length > 0) {
                    for (const tryBtn of buttonsToTry) {
                        for (const btn of buttons) {
                            if (btn.textContent?.trim() === tryBtn) {
                                btn.click();
                                console.log(`DartSuite: Clicked "${tryBtn}" in group "${tryLabel}"`);
                                return true;
                            }
                        }
                    }
                    break;
                }
                container = container.parentElement;
            }
        }
    }
    console.warn(`DartSuite: Could not find button "${buttonText}" in group "${groupLabel}"`);
    return false;
}

async function invitePlayers(players) {
    if (!players || players.length === 0) return;
    console.log("DartSuite: Inviting players", players.map(p => p.name));

    // Find the invite/add player section
    for (const player of players) {
        // Look for a search/invite input
        const inputs = document.querySelectorAll("input[placeholder*='Spieler'], input[placeholder*='player'], input[placeholder*='invite'], input[placeholder*='search']");
        for (const input of inputs) {
            input.value = player.name;
            input.dispatchEvent(new Event("input", { bubbles: true }));
            input.dispatchEvent(new Event("change", { bubbles: true }));
            await sleep(500);

            // Click the matching result
            const results = document.querySelectorAll(`[class*="css-"] p, [class*="css-"] span`);
            for (const el of results) {
                if (el.textContent?.trim().toLowerCase() === player.name.toLowerCase()) {
                    el.click();
                    await sleep(300);
                    break;
                }
            }
        }
    }
}

// ─── Upcoming Match Info ───

function handleUpcomingMatch(payload) {
    if (!payload) return;
    console.log("DartSuite: Upcoming match info", payload);

    // Navigate to lobbies page to prepare the match
    if (payload.lobbyUrl) {
        location.href = payload.lobbyUrl;
        return;
    }

    // If we have match preparation data, trigger prepareMatch
    if (payload.players && payload.players.length > 0) {
        handlePrepareMatch(payload);
        return;
    }

    // Fallback: show a notification in the info bar
    const bar = document.getElementById("dartsuite-info-bar");
    if (bar) {
        const infoDiv = bar.querySelector("div");
        if (infoDiv) {
            const players = (payload.players || []).map(p => (p.name || "").toUpperCase()).join(" vs. ");
            const existing = bar.querySelector(".upcoming-info");
            if (existing) existing.remove();
            const span = document.createElement("span");
            span.className = "upcoming-info";
            span.style.cssText = "margin-left:12px;color:#ff9800;font-size:12px";
            span.textContent = `Nächstes Match: ${players}`;
            infoDiv.appendChild(span);
        }
    }
}

// ─── Game Event Observation ───
// DOM-based "Game Shot" / "Match Shot" detection removed.
// Live match data sync is now handled server-side by AutodartsMatchListenerService
// which polls the Autodarts API directly and pushes updates via the DartSuite API.

// Score extraction is now handled server-side via Autodarts API (sync-external endpoint)

function showNextMatchButton() {
    if (document.getElementById("dartsuite-next-match-btn")) return;

    // Match ended — update status
    chrome.runtime.sendMessage({ action: "setMatchStatus", status: "ended" });

    // The next match button triggers prepareMatch for the next game
    const btn = document.createElement("button");
    btn.id = "dartsuite-next-match-btn";
    btn.style.cssText = `
        position: fixed; bottom: 50px; right: 20px;
        background: #e94560; color: #fff; border: none;
        padding: 12px 24px; border-radius: 8px; font-size: 14px;
        cursor: pointer; z-index: 10001; box-shadow: 0 4px 16px rgba(0,0,0,0.4);
        font-weight: bold;
    `;
    btn.textContent = "Nächstes Match vorbereiten";
    btn.addEventListener("click", () => {
        btn.remove();
        chrome.runtime.sendMessage({ action: "requestNextMatch", tournamentId: managedState.tournamentId, boardId: managedState.boardId });
    });
    document.body.appendChild(btn);
}

// ─── Utility Functions ───

function reportCurrentUrl(reason) {
    lastReportedUrl = location.href;
    chrome.runtime.sendMessage({ action: "reportUrl", url: location.href, reason }).catch(() => { });

    // Check for pending prepareMatch after navigation
    if (reason === "page-loaded" || reason === "route-change") {
        chrome.storage.sync.get("pendingPrepareMatch").then(stored => {
            if (stored.pendingPrepareMatch && location.pathname.startsWith("/lobbies")) {
                const pending = JSON.parse(stored.pendingPrepareMatch);
                const age = Date.now() - (pending._savedAt || 0);
                chrome.storage.sync.remove("pendingPrepareMatch");
                if (age > 30000) {
                    console.log("DartSuite: pendingPrepareMatch stale (age:", age, "ms), ignoring");
                    return;
                }
                const { _savedAt, ...payload } = pending;
                showStartExecutionOverlay(payload);
                setTimeout(() => handlePrepareMatch(payload), 600);
            }
        });
    }
}

function getPageState() {
    const url = location.href;
    const players = Array.isArray(currentMatchPlayers)
        ? currentMatchPlayers
            .map(p => {
                if (typeof p === "string") return p;
                if (p && typeof p === "object") return p.name || p.displayName || p.accountName || "";
                return "";
            })
            .map(name => name.trim())
            .filter(Boolean)
        : [];

    return {
        url,
        matchId: extractId(url, /\/matches\/([\w-]+)/i),
        lobbyId: extractId(url, /\/lobbies\/([\w-]+)/i),
        managed: managedState.active,
        tournamentId: managedState.tournamentId,
        currentMatchPlayers: players
    };
}

function extractId(url, pattern) {
    return url?.match(pattern)?.[1] || null;
}

function extractFirstIp(ipField) {
    if (!ipField) return null;
    const first = ipField.split(",")[0]?.trim();
    if (!first) return null;
    try { return new URL(first).hostname; } catch { return first; }
}

async function clickButtonByText(text) {
    const buttons = document.querySelectorAll("button");
    for (const btn of buttons) {
        const btnText = btn.textContent?.trim();
        if (btnText === text) {
            btn.click();
            return true;
        }
    }
    return false;
}

function sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

async function getApiBaseUrl() {
    try {
        const stored = await chrome.storage.sync.get({ apiBaseUrl: DEFAULT_API_BASE_URL });
        return (stored.apiBaseUrl || DEFAULT_API_BASE_URL).replace(/\/$/, "");
    } catch {
        return DEFAULT_API_BASE_URL;
    }
}

function escapeHtml(str) {
    if (!str) return "";
    const div = document.createElement("div");
    div.textContent = str;
    return div.innerHTML;
}

// ─── Quick Config (in-page panel for tournament join) ───

function setupQuickConfig() {
    const codeInput = document.getElementById("dartsuite-quick-code");
    const statusEl = document.getElementById("dartsuite-quick-status");
    const joinBtn = document.getElementById("dartsuite-quick-join");
    let quickTournament = null;

    let debounce;
    codeInput?.addEventListener("input", () => {
        clearTimeout(debounce);
        debounce = setTimeout(async () => {
            const code = (codeInput.value || "").trim().toUpperCase();
            if (code.length !== 3) return;
            const apiBaseUrl = await getApiBaseUrl();
            try {
                const result = await apiFetch(`${apiBaseUrl}/api/tournaments/by-code/${encodeURIComponent(code)}`, { method: "GET" });
                if (result?.id) {
                    quickTournament = result;
                    statusEl.style.color = "#4caf50";
                    statusEl.textContent = `Turnier gefunden: ${result.name}`;
                    await loadQuickBoardOptions();
                } else {
                    quickTournament = null;
                    statusEl.style.color = "#f44336";
                    statusEl.textContent = "Kein Turnier mit diesem Code gefunden.";
                }
            } catch {
                statusEl.style.color = "#f44336";
                statusEl.textContent = "API nicht erreichbar.";
            }
            updateQuickJoinButton();
        }, 600);
    });

    async function loadQuickBoardOptions() {
        const boardSection = document.getElementById("dartsuite-quick-board-section");
        const boardSelect = document.getElementById("dartsuite-quick-board");
        if (!boardSection || !boardSelect || !quickTournament) return;

        // Only show board selection for active tournaments
        const startDate = new Date(quickTournament.startDate);
        const isActive = startDate <= new Date();
        if (!isActive) {
            boardSection.style.display = "none";
            return;
        }

        boardSection.style.display = "block";
        boardSelect.innerHTML = '<option value="">Board wählen...</option>';

        // Get "my" boards from the boards tab in content script
        if (capturedBoards.length === 0) {
            window.postMessage({ type: "dartsuite-request-boards" }, "*");
            await sleep(2000);
        }

        // Get DartSuite boards to filter by tournament
        const apiBaseUrl = await getApiBaseUrl();
        let dsBoards = [];
        try {
            const result = await apiFetch(`${apiBaseUrl}/api/boards`, { method: "GET" });
            if (result?.ok && Array.isArray(result.body)) dsBoards = result.body;
        } catch { /* silent */ }

        // Show boards that are registered in DartSuite and available locally
        for (const board of capturedBoards) {
            const dsBoard = dsBoards.find(b => b.externalBoardId === board.id);
            if (!dsBoard) continue;
            const inTournament = dsBoard.tournamentId === quickTournament.id;
            const opt = document.createElement("option");
            opt.value = dsBoard.id;
            opt.dataset.name = dsBoard.name || board.name || board.id;
            opt.textContent = (board.name || board.id) + (inTournament ? "" : " (nicht im Turnier)");
            boardSelect.appendChild(opt);
        }

        if (boardSelect.options.length <= 1) {
            boardSelect.innerHTML = '<option value="">Keine Boards im Turnier registriert</option>';
        }

        boardSelect.addEventListener("change", updateQuickJoinButton);
    }

    function updateQuickJoinButton() {
        const boardSelect = document.getElementById("dartsuite-quick-board");
        const boardSection = document.getElementById("dartsuite-quick-board-section");
        const hasTournament = !!quickTournament;
        const needsBoard = boardSection?.style.display !== "none";
        const hasBoard = !needsBoard || (boardSelect?.value || "").length > 0;
        const canJoin = hasTournament && hasBoard;
        if (joinBtn) {
            joinBtn.disabled = !canJoin;
            joinBtn.style.opacity = canJoin ? "1" : "0.5";
        }
    }

    joinBtn?.addEventListener("click", async () => {
        if (!quickTournament) return;
        const boardSelect = document.getElementById("dartsuite-quick-board");
        const selectedBoardId = boardSelect?.value || null;

        // Save to storage and activate managed mode
        await chrome.storage.sync.set({
            tournamentId: quickTournament.id,
            hostInput: quickTournament.organizerAccount
        });

        if (selectedBoardId) {
            const selectedOption = boardSelect?.selectedOptions[0];
            const boardName = selectedOption?.dataset.name || selectedBoardId;
            setManagedMode({
                mode: "Auto",
                boardId: selectedBoardId,
                tournamentId: quickTournament.id,
                tournamentName: quickTournament.name,
                host: quickTournament.organizerAccount,
                boardName: boardName
            });
            chrome.runtime.sendMessage({
                action: "managedModeChanged",
                boardId: selectedBoardId,
                mode: "Auto",
                tournamentId: quickTournament.id,
                tournamentName: quickTournament.name,
                host: quickTournament.organizerAccount,
                boardName: boardName
            });
        }

        chrome.runtime.sendMessage({ action: "tournamentSelected", tournament: quickTournament });

        // Close panel and refresh
        const panel = document.getElementById("dartsuite-panel");
        if (panel) panel.remove();
        injectInfoBar();
    });
}

async function loadUpcomingMatchesForPanel() {
    const el = document.getElementById("dartsuite-panel-upcoming");
    if (!el || !managedState.active || !managedState.boardId) return;

    const apiBaseUrl = await getApiBaseUrl();
    try {
        const result = await apiFetch(`${apiBaseUrl}/api/matches/${managedState.tournamentId}`, { method: "GET" });
        const allMatches = Array.isArray(result?.body) ? result.body : [];

        // Fetch participants for name resolution
        if (cachedParticipants.length === 0 || Date.now() - participantsCacheTime > 60000) {
            try {
                const pResult = await apiFetch(`${apiBaseUrl}/api/tournaments/${managedState.tournamentId}/participants`, { method: "GET" });
                if (pResult?.ok && Array.isArray(pResult?.body)) {
                    cachedParticipants = pResult.body;
                    participantsCacheTime = Date.now();
                }
            } catch { /* silent */ }
        }

        const boardMatches = allMatches.filter(m => m.boardId === managedState.boardId && !m.finishedUtc)
            .sort((a, b) => (a.plannedStartUtc || "").localeCompare(b.plannedStartUtc || ""));

        if (boardMatches.length === 0) {
            el.textContent = "Keine anstehenden Matches an diesem Board.";
            return;
        }

        el.innerHTML = boardMatches.slice(0, 5).map((m, i) => {
            const time = m.plannedStartUtc ? new Date(m.plannedStartUtc).toLocaleTimeString("de-DE", { hour: "2-digit", minute: "2-digit" }) : "—";
            const home = resolveParticipantName(m.homeParticipantId);
            const away = resolveParticipantName(m.awayParticipantId);
            const isFirst = i === 0 && !m.startedUtc;
            let playBtn = "";
            if (isFirst) {
                const boardConnected = isBoardConnected();
                const boardHasActiveMatch = hasActiveAutodartsMatch();
                const canStart = apiReachable && !isMatchActive() && !boardHasActiveMatch;
                if (canStart) {
                    const btnColor = boardConnected ? "#4caf50" : "#ff9800";
                    const btnTitle = boardConnected
                        ? "Match starten"
                        : "Match starten (API erreichbar, Board nicht verbunden)";
                    playBtn = `<button class="dartsuite-panel-play-btn" style="background:${btnColor};border:none;color:#fff;padding:1px 6px;border-radius:3px;cursor:pointer;font-size:11px;margin-left:6px" title="${btnTitle}">▶</button>`;
                }
            }
            return `<div style="padding:3px 0;border-bottom:1px solid #333">
                <span style="color:#e94560;font-weight:bold">${time}</span>
                ${escapeHtml(home)} vs ${escapeHtml(away)}${playBtn}
            </div>`;
        }).join("");

        // Attach click handler for panel play button
        const btn = el.querySelector(".dartsuite-panel-play-btn");
        if (btn) {
            btn.addEventListener("click", (e) => {
                e.stopPropagation();
                requestStartNextMatch();
            });
        }
    } catch {
        el.textContent = "Fehler beim Laden der Matches.";
    }
}
