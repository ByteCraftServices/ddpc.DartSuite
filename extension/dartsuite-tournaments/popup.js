// DartSuite Tournaments — Popup v0.4.0
// Tournament selection, board management, friends import, settings.
// Verified transfers: only show success after API confirmation.

const DEFAULT_API_BASE_URL = "http://localhost:5290";
const SETTINGS_KEYS = {
    apiBaseUrl: DEFAULT_API_BASE_URL,
    defaultHost: "",
    hostInput: "",
    tournamentId: "",
    dartsuiteEnabled: true,
    autoManaged: true,
    fullscreen: false,
    statusPollSeconds: 30,
    statusBarMode: "always",
    debugMode: false
};

let currentTournament = null;
const lastBoardTransferOutcomes = new Map();
let dartsuiteEnabled = true;

// ─── Initialization ───

document.addEventListener("DOMContentLoaded", async () => {
    await loadSettings();
    setupTabs();
    setupEventListeners();
    applyExtensionEnabledUi();
    await updateHeaderStatus();
    await checkApiStatus();
    await updateApiErrorDisplay();
    if (dartsuiteEnabled) {
        await loadTournamentState();
    }
});

// ─── Header Status Display ───

async function updateHeaderStatus() {
    try {
        const status = await chrome.runtime.sendMessage({ action: "getStatus" });
        if (status) {
            const trophy = document.getElementById("headerTrophy");
            const statusEl = document.getElementById("headerDstStatus");
            const colors = { connected: "#4caf50", ready: "#ff9800", offline: "#f44336" };
            const labels = { connected: "Verbunden", ready: "Bereit", offline: "Offline" };
            const matchLabels = {
                available: "", idle: "Idle", scheduled: "Geplant",
                waitForPlayer: "Warte auf Spieler", waitForMatch: "Warte auf Match",
                playing: "Spiel läuft", listening: "Listener", disconnected: "Getrennt", ended: "Beendet"
            };
            if (trophy) {
                trophy.style.filter = status.dstStatus === "connected"
                    ? "hue-rotate(80deg) brightness(1.2)"
                    : status.dstStatus === "offline"
                        ? "hue-rotate(-40deg) brightness(0.8)"
                        : "";
            }
            if (statusEl) {
                const matchLabel = matchLabels[status.matchStatus] || "";
                statusEl.textContent = labels[status.dstStatus] || "";
                statusEl.style.color = colors[status.dstStatus] || "#888";
                if (matchLabel) {
                    statusEl.textContent += ` | ${matchLabel}`;
                }
            }
            showPopupStatusToast(status.dstStatus, status.matchStatus);
        }
    } catch { /* background not ready */ }
}

// ─── Tab Navigation ───

function setupTabs() {
    for (const btn of document.querySelectorAll(".tabs button")) {
        btn.addEventListener("click", () => {
            if (!dartsuiteEnabled && btn.dataset.tab !== "settings") {
                return;
            }

            document.querySelectorAll(".tabs button").forEach(b => b.classList.remove("active"));
            document.querySelectorAll(".tab-content").forEach(t => t.classList.remove("active"));
            btn.classList.add("active");
            const target = document.getElementById(`tab-${btn.dataset.tab}`);
            if (target) target.classList.add("active");
        });
    }
}

// ─── Event Listeners ───

function setupEventListeners() {
    // Manage extension link
    document.getElementById("manageLink")?.addEventListener("click", () => {
        chrome.tabs.create({ url: "chrome://extensions/?id=" + chrome.runtime.id });
    });

    // Host input — load tournaments on change
    const hostInput = document.getElementById("hostInput");
    let hostDebounce;
    hostInput?.addEventListener("input", () => {
        clearTimeout(hostDebounce);
        hostDebounce = setTimeout(() => loadTournamentsByHost(), 500);
    });

    // Code input — lookup tournament by code
    const codeInput = document.getElementById("codeInput");
    let codeDebounce;
    codeInput?.addEventListener("input", () => {
        clearTimeout(codeDebounce);
        codeDebounce = setTimeout(() => lookupTournamentByCode(), 600);
    });

    // Tournament dropdown
    document.getElementById("tournamentSelect")?.addEventListener("change", (e) => {
        if (e.target.value) selectTournamentById(e.target.value);
    });

    // Settings
    document.getElementById("saveSettings")?.addEventListener("click", saveSettings);
    document.getElementById("connectApiBtn")?.addEventListener("click", async () => {
        await checkApiStatus();
    });

    // Boards: transfer button
    document.getElementById("transferBoards")?.addEventListener("click", transferSelectedBoards);

    // Friends
    document.getElementById("selectAllFriends")?.addEventListener("click", () => {
        document.querySelectorAll("#friendsList input[type=checkbox]").forEach(cb => { cb.checked = true; });
    });
    document.getElementById("importFriends")?.addEventListener("click", importSelectedFriends);
}

// ─── API Status ───

async function checkApiStatus() {
    const apiBaseUrl = getApiBaseUrl();
    setConnectState("connecting");
    try {
        const response = await fetch(`${apiBaseUrl}/api/boards`, { signal: AbortSignal.timeout(3000) });
        if (response.ok) {
            setDot("apiDot", "online");
            document.getElementById("apiStatus").textContent = "DartSuite API: online";
            if (dartsuiteEnabled) {
                chrome.runtime.sendMessage({ action: "setDstStatus", status: "ready" });
            }
            setConnectState("connected");
            await updateApiErrorDisplay();
        } else {
            setDot("apiDot", "offline");
            document.getElementById("apiStatus").textContent = `API: Fehler (${response.status})`;
            if (dartsuiteEnabled) {
                chrome.runtime.sendMessage({ action: "setDstStatus", status: "offline" });
            }
            setConnectState("offline");
            openTab("settings");
            focusApiUrl();
            await updateApiErrorDisplay();
        }
    } catch {
        setDot("apiDot", "offline");
        document.getElementById("apiStatus").textContent = "DartSuite API: nicht erreichbar";
        if (dartsuiteEnabled) {
            chrome.runtime.sendMessage({ action: "setDstStatus", status: "offline" });
        }
        setConnectState("offline");
        openTab("settings");
        focusApiUrl();
        await updateApiErrorDisplay();
    }

    // Check Autodarts tab
    try {
        const state = await getFromActiveTab("ping");
        if (state?.ok) {
            setDot("autoDot", "online");
            document.getElementById("autoStatus").textContent = "Autodarts: verbunden";
        } else {
            setDot("autoDot", "warning");
            document.getElementById("autoStatus").textContent = "Autodarts: kein Tab offen";
        }
    } catch {
        setDot("autoDot", "warning");
        document.getElementById("autoStatus").textContent = "Autodarts: kein Tab offen";
    }
}

