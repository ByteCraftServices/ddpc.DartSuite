// DartSuite Tournaments — Popup v0.3.0
// Tournament selection, board management, friends import, settings.

const DEFAULT_API_BASE_URL = "http://localhost:5290";
const SETTINGS_KEYS = {
    apiBaseUrl: DEFAULT_API_BASE_URL,
    defaultHost: "",
    hostInput: "",
    tournamentId: "",
    autoManaged: true,
    fullscreen: false
};

let currentTournament = null;

// ─── Initialization ───

document.addEventListener("DOMContentLoaded", async () => {
    await loadSettings();
    setupTabs();
    setupEventListeners();
    await checkApiStatus();
    await loadTournamentState();
});

// ─── Tab Navigation ───

function setupTabs() {
    for (const btn of document.querySelectorAll(".tabs button")) {
        btn.addEventListener("click", () => {
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
    try {
        const response = await fetch(`${apiBaseUrl}/api/boards`, { signal: AbortSignal.timeout(3000) });
        if (response.ok) {
            setDot("apiDot", "online");
            document.getElementById("apiStatus").textContent = "DartSuite API: online";
            chrome.runtime.sendMessage({ action: "setIconStatus", status: "configured" });
        } else {
            setDot("apiDot", "offline");
            document.getElementById("apiStatus").textContent = `API: Fehler (${response.status})`;
            chrome.runtime.sendMessage({ action: "setIconStatus", status: "error" });
        }
    } catch {
        setDot("apiDot", "offline");
        document.getElementById("apiStatus").textContent = "DartSuite API: nicht erreichbar";
        chrome.runtime.sendMessage({ action: "setIconStatus", status: "error" });
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
            showActiveTournament(tournament);
            chrome.runtime.sendMessage({ action: "setIconStatus", status: "connected" });
            chrome.runtime.sendMessage({ action: "tournamentSelected", tournament });
        } else if (response.status === 404) {
            errEl.textContent = "Kein Turnier mit diesem Code gefunden.";
            errEl.style.display = "block";
            chrome.runtime.sendMessage({ action: "setIconStatus", status: "warning" });
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
        showActiveTournament(tournament);
        chrome.runtime.sendMessage({ action: "setIconStatus", status: "connected" });
        chrome.runtime.sendMessage({ action: "tournamentSelected", tournament });
    } catch {
        setMessage("Fehler beim Laden des Turniers.");
    }
}

function showActiveTournament(tournament) {
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
        loadActiveBoardOptions();
    } else {
        const boardSection = document.getElementById("boardSelectSection");
        if (boardSection) boardSection.style.display = "none";
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
                showActiveTournament(tournament);
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

    if (localBoards.length === 0) {
        list.innerHTML = '<div class="info">Keine lokalen Boards gefunden. Bitte play.autodarts.io/boards öffnen.</div>';
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
        dsMap.set(b.externalBoardId, b);
    }

    // Check current tournament boards
    const tournamentId = currentTournament?.id;

    list.innerHTML = "";
    let hasTransferable = false;

    for (const board of localBoards) {
        const boardId = board.id || board.externalBoardId;
        const boardName = board.name || boardId;
        const boardIp = board.ip || board.localIpAddress || "";
        const dsBoard = dsMap.get(boardId);
        const isInTournament = dsBoard && dsBoard.tournamentId && tournamentId &&
            dsBoard.tournamentId === tournamentId;

        const item = document.createElement("div");
        item.className = "board-item";
        item.style.flexWrap = "wrap";

        if (isInTournament) {
            // Board is already in the tournament — show Teilnehmen/Verlassen
            const isManaged = dsBoard.managedMode === "Auto";
            const reachable = boardIp ? await pingBoard(boardIp) : false;

            item.innerHTML = `
                <div style="flex:1">
                    <div class="board-name">${escapeHtml(boardName)}</div>
                    <div class="board-status" style="color:#4caf50;font-size:10px">Im Turnier | ${dsBoard.managedMode}</div>
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
            item.innerHTML = `
                <div class="checkbox-row" style="flex:1">
                    <input type="checkbox" data-board-ext-id="${escapeHtml(boardId)}"
                           data-board-name="${escapeHtml(boardName)}"
                           data-board-ip="${escapeHtml(boardIp)}" />
                    <div>
                        <div class="board-name">${escapeHtml(boardName)}</div>
                        <div class="board-status" style="color:#aaa;font-size:10px">
                            ${inDartSuite ? 'In DartSuite' : 'Neu'} | ${boardIp || 'Keine IP'}
                        </div>
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
    for (const cb of checked) {
        const extId = cb.dataset.boardExtId;
        const name = cb.dataset.boardName;
        const ip = cb.dataset.boardIp;
        try {
            const response = await fetch(`${apiBaseUrl}/api/boards`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    externalBoardId: extId,
                    name: name,
                    localIpAddress: ip || null,
                    boardManagerUrl: null
                })
            });
            if (response.ok || response.status === 409) transferred++;
        } catch { /* skip */ }
    }
    setMessage(`${transferred} Board(s) an DartSuite übergeben.`);
    await loadBoards(); // Refresh
}

async function setManagedMode(boardId, mode) {
    const apiBaseUrl = getApiBaseUrl();
    const tournamentId = currentTournament?.id || "";
    const tournamentName = currentTournament?.name || "";
    const host = currentTournament?.organizerAccount || "";
    try {
        const url = `${apiBaseUrl}/api/boards/${boardId}/managed?mode=${mode}` +
            (mode === "Auto" && tournamentId ? `&tournamentId=${tournamentId}` : "");
        const response = await fetch(url, { method: "PATCH" });
        if (response.ok) {
            // Resolve board name from the boards list
            let boardName = boardId;
            try {
                const bResp = await fetch(`${apiBaseUrl}/api/boards`);
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
        }
    } catch {
        setMessage("Fehler beim Setzen des Managed Mode.");
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
            if (response.ok || response.status === 409) imported++;
        } catch { /* skip */ }
    }
    setMessage(`${imported} Freunde importiert.`);
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

    // Restore previously selected board
    const stored = await chrome.storage.sync.get(["managedBoardId"]);
    if (stored.managedBoardId) {
        boardSelect.value = stored.managedBoardId;
    }

    // Show warning if no board selected yet
    updateBoardSelectionHint(boardSelect);

    // On change: save selection and notify both content script AND background
    boardSelect.addEventListener("change", async () => {
        const selectedValue = boardSelect.value;
        const selectedOption = boardSelect.selectedOptions[0];
        const boardName = selectedOption?.dataset.name || selectedValue;
        updateBoardSelectionHint(boardSelect);
        if (selectedValue && currentTournament) {
            await chrome.storage.sync.set({ managedBoardId: selectedValue, managedBoardName: boardName });
            // Notify content script with DartSuite internal board ID
            const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
            if (tab?.id && tab.url?.startsWith("https://play.autodarts.io/")) {
                chrome.tabs.sendMessage(tab.id, {
                    action: "setManagedMode",
                    payload: {
                        mode: "Auto",
                        boardId: selectedValue,
                        tournamentId: currentTournament.id,
                        tournamentName: currentTournament.name,
                        host: currentTournament.organizerAccount,
                        boardName: boardName
                    }
                });
            }
            // Notify background to start polling
            chrome.runtime.sendMessage({
                action: "managedModeChanged",
                boardId: selectedValue,
                mode: "Auto",
                tournamentId: currentTournament.id,
                tournamentName: currentTournament.name,
                host: currentTournament.organizerAccount,
                boardName: boardName
            });
            setMessage("Board ausgewählt: " + boardName);
        } else if (!selectedValue) {
            // Board deselected
            await chrome.storage.sync.remove(["managedBoardId", "managedBoardName"]);
        }
    });

    // Refresh button — manually re-fetch schedule
    if (refreshBtn) {
        refreshBtn.addEventListener("click", async () => {
            refreshBtn.disabled = true;
            refreshBtn.textContent = "⟳";
            // Trigger content script to re-poll schedule
            const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
            if (tab?.id && tab.url?.startsWith("https://play.autodarts.io/")) {
                await chrome.tabs.sendMessage(tab.id, { action: "refreshSchedule" });
            }
            setTimeout(() => { refreshBtn.disabled = false; refreshBtn.textContent = "🔄"; }, 2000);
        });
    }
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
    document.getElementById("apiBaseUrl").value = stored.apiBaseUrl || DEFAULT_API_BASE_URL;
    document.getElementById("defaultHost").value = stored.defaultHost || "";
    document.getElementById("autoManaged").checked = stored.autoManaged !== false;
    document.getElementById("fullscreen").checked = !!stored.fullscreen;

    // Apply defaults
    if (stored.defaultHost && !stored.hostInput) {
        document.getElementById("hostInput").value = stored.defaultHost;
    }
}

async function saveSettings() {
    const settings = {
        apiBaseUrl: document.getElementById("apiBaseUrl").value.trim() || DEFAULT_API_BASE_URL,
        defaultHost: document.getElementById("defaultHost").value.trim(),
        autoManaged: document.getElementById("autoManaged").checked,
        fullscreen: document.getElementById("fullscreen").checked
    };
    await chrome.storage.sync.set(settings);
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
    const activeTab = document.querySelector(".tabs button.active");
    if (!activeTab) return;
    const tab = activeTab.dataset.tab;
    if (tab === "boards") loadBoards();
    if (tab === "friends") loadFriends();
});
tabObserver.observe(document.querySelector(".tabs") || document.body, { subtree: true, attributes: true, attributeFilter: ["class"] });

// ─── UI Helpers ───

function setDot(id, status) {
    const el = document.getElementById(id);
    if (el) el.className = `dot ${status}`;
}

function setMessage(text) {
    const el = document.getElementById("statusMessage");
    if (el) {
        el.textContent = text;
        setTimeout(() => { el.textContent = ""; }, 5000);
    }
}

function escapeHtml(str) {
    if (!str) return "";
    const div = document.createElement("div");
    div.textContent = str;
    return div.innerHTML;
}