// ─── Tournament Selection ───

async function loadTournamentsByHost() {
    const host = document.getElementById("hostInput").value.trim();
    const select = document.getElementById("tournamentSelect");
    const errEl = document.getElementById("tournamentError");
    errEl.style.display = "none";

    if (!host) {
        select.disabled = true;
        select.innerHTML = '<option value="">Zuerst Host eingeben</option>';
        return;
    }

    const apiBaseUrl = getApiBaseUrl();
    try {
        const encodedHost = encodeURIComponent(host);
        const response = await fetch(`${apiBaseUrl}/api/tournaments?host=${encodedHost}`);
        if (!response.ok) throw new Error(`Status ${response.status}`);
        const tournaments = await response.json();

        select.innerHTML = '<option value="">Turnier auswählen...</option>';
        for (const t of tournaments) {
            const opt = document.createElement("option");
            opt.value = t.id;
            opt.textContent = `${t.name} (${t.joinCode || "—"}) — ${t.startDate}`;
            select.appendChild(opt);
        }
        select.disabled = tournaments.length === 0;
        if (tournaments.length === 0) {
            select.innerHTML = '<option value="">Keine Turniere gefunden</option>';
        }
    } catch {
        select.disabled = true;
        select.innerHTML = '<option value="">Fehler beim Laden</option>';
    }
}

async function lookupTournamentByCode() {
    const code = document.getElementById("codeInput").value.trim().toUpperCase();
    const errEl = document.getElementById("tournamentError");
    const successEl = document.getElementById("tournamentSuccess");
    errEl.style.display = "none";
    successEl.style.display = "none";

    if (code.length !== 3) return;

    const apiBaseUrl = getApiBaseUrl();
    try {
        const response = await fetch(`${apiBaseUrl}/api/tournaments/by-code/${encodeURIComponent(code)}`);
        if (response.ok) {
            const tournament = await response.json();
            currentTournament = tournament;

            // Auto-fill host
            document.getElementById("hostInput").value = tournament.organizerAccount;

            successEl.textContent = `Turnier gefunden: ${tournament.name}`;
            successEl.style.display = "block";

            await saveTournamentSelection(tournament);
            await showActiveTournament(tournament);
            chrome.runtime.sendMessage({ action: "setDstStatus", status: "connected" });
            chrome.runtime.sendMessage({ action: "tournamentSelected", tournament });
        } else if (response.status === 404) {
            errEl.textContent = "Kein Turnier mit diesem Code gefunden.";
            errEl.style.display = "block";
            chrome.runtime.sendMessage({ action: "setDstStatus", status: "ready" });
        } else {
            errEl.textContent = `Fehler: ${response.status}`;
            errEl.style.display = "block";
        }
    } catch {
        errEl.textContent = "API nicht erreichbar.";
        errEl.style.display = "block";
    }
}

async function selectTournamentById(id) {
    const apiBaseUrl = getApiBaseUrl();
    try {
        const response = await fetch(`${apiBaseUrl}/api/tournaments/${id}`);
        if (!response.ok) return;
        const tournament = await response.json();
        currentTournament = tournament;

        document.getElementById("codeInput").value = tournament.joinCode || "";
        await saveTournamentSelection(tournament);
        await showActiveTournament(tournament);
        chrome.runtime.sendMessage({ action: "setDstStatus", status: "connected" });
        chrome.runtime.sendMessage({ action: "tournamentSelected", tournament });
    } catch {
        setMessage("Fehler beim Laden des Turniers.");
    }
}

async function showActiveTournament(tournament) {
    const section = document.getElementById("activeTournamentSection");
    const info = document.getElementById("activeTournamentInfo");
    if (!section || !info) return;
    section.style.display = "block";
    info.innerHTML = `
        <div class="tournament-card active selected">
            <div class="name">${escapeHtml(tournament.name)}</div>
            <div class="meta">
                Host: ${escapeHtml(tournament.organizerAccount)} |
                Code: <span class="code">${escapeHtml(tournament.joinCode || "—")}</span>
            </div>
            <div class="meta">${tournament.startDate} — ${tournament.endDate} | ${tournament.mode}</div>
        </div>
    `;

    // Show board selection if tournament is active (startDate <= today)
    const startDate = new Date(tournament.startDate);
    const isActive = startDate <= new Date();
    if (isActive) {
        await loadActiveBoardOptions();
    } else {
        const boardSection = document.getElementById("boardSelectSection");
        if (boardSection) boardSection.style.display = "none";
        await synchronizeTournamentContext({ showMessage: false, source: "initial-load" });
    }
}

async function saveTournamentSelection(tournament) {
    await chrome.storage.sync.set({
        tournamentId: tournament.id,
        hostInput: tournament.organizerAccount
    });
}

async function loadTournamentState() {
    const stored = await chrome.storage.sync.get(["tournamentId", "hostInput"]);
    if (stored.hostInput) {
        document.getElementById("hostInput").value = stored.hostInput;
        await loadTournamentsByHost();
    }
    if (stored.tournamentId) {
        // Reload tournament info
        const apiBaseUrl = getApiBaseUrl();
        try {
            const response = await fetch(`${apiBaseUrl}/api/tournaments/${stored.tournamentId}`);
            if (response.ok) {
                const tournament = await response.json();
                currentTournament = tournament;
                document.getElementById("codeInput").value = tournament.joinCode || "";
                document.getElementById("tournamentSelect").value = tournament.id;
                await showActiveTournament(tournament);
            }
        } catch { /* API offline, silent */ }
    }
}

// ─── Boards Tab ───

async function loadBoards() {
    const list = document.getElementById("boardsList");
    const transferBtn = document.getElementById("transferBoards");

    // Get local Autodarts boards from content script
    const localResult = await getFromActiveTab("getLocalBoards");
    const localBoards = localResult?.boards || [];
    const localBoardError = localResult?.boardError || null;

    if (localBoards.length === 0) {
        const reason = localBoardError?.message
            ? escapeHtml(localBoardError.message)
            : "Keine lokalen Boards gefunden. Bitte play.autodarts.io/boards öffnen.";
        const extra = localBoardError?.status
            ? `<div class="info" style="color:#ffcc80">Status: ${escapeHtml(String(localBoardError.status))}</div>`
            : "";
        list.innerHTML = `<div class="error">${reason}</div>${extra}`;
        if (transferBtn) transferBtn.disabled = true;
        return;
    }

    // Get DartSuite boards to compare
    const apiBaseUrl = getApiBaseUrl();
    let dartsuiteBoards = [];
    try {
        const response = await fetch(`${apiBaseUrl}/api/boards`);
        if (response.ok) dartsuiteBoards = await response.json();
    } catch { /* API offline */ }

    // Map DartSuite boards by externalBoardId for lookup
    const dsMap = new Map();
    for (const b of dartsuiteBoards) {
        dsMap.set(normalizeBoardId(b.externalBoardId), b);
    }

    // Check current tournament boards
    const tournamentId = currentTournament?.id;

    list.innerHTML = "";
    let hasTransferable = false;

    for (const board of localBoards) {
        const boardId = board.id || board.externalBoardId;
        const boardName = board.name || boardId;
        const boardIp = board.ip || board.localIpAddress || "";
        const dsBoard = dsMap.get(normalizeBoardId(boardId));
        const isInTournament = dsBoard && dsBoard.tournamentId && tournamentId &&
            sameId(dsBoard.tournamentId, tournamentId);
        const lastOutcome = lastBoardTransferOutcomes.get(normalizeBoardId(boardId));

        const item = document.createElement("div");
        item.className = "board-item";
        item.style.flexWrap = "wrap";

        if (isInTournament) {
            // Board is already in the tournament — show green check + Teilnehmen/Verlassen
            const isManaged = dsBoard.managedMode === "Auto";
            const reachable = boardIp ? await pingBoard(boardIp) : false;

            item.innerHTML = `
                <div style="flex:1;display:flex;gap:6px;align-items:flex-start">
                    <span style="color:#4caf50;font-weight:bold;font-size:13px;line-height:1">✓</span>
                    <div>
                        <div class="board-name">${escapeHtml(boardName)}</div>
                        <div class="board-status" style="color:#4caf50;font-size:10px">Im Turnier | ${dsBoard.managedMode}</div>
                    </div>
                </div>
                ${reachable
                    ? (isManaged
                        ? `<button class="btn btn-danger" data-board-ds-id="${dsBoard.id}" data-mode="Manual" style="width:auto;padding:4px 10px;font-size:11px">Verlassen</button>`
                        : `<button class="btn btn-success" data-board-ds-id="${dsBoard.id}" data-mode="Auto" style="width:auto;padding:4px 10px;font-size:11px">Teilnehmen</button>`)
                    : '<span class="info" style="font-size:10px">Nicht erreichbar</span>'}
            `;
        } else {
            // Board not in tournament — show checkbox for transfer
            hasTransferable = true;
            const inDartSuite = !!dsBoard;
            const outcomeHint = lastOutcome && !lastOutcome.ok
                ? `<div class="board-status" style="color:#ffb74d;font-size:10px">Zuordnung fehlgeschlagen: ${escapeHtml(lastOutcome.reason || "Unbekannter Fehler")}</div>`
                : "";
            item.innerHTML = `
                <div class="checkbox-row" style="flex:1">
                    <input type="checkbox" data-board-ext-id="${escapeHtml(boardId)}"
                           data-board-name="${escapeHtml(boardName)}"
                           data-board-ip="${escapeHtml(boardIp)}" />
                    <div>
                        <div class="board-name">${escapeHtml(boardName)}</div>
                        <div class="board-status" style="color:#aaa;font-size:10px">
                            ${inDartSuite ? 'In DartSuite, nicht im Turnier' : 'Neu'} | ${boardIp || 'Keine IP'}
                        </div>
                        ${outcomeHint}
                    </div>
                </div>
            `;
        }

        list.appendChild(item);
    }

    if (transferBtn) transferBtn.disabled = !hasTransferable || !currentTournament;

    // Attach managed mode click handlers
    for (const btn of list.querySelectorAll("[data-board-ds-id]")) {
        btn.addEventListener("click", () => {
            setManagedMode(btn.dataset.boardDsId, btn.dataset.mode);
        });
    }
}

async function pingBoard(ipField) {
    if (!ipField) return false;
    // Extract first URL/IP from the field
    const first = ipField.split(",")[0]?.trim();
    if (!first) return false;
    let pingUrl;
    try {
        // If it's already a URL, use it directly
        const url = new URL(first);
        pingUrl = url.origin;
    } catch {
        // Plain IP — try common board manager port
        pingUrl = `http://${first}`;
    }
    try {
        const result = await chrome.runtime.sendMessage({
            action: "proxyFetch",
            url: pingUrl,
            options: { method: "HEAD", signal: undefined }
        });
        return result?.ok || result?.status > 0;
    } catch {
        return false;
    }
}

async function transferSelectedBoards() {
    if (!currentTournament) {
        setMessage("Bitte zuerst ein Turnier auswählen.");
        return;
    }
    const checked = document.querySelectorAll("#boardsList input[type=checkbox]:checked");
    if (checked.length === 0) {
        setMessage("Keine Boards ausgewählt.");
        return;
    }

    const apiBaseUrl = getApiBaseUrl();
    let transferred = 0;
    let failed = 0;
    const results = [];

    for (const cb of checked) {
        const extId = cb.dataset.boardExtId;
        const name = cb.dataset.boardName;
        const ip = cb.dataset.boardIp;
        try {
            const result = await ensureBoardRegisteredInTournament({ apiBaseUrl, extId, name, ip, tournamentId: currentTournament.id });
            results.push(result);
            lastBoardTransferOutcomes.set(normalizeBoardId(extId), result);

            if (result.ok) {
                transferred++;
            } else {
                failed++;
            }
        } catch (error) {
            failed++;
            const fallback = { extId, name, ok: false, reason: error?.message || "Unbekannter Fehler" };
            results.push(fallback);
            lastBoardTransferOutcomes.set(normalizeBoardId(extId), fallback);
        }
    }

    if (failed > 0) {
        const firstIssues = results
            .filter(x => !x.ok)
            .slice(0, 2)
            .map(x => `${x.name}: ${x.reason || "Fehler"}`)
            .join(" | ");
        const more = failed > 2 ? ` (+${failed - 2} weitere)` : "";
        setMessage(`${transferred} Board(s) im Turnier registriert, ${failed} fehlgeschlagen. ${firstIssues}${more}`, "error");
    } else {
        setMessage(`${transferred} Board(s) im Turnier registriert. ✓`);
    }
    await loadBoards(); // Refresh to show verified tournament state
}

async function setManagedMode(boardId, mode) {
    const apiBaseUrl = getApiBaseUrl();
    const tournamentId = currentTournament?.id || "";
    const tournamentName = currentTournament?.name || "";
    const host = currentTournament?.organizerAccount || "";
    try {
        const url = `${apiBaseUrl}/api/boards/${boardId}/managed?mode=${mode}` +
            (mode === "Auto" && tournamentId ? `&tournamentId=${tournamentId}` : "");
        const response = await apiFetchWithAutodartsRetry(apiBaseUrl, url, { method: "PATCH" });
        if (response.ok) {
            // Resolve board name from the boards list
            let boardName = boardId;
            try {
                const bResp = await apiFetchWithAutodartsRetry(apiBaseUrl, `${apiBaseUrl}/api/boards`);
                if (bResp.ok) {
                    const boards = await bResp.json();
                    const b = boards.find(x => x.id === boardId);
                    if (b) boardName = b.name;
                }
            } catch { /* silent */ }

            setMessage(mode === "Auto" ? "Board nimmt teil!" : "Teilnahme beendet.");
            chrome.runtime.sendMessage({
                action: "managedModeChanged",
                boardId, mode, tournamentId, tournamentName, host, boardName
            });
            await loadBoards();
        } else {
            const errorMessage = await readApiErrorResponse(response, "Managed Mode konnte nicht gesetzt werden.");
            setMessage(errorMessage, "error");
        }
    } catch (error) {
        setMessage(error?.message || "Fehler beim Setzen des Managed Mode.", "error");
    }
}
// Make accessible from inline onclick
window.setManagedMode = setManagedMode;

// ─── Friends Tab ───

function extractFriendName(friend) {
    if (!friend || typeof friend !== "object") return "Unbekannt";
    // Try direct properties (common Autodarts / Keycloak field names)
    const direct = [
        friend.name, friend.username, friend.displayName,
        friend.display_name, friend.preferred_username, friend.email
    ];
    for (const n of direct) {
        if (n && typeof n === "string" && n.trim()) return n.trim();
    }
    // Try nested user / profile objects
    for (const nested of [friend.user, friend.profile]) {
        if (nested && typeof nested === "object") {
            const inner = [nested.name, nested.username, nested.displayName, nested.display_name, nested.preferred_username];
            for (const n of inner) {
                if (n && typeof n === "string" && n.trim()) return n.trim();
            }
        }
    }
    // Fallback: first short string property that looks like a name
    for (const [key, val] of Object.entries(friend)) {
        if (typeof val === "string" && val.trim() &&
            !key.toLowerCase().includes("id") && key !== "status" && key !== "country" &&
            val.length < 100) {
            return val.trim();
        }
    }
    return friend.id || "Unbekannt";
}

async function loadFriends() {
    const list = document.getElementById("friendsList");
    const importBtn = document.getElementById("importFriends");

    // Get local Autodarts friends from content script
    const localResult = await getFromActiveTab("getLocalFriends");
    const localFriends = localResult?.friends || [];

    if (localFriends.length === 0) {
        list.innerHTML = '<div class="info">Keine Freunde gefunden. Bitte play.autodarts.io öffnen.</div>';
        if (importBtn) importBtn.disabled = true;
        return;
    }

    // If a tournament is selected, get existing participants to mark duplicates
    let existingParticipants = [];
    if (currentTournament) {
        const apiBaseUrl = getApiBaseUrl();
        try {
            const response = await fetch(`${apiBaseUrl}/api/tournaments/${currentTournament.id}/participants`);
            if (response.ok) existingParticipants = await response.json();
        } catch { /* silent */ }
    }
    const participantNames = new Set(existingParticipants.map(p => (p.accountName || p.displayName || "").toLowerCase()));

    list.innerHTML = "";
    let hasImportable = false;
    for (const friend of localFriends) {
        const friendName = extractFriendName(friend);
        const friendId = friend.id || friendName;
        const alreadyParticipant = participantNames.has(friendName.toLowerCase());

        const item = document.createElement("div");
        item.className = "friend-item";

        if (alreadyParticipant) {
            item.innerHTML = `
                <span style="color:#4caf50;font-size:12px">✓</span>
                <span style="color:#888">${escapeHtml(friendName).toUpperCase()}</span>
                <span class="info" style="margin-left:auto">Bereits Teilnehmer</span>
            `;
        } else {
            hasImportable = true;
            item.innerHTML = `
                <input type="checkbox" data-friend-id="${escapeHtml(friendId)}" data-friend-name="${escapeHtml(friendName)}" style="width:auto" />
                <span>${escapeHtml(friendName).toUpperCase()}</span>
            `;
        }
        list.appendChild(item);
    }

    if (importBtn) importBtn.disabled = !hasImportable || !currentTournament;
}

async function importSelectedFriends() {
    if (!currentTournament) {
        setMessage("Bitte zuerst ein Turnier auswählen.");
        return;
    }
    const checked = document.querySelectorAll("#friendsList input[type=checkbox]:checked");
    if (checked.length === 0) {
        setMessage("Keine Freunde ausgewählt.");
        return;
    }

    const apiBaseUrl = getApiBaseUrl();
    let imported = 0;
    let failed = 0;

    for (const cb of checked) {
        const name = cb.dataset.friendName;
        try {
            const response = await fetch(`${apiBaseUrl}/api/tournaments/${currentTournament.id}/participants`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    tournamentId: currentTournament.id,
                    displayName: name,
                    accountName: name,
                    isAutodartsAccount: true,
                    isManager: false,
                    seed: 0
                })
            });
            if (response.ok) {
                const participant = await response.json();
                if (participant?.id) {
                    imported++;
                } else {
                    failed++;
                }
            } else if (response.status === 409) {
                // Already exists — verify by checking participant list
                const verifyResp = await fetch(`${apiBaseUrl}/api/tournaments/${currentTournament.id}/participants`);
                if (verifyResp.ok) {
                    const participants = await verifyResp.json();
                    const exists = participants.some(p =>
                        (p.accountName || "").toLowerCase() === name.toLowerCase() ||
                        (p.displayName || "").toLowerCase() === name.toLowerCase()
                    );
                    if (exists) {
                        imported++;
                    } else {
                        failed++;
                    }
                } else {
                    failed++;
                }
            } else {
                failed++;
            }
        } catch {
            failed++;
        }
    }

    if (failed > 0) {
        setMessage(`${imported} Freunde importiert, ${failed} fehlgeschlagen.`, "error");
    } else {
        setMessage(`${imported} Freunde verifiziert importiert. ✓`);
    }
    // Refresh friends list to show verified checkmarks
    await loadFriends();
}

// ─── Active Board Selection (shown in tournament tab when tournament is live) ───

async function loadActiveBoardOptions() {
    const boardSection = document.getElementById("boardSelectSection");
    const boardSelect = document.getElementById("activeBoardSelect");
    const refreshBtn = document.getElementById("refreshBoardSchedule");
    if (!boardSection || !boardSelect) return;

    // Get local (my) boards from the content script
    const localResult = await getFromActiveTab("getLocalBoards");
    const localBoards = localResult?.boards || [];

    // Get DartSuite boards from API to resolve internal IDs
    const apiBaseUrl = getApiBaseUrl();
    let dsBoards = [];
    try {
        const resp = await fetch(`${apiBaseUrl}/api/boards`);
        if (resp.ok) dsBoards = await resp.json();
    } catch { /* API offline */ }
    const extToDsMap = new Map();
    for (const ds of dsBoards) {
        extToDsMap.set(ds.externalBoardId, ds);
    }

    // Filter: show boards that are registered in DartSuite and available locally.
    // Prefer boards assigned to the current tournament, but also show unassigned ones.
    const tournamentId = currentTournament?.id;
    const availableBoards = localBoards.filter(board => extToDsMap.has(board.id));

    boardSection.style.display = "block";

    if (availableBoards.length === 0) {
        boardSelect.innerHTML = '<option value="">Keine Boards in DartSuite registriert</option>';
        boardSelect.disabled = true;
        boardSection.querySelector(".info")?.remove();
        const hint = document.createElement("div");
        hint.className = "info";
        hint.style.color = "#ff9800";
        hint.textContent = "Registriere deine Boards zuerst im Tab \"Boards\" und weise sie dem Turnier zu.";
        if (!boardSection.querySelector(".info")) boardSection.appendChild(hint);
        return;
    }

    boardSelect.disabled = false;
    boardSelect.innerHTML = '<option value="">Board wählen...</option>';
    for (const board of availableBoards) {
        const dsBoard = extToDsMap.get(board.id);
        const inTournament = dsBoard.tournamentId === tournamentId;
        const opt = document.createElement("option");
        opt.value = dsBoard.id;
        opt.dataset.name = dsBoard.name || board.name || board.id;
        opt.dataset.externalId = board.id;
        opt.textContent = (board.name || board.id) + (inTournament ? "" : " (nicht im Turnier)");
        boardSelect.appendChild(opt);
    }

    // Restore previously selected board only if it belongs to the currently selected tournament context
    const stored = await chrome.storage.sync.get(["managedBoardId", "managedTournamentId"]);
    if (stored.managedBoardId && sameId(stored.managedTournamentId, tournamentId)) {
        boardSelect.value = stored.managedBoardId;
    } else {
        boardSelect.value = "";
    }

    // Show warning if no board selected yet
    updateBoardSelectionHint(boardSelect);

    // On change: keep managed context in sync across popup/content/background
    boardSelect.onchange = async () => {
        updateBoardSelectionHint(boardSelect);
        await synchronizeTournamentContext({ showMessage: true, source: "user-change" });
    };

    await synchronizeTournamentContext({ showMessage: false, source: "initial-load" });

    // Refresh button — manually re-fetch schedule
    if (refreshBtn) {
        refreshBtn.onclick = async () => {
            refreshBtn.disabled = true;
            refreshBtn.textContent = "⟳";
            // Trigger content script to re-poll schedule
            const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
            if (tab?.id && tab.url?.startsWith("https://play.autodarts.io/")) {
                await chrome.tabs.sendMessage(tab.id, { action: "refreshSchedule" });
            }
            setTimeout(() => { refreshBtn.disabled = false; refreshBtn.textContent = "🔄"; }, 2000);
        };
    }
}

async function synchronizeTournamentContext(options) {
    const showMessage = !!options?.showMessage;
    const source = options?.source || "system";
    if (!currentTournament) return;

    const boardSelect = document.getElementById("activeBoardSelect");
    const selectedBoardId = boardSelect?.value || "";
    const selectedOption = boardSelect?.selectedOptions?.[0] || null;
    const selectedBoardName = selectedOption?.dataset?.name || selectedBoardId;

    const stored = await chrome.storage.sync.get(["managedBoardId", "managedTournamentId"]);

    await chrome.storage.sync.set({
        managedTournamentId: currentTournament.id,
        managedTournamentName: currentTournament.name,
        managedHost: currentTournament.organizerAccount
    });

    if (selectedBoardId) {
        const isSameManagedContext =
            sameId(stored.managedBoardId, selectedBoardId)
            && sameId(stored.managedTournamentId, currentTournament.id);

        await chrome.storage.sync.set({
            managedBoardId: selectedBoardId,
            managedBoardName: selectedBoardName
        });

        // Popup öffnen darf niemals ungewollt einen Start-Flow triggern.
        // Deshalb nur bei echter Änderung oder expliziter Benutzeraktion Auto-Mode melden.
        if (!isSameManagedContext || source === "user-change") {
            await notifyManagedModeAuto(selectedBoardId, selectedBoardName);
        } else {
            await notifyTournamentContextChanged();
        }

        if (showMessage) {
            setMessage(`Aktives Turnier: ${currentTournament.name} | Board: ${selectedBoardName}`);
        }
        return;
    }

    if (stored.managedBoardId && !sameId(stored.managedTournamentId, currentTournament.id)) {
        await notifyManagedModeManual(stored.managedBoardId, true);
        await chrome.storage.sync.remove(["managedBoardId", "managedBoardName"]);
    }

    await notifyTournamentContextChanged();

    if (showMessage) {
        setMessage(`Aktives Turnier: ${currentTournament.name}`);
    }
}

async function notifyManagedModeAuto(boardId, boardName) {
    if (!currentTournament) return;

    const payload = {
        mode: "Auto",
        boardId,
        tournamentId: currentTournament.id,
        tournamentName: currentTournament.name,
        host: currentTournament.organizerAccount,
        boardName
    };

    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (tab?.id && tab.url?.startsWith("https://play.autodarts.io/")) {
        chrome.tabs.sendMessage(tab.id, { action: "setManagedMode", payload });
    }

    chrome.runtime.sendMessage({ action: "managedModeChanged", ...payload });
}

async function notifyManagedModeManual(boardId, keepTournamentContext) {
    if (!boardId) return;

    const payload = {
        mode: "Manual",
        boardId,
        keepTournamentContext: !!keepTournamentContext,
        tournamentId: currentTournament?.id || null,
        tournamentName: currentTournament?.name || "",
        host: currentTournament?.organizerAccount || ""
    };

    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (tab?.id && tab.url?.startsWith("https://play.autodarts.io/")) {
        chrome.tabs.sendMessage(tab.id, { action: "setManagedMode", payload });
    }

    chrome.runtime.sendMessage({ action: "managedModeChanged", ...payload });
}

async function notifyTournamentContextChanged() {
    if (!currentTournament) return;

    const payload = {
        tournamentId: currentTournament.id,
        tournamentName: currentTournament.name,
        host: currentTournament.organizerAccount
    };

    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (tab?.id && tab.url?.startsWith("https://play.autodarts.io/")) {
        chrome.tabs.sendMessage(tab.id, { action: "tournamentContextChanged", payload });
    }

    chrome.runtime.sendMessage({ action: "tournamentContextChanged", payload });
}

function updateBoardSelectionHint(boardSelect) {
    const boardSection = document.getElementById("boardSelectSection");
    if (!boardSection) return;
    let hint = boardSection.querySelector(".board-hint");
    if (!boardSelect.value) {
        if (!hint) {
            hint = document.createElement("div");
            hint.className = "board-hint";
            hint.style.cssText = "background:#4a3000;border:1px solid #ff9800;border-radius:4px;padding:6px 8px;margin-top:6px;font-size:11px;color:#ffcc80";
            hint.textContent = "⚠ Bitte wähle ein Board aus, damit DartSuite die Lobby steuern kann.";
            boardSection.appendChild(hint);
        }
    } else if (hint) {
        hint.remove();
    }
}

// ─── Settings ───

async function loadSettings() {
    const stored = await chrome.storage.sync.get(SETTINGS_KEYS);
    dartsuiteEnabled = stored.dartsuiteEnabled !== false;
    document.getElementById("apiBaseUrl").value = stored.apiBaseUrl || DEFAULT_API_BASE_URL;
    document.getElementById("defaultHost").value = stored.defaultHost || "";
    document.getElementById("dartsuiteEnabled").checked = dartsuiteEnabled;
    document.getElementById("autoManaged").checked = stored.autoManaged !== false;
    document.getElementById("fullscreen").checked = !!stored.fullscreen;
    document.getElementById("statusPollSeconds").value = stored.statusPollSeconds || 30;
    document.getElementById("statusBarMode").value = stored.statusBarMode || "always";
    document.getElementById("debugModeEnabled").checked = !!stored.debugMode;

    // Apply defaults
    if (stored.defaultHost && !stored.hostInput) {
        document.getElementById("hostInput").value = stored.defaultHost;
    }
}

async function saveSettings() {
    dartsuiteEnabled = document.getElementById("dartsuiteEnabled").checked;

    const settings = {
        apiBaseUrl: document.getElementById("apiBaseUrl").value.trim() || DEFAULT_API_BASE_URL,
        defaultHost: document.getElementById("defaultHost").value.trim(),
        dartsuiteEnabled,
        autoManaged: document.getElementById("autoManaged").checked,
        fullscreen: document.getElementById("fullscreen").checked,
        statusPollSeconds: Number(document.getElementById("statusPollSeconds").value || 30),
        statusBarMode: document.getElementById("statusBarMode").value || "always",
        debugMode: document.getElementById("debugModeEnabled").checked
    };
    await chrome.storage.sync.set(settings);
    applyExtensionEnabledUi();
    // Propagate debug mode to all open autodarts.io tabs
    const debugEnabled = settings.debugMode;
    const tabs = await chrome.tabs.query({ url: "https://play.autodarts.io/*" });
    for (const tab of tabs) {
        chrome.tabs.sendMessage(tab.id, { action: "setDebugMode", enabled: debugEnabled }).catch(() => { });
    }
    chrome.runtime.sendMessage({ action: "updateStatusPolling" });
    setMessage("Einstellungen gespeichert.");
    await checkApiStatus();
}

function getApiBaseUrl() {
    const val = document.getElementById("apiBaseUrl")?.value?.trim() || DEFAULT_API_BASE_URL;
    return val.replace(/\/$/, "");
}

// ─── Tab Communication ───

async function getFromActiveTab(action, payload) {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (!tab?.id || !tab.url?.startsWith("https://play.autodarts.io/")) return null;
    try {
        return await chrome.tabs.sendMessage(tab.id, { action, payload });
    } catch {
        return null;
    }
}

// ─── Tab Observer: load tab-specific data when switching ───

const tabObserver = new MutationObserver(() => {
    if (!dartsuiteEnabled) return;

    const activeTab = document.querySelector(".tabs button.active");
    if (!activeTab) return;
    const tab = activeTab.dataset.tab;
    if (tab === "boards") loadBoards();
    if (tab === "friends") loadFriends();
});
tabObserver.observe(document.querySelector(".tabs") || document.body, { subtree: true, attributes: true, attributeFilter: ["class"] });

function applyExtensionEnabledUi() {
    const tabButtons = document.querySelectorAll(".tabs button");
    for (const btn of tabButtons) {
        const isSettings = btn.dataset.tab === "settings";
        btn.style.display = dartsuiteEnabled || isSettings ? "" : "none";
    }

    const tabContents = document.querySelectorAll(".tab-content");
    for (const content of tabContents) {
        const isSettings = content.id === "tab-settings";
        content.style.display = dartsuiteEnabled || isSettings ? "" : "none";
    }

    if (!dartsuiteEnabled) {
        openTab("settings");
        setMessage("DartSuite ist deaktiviert. Nur Einstellungen sind sichtbar.");
    }
}

// ─── UI Helpers ───

function setDot(id, status) {
    const el = document.getElementById(id);
    if (el) el.className = `dot ${status}`;
}

function setMessage(text, type) {
    const el = document.getElementById("statusMessage");
    if (el) {
        el.textContent = text;
        const isError = type === "error";
        el.style.color = isError ? "#ffebee" : "#e8f5e9";
        el.style.background = isError ? "#b71c1c" : "#1b5e20";
        el.style.borderRadius = "4px";
        el.style.padding = "6px 10px";
        setTimeout(() => {
            el.textContent = "";
            el.style.color = "";
            el.style.background = "";
            el.style.borderRadius = "";
            el.style.padding = "";
        }, 5000);
    }
}

function escapeHtml(str) {
    if (!str) return "";
    const div = document.createElement("div");
    div.textContent = str;
    return div.innerHTML;
}

function openTab(tabId) {
    const btn = document.querySelector(`.tabs button[data-tab="${tabId}"]`);
    if (!btn) return;
    btn.click();
}

function focusApiUrl() {
    const input = document.getElementById("apiBaseUrl");
    if (!input) return;
    input.focus();
    input.select();
}

async function updateApiErrorDisplay() {
    const el = document.getElementById("apiLastError");
    if (!el) return;
    try {
        const stored = await chrome.storage.local.get({ apiLastError: "", apiLastErrorUtc: "" });
        if (stored.apiLastError) {
            const ts = stored.apiLastErrorUtc ? ` (${stored.apiLastErrorUtc})` : "";
            el.textContent = `Letzter Fehler: ${stored.apiLastError}${ts}`;
        } else {
            el.textContent = "";
        }
    } catch {
        el.textContent = "";
    }
}

function setConnectState(state) {
    const btn = document.getElementById("connectApiBtn");
    const label = document.getElementById("connectApiState");
    if (!btn || !label) return;
    btn.disabled = state === "connecting";
    if (state === "connecting") {
        btn.textContent = "Verbinde...";
        btn.style.background = "#ff9800";
        btn.style.borderColor = "#ff9800";
        label.textContent = "Prüfe";
        label.style.color = "#ffcc80";
    } else if (state === "connected") {
        btn.textContent = "Verbunden";
        btn.style.background = "#2e7d32";
        btn.style.borderColor = "#2e7d32";
        label.textContent = "Online";
        label.style.color = "#4caf50";
    } else {
        btn.textContent = "Verbinden";
        btn.style.background = "#0f3460";
        btn.style.borderColor = "#333";
        label.textContent = "Offline";
        label.style.color = "#f44336";
    }
}

function showPopupStatusToast(dstStatus, matchStatus) {
    const el = document.getElementById("statusMessage");
    if (!el) return;
    if (el.textContent && el.textContent.length > 0) return;
    const colors = {
        connected: "#1b5e20",
        ready: "#e65100",
        offline: "#b71c1c"
    };
    const label = dstStatus === "connected" ? "Verbunden" : dstStatus === "ready" ? "Bereit" : "Offline";
    const matchLabel = matchStatus && matchStatus !== "available" ? ` | ${matchStatus}` : "";
    el.textContent = `Status: ${label}${matchLabel}`;
    el.style.color = "#fff";
    el.style.background = colors[dstStatus] || "#333";
    el.style.borderRadius = "4px";
    el.style.padding = "6px 10px";
    setTimeout(() => {
        el.textContent = "";
        el.style.color = "";
        el.style.background = "";
        el.style.borderRadius = "";
        el.style.padding = "";
    }, 3000);
}

function normalizeBoardId(id) {
    return (id || "").trim().toLowerCase();
}

function sameId(a, b) {
    return String(a || "").trim().toLowerCase() === String(b || "").trim().toLowerCase();
}

async function readApiErrorResponse(response, fallbackMessage) {
    let text = "";
    try {
        text = await response.text();
    } catch {
        text = "";
    }

    let message = "";
    if (text) {
        try {
            const payload = JSON.parse(text);
            message =
                payload?.message
                || payload?.error?.message
                || payload?.error_description
                || payload?.title
                || payload?.detail
                || "";
        } catch {
            message = text;
        }
    }

    if (!message) {
        message = fallbackMessage || `HTTP ${response.status}`;
    }

    if (/token has invalid claims|token is expired/i.test(message)) {
        return "Autodarts Token ist abgelaufen. Bitte in Autodarts neu anmelden und die Boards-Seite neu laden.";
    }

    if (/autodarts-login erforderlich/i.test(message)) {
        return "API fordert Autodarts-Login. Bitte play.autodarts.io offen lassen und im Popup erneut versuchen.";
    }

    return message;
}

async function findExistingBoardByExternalId(apiBaseUrl, externalBoardId) {
    const allResp = await apiFetchWithAutodartsRetry(apiBaseUrl, `${apiBaseUrl}/api/boards`);
    if (!allResp.ok) {
        const reason = await readApiErrorResponse(allResp, "Boards konnten nicht geladen werden.");
        return { ok: false, reason };
    }

    const allBoards = await allResp.json();
    const existing = allBoards.find(b => sameId(b.externalBoardId, externalBoardId));
    if (!existing) {
        return { ok: false, reason: "Board wurde in DartSuite nicht gefunden." };
    }

    return { ok: true, board: existing };
}

async function ensureBoardRegisteredInTournament({ apiBaseUrl, extId, name, ip, tournamentId }) {
    let board = null;

    const createResp = await apiFetchWithAutodartsRetry(apiBaseUrl, `${apiBaseUrl}/api/boards`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
            externalBoardId: extId,
            name,
            localIpAddress: ip || null,
            boardManagerUrl: null
        })
    });

    if (createResp.ok) {
        board = await createResp.json();
        if (!board?.id) {
            return { extId, name, ok: false, reason: "API-Antwort ohne Board-ID." };
        }
    } else if (createResp.status === 409) {
        const existing = await findExistingBoardByExternalId(apiBaseUrl, extId);
        if (!existing.ok) {
            return { extId, name, ok: false, reason: existing.reason };
        }
        board = existing.board;
    } else {
        const reason = await readApiErrorResponse(createResp, "Board konnte nicht angelegt werden.");
        return { extId, name, ok: false, reason };
    }

    const assignUrl = `${apiBaseUrl}/api/boards/${board.id}/managed?mode=Auto&tournamentId=${encodeURIComponent(tournamentId)}`;
    const assignResp = await apiFetchWithAutodartsRetry(apiBaseUrl, assignUrl, { method: "PATCH" });
    if (!assignResp.ok) {
        const reason = await readApiErrorResponse(assignResp, "Board konnte dem Turnier nicht zugewiesen werden.");
        return { extId, name, ok: false, reason };
    }

    const verifyResp = await apiFetchWithAutodartsRetry(apiBaseUrl, `${apiBaseUrl}/api/boards/${board.id}`);
    if (!verifyResp.ok) {
        const reason = await readApiErrorResponse(verifyResp, "Board-Verifikation fehlgeschlagen.");
        return { extId, name, ok: false, reason };
    }

    const verified = await verifyResp.json();
    if (!sameId(verified?.tournamentId, tournamentId)) {
        return {
            extId,
            name,
            ok: false,
            reason: "Board angelegt, aber nicht im ausgewählten Turnier registriert."
        };
    }

    return { extId, name, ok: true, boardId: verified.id };
}

async function apiFetchWithAutodartsRetry(apiBaseUrl, url, options) {
    let response = await fetch(url, options || {});
    if (!isAutodartsLoginRequiredResponse(response)) {
        return response;
    }

    const restored = await ensureApiAutodartsSession(apiBaseUrl);
    if (!restored) {
        return response;
    }

    response = await fetch(url, options || {});
    return response;
}

function isAutodartsLoginRequiredResponse(response) {
    if (!response) return false;
    return response.status === 401;
}

async function ensureApiAutodartsSession(apiBaseUrl) {
    const tokenResult = await getFromActiveTab("getAutodartsAccessToken");
    const accessToken = tokenResult?.accessToken;
    if (!accessToken) {
        return false;
    }

    try {
        const loginResp = await fetch(`${apiBaseUrl}/api/autodarts/token-login`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ accessToken })
        });
        return loginResp.ok;
    } catch {
        return false;
    }
}
