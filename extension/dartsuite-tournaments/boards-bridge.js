// DartSuite Tournaments — Bridge (MAIN world)
// Captures board and friend data from the Autodarts API and posts it to the
// content script via window.postMessage.
// Runs in MAIN world so it shares the page's cookies/credentials.

(function () {
    const BOARDS_API = "https://api.autodarts.io/bs/v0/boards/";
    const FRIENDS_API = "https://api.autodarts.io/as/v0/users/me/friends";
    const BOARDS_PATTERN = /\/bs\/v0\/boards/;
    const FRIENDS_PATTERN = /\/as\/v0\/(friends|users\/me\/friends)/;
    const AUTODARTS_API = /api\.autodarts\.io/;
    const BOARDS_MSG_TYPE = "dartsuite-boards-response";
    const FRIENDS_MSG_TYPE = "dartsuite-friends-response";
    let boardsAlreadySent = false;
    let friendsAlreadySent = false;
    let capturedToken = null;

    console.log("[DartSuite Bridge] Loaded in MAIN world");

    // ── Helper: extract Bearer token from various header formats ──
    function extractBearerToken(headers) {
        if (!headers) return null;
        let auth = null;
        if (headers instanceof Headers) {
            auth = headers.get("Authorization");
        } else if (typeof headers === "object") {
            auth = headers["Authorization"] || headers["authorization"];
        }
        if (auth && auth.startsWith("Bearer ")) {
            return auth.substring(7);
        }
        return null;
    }

    function onTokenCaptured(token, source) {
        if (!token || capturedToken) return;
        capturedToken = token;
        console.log("[DartSuite Bridge] Captured Bearer token via " + source);
        // If we're on /boards and haven't sent boards yet, try fetching now
        if (location.pathname.startsWith("/boards") && !boardsAlreadySent) {
            fetchBoardsDirectly();
        }
    }

    function onBoardsIntercepted(data) {
        if (!Array.isArray(data) || data.length === 0 || boardsAlreadySent) return;
        boardsAlreadySent = true;
        console.log("[DartSuite Bridge] Captured", data.length, "boards from intercepted request");
        window.postMessage({ type: BOARDS_MSG_TYPE, boards: data }, "*");
    }

    function onFriendsIntercepted(data) {
        if (!Array.isArray(data) || friendsAlreadySent) return;
        friendsAlreadySent = true;
        console.log("[DartSuite Bridge] Captured", data.length, "friends from intercepted request");
        if (data.length > 0) console.log("[DartSuite Bridge] Friend sample keys:", Object.keys(data[0]));
        window.postMessage({ type: FRIENDS_MSG_TYPE, friends: data }, "*");
    }

    // ── Strategy 1a: Hook window.fetch ──
    const originalFetch = window.fetch;
    window.fetch = async function (...args) {
        try {
            const input = args[0];
            const init = args[1] || (typeof input !== "string" ? input : null);
            const url = typeof input === "string" ? input : input?.url;

            // Capture token from any Autodarts API request
            if (url && AUTODARTS_API.test(url)) {
                const token = extractBearerToken(init?.headers);
                if (token) onTokenCaptured(token, "fetch");
            }
        } catch { /* ignore */ }

        const response = await originalFetch.apply(this, args);

        try {
            const url = typeof args[0] === "string" ? args[0] : args[0]?.url;
            if (url && BOARDS_PATTERN.test(url) && response.ok) {
                const clone = response.clone();
                const data = await clone.json();
                onBoardsIntercepted(data);
            }
            if (url && FRIENDS_PATTERN.test(url) && response.ok) {
                const clone = response.clone();
                const data = await clone.json();
                const items = Array.isArray(data) ? data : data?.items || [];
                onFriendsIntercepted(items);
            }
        } catch { /* ignore */ }

        return response;
    };

    // ── Strategy 1b: Hook XMLHttpRequest ──
    const xhrOpen = XMLHttpRequest.prototype.open;
    const xhrSetRequestHeader = XMLHttpRequest.prototype.setRequestHeader;
    const xhrSend = XMLHttpRequest.prototype.send;

    XMLHttpRequest.prototype.open = function (method, url, ...rest) {
        this._dartsuiteUrl = typeof url === "string" ? url : url?.toString();
        return xhrOpen.call(this, method, url, ...rest);
    };

    XMLHttpRequest.prototype.setRequestHeader = function (name, value) {
        if ((name === "Authorization" || name === "authorization") &&
            value?.startsWith("Bearer ") &&
            this._dartsuiteUrl && AUTODARTS_API.test(this._dartsuiteUrl)) {
            onTokenCaptured(value.substring(7), "XHR");
        }
        return xhrSetRequestHeader.call(this, name, value);
    };

    XMLHttpRequest.prototype.send = function (...args) {
        if (this._dartsuiteUrl && BOARDS_PATTERN.test(this._dartsuiteUrl)) {
            this.addEventListener("load", function () {
                try {
                    if (this.status >= 200 && this.status < 300) {
                        const data = JSON.parse(this.responseText);
                        onBoardsIntercepted(data);
                    }
                } catch { /* ignore */ }
            });
        }
        if (this._dartsuiteUrl && FRIENDS_PATTERN.test(this._dartsuiteUrl)) {
            this.addEventListener("load", function () {
                try {
                    if (this.status >= 200 && this.status < 300) {
                        const data = JSON.parse(this.responseText);
                        const items = Array.isArray(data) ? data : data?.items || [];
                        onFriendsIntercepted(items);
                    }
                } catch { /* ignore */ }
            });
        }
        return xhrSend.apply(this, args);
    };

    // ── Strategy 2: Look for Keycloak adapter on window ──
    function findKeycloakToken() {
        // keycloak-js adapter typically lives on a global
        const candidates = [
            window.keycloak,
            window.Keycloak,
            window._keycloak,
            window.__keycloak,
            window.kc,
        ];
        for (const kc of candidates) {
            if (kc?.token && typeof kc.token === "string" && kc.token.startsWith("eyJ")) {
                return kc.token;
            }
        }
        return null;
    }

    // ── Strategy 3: Scan storage for tokens ──
    function findStorageToken() {
        for (const storage of [localStorage, sessionStorage]) {
            try {
                for (let i = 0; i < storage.length; i++) {
                    const key = storage.key(i);
                    const val = storage.getItem(key);
                    if (!val) continue;
                    // Direct JWT
                    if (val.startsWith("eyJ") && val.includes(".") && val.length > 100) {
                        return { key, token: val };
                    }
                    // JSON with access_token
                    if (val.includes("access_token")) {
                        try {
                            const parsed = JSON.parse(val);
                            if (parsed?.access_token?.startsWith("eyJ")) {
                                return { key, token: parsed.access_token };
                            }
                        } catch { /* not JSON */ }
                    }
                }
            } catch { /* storage error */ }
        }
        return null;
    }

    // ── Diagnostic: log storage keys once to help debug ──
    function logStorageDiagnostics() {
        const keys = { local: [], session: [] };
        try {
            for (let i = 0; i < localStorage.length; i++) keys.local.push(localStorage.key(i));
        } catch { /* */ }
        try {
            for (let i = 0; i < sessionStorage.length; i++) keys.session.push(sessionStorage.key(i));
        } catch { /* */ }
        console.log("[DartSuite Bridge] localStorage keys:", keys.local.join(", ") || "(empty)");
        console.log("[DartSuite Bridge] sessionStorage keys:", keys.session.join(", ") || "(empty)");

        const kcToken = findKeycloakToken();
        if (kcToken) console.log("[DartSuite Bridge] Found Keycloak adapter token on window");
        else console.log("[DartSuite Bridge] No Keycloak adapter found on window");
    }

    // ── Active fetch using captured/found token ──
    function findAccessToken() {
        if (capturedToken) return capturedToken;
        const kcToken = findKeycloakToken();
        if (kcToken) return kcToken;
        const storageResult = findStorageToken();
        if (storageResult) {
            console.log("[DartSuite Bridge] Using token from storage key:", storageResult.key);
            return storageResult.token;
        }
        return null;
    }

    async function fetchBoardsDirectly() {
        if (boardsAlreadySent) return;

        const token = findAccessToken();
        if (!token) {
            console.warn("[DartSuite Bridge] No access token available yet — waiting for app to make an API call");
            return;
        }

        try {
            const response = await originalFetch(BOARDS_API, {
                method: "GET",
                headers: {
                    "Accept": "application/json",
                    "Authorization": "Bearer " + token
                }
            });

            if (!response.ok) {
                console.warn("[DartSuite Bridge] Direct boards fetch failed:", response.status);
                if (response.status === 401) capturedToken = null; // Token expired, clear it
                return;
            }

            const data = await response.json();
            onBoardsIntercepted(data);
        } catch (e) {
            console.warn("[DartSuite Bridge] Direct fetch error:", e.message);
        }
    }

    async function fetchFriendsDirectly() {
        if (friendsAlreadySent) return;

        const token = findAccessToken();
        if (!token) {
            console.warn("[DartSuite Bridge] No access token available for friends fetch");
            return;
        }

        try {
            const response = await originalFetch(FRIENDS_API, {
                method: "GET",
                headers: {
                    "Accept": "application/json",
                    "Authorization": "Bearer " + token
                }
            });

            if (!response.ok) {
                console.warn("[DartSuite Bridge] Direct friends fetch failed:", response.status);
                if (response.status === 401) capturedToken = null;
                return;
            }

            const data = await response.json();
            const items = Array.isArray(data) ? data : data?.items || [];
            onFriendsIntercepted(items);
        } catch (e) {
            console.warn("[DartSuite Bridge] Direct friends fetch error:", e.message);
        }
    }

    // ── Navigation watcher ──
    let lastPath = location.pathname;
    function checkNavigation() {
        if (location.pathname !== lastPath) {
            lastPath = location.pathname;
            boardsAlreadySent = false;
            friendsAlreadySent = false;
        }
        if (location.pathname.startsWith("/boards") && !boardsAlreadySent) {
            fetchBoardsDirectly();
        }
    }

    // Run diagnostics + first check after page loads
    function onReady() {
        logStorageDiagnostics();
        setTimeout(checkNavigation, 2000);
    }

    if (document.readyState === "complete") {
        onReady();
    } else {
        window.addEventListener("load", onReady);
    }

    // Periodic navigation check (every 3 seconds)
    setInterval(checkNavigation, 3000);

    // Listen for content script requesting boards or friends
    window.addEventListener("message", (event) => {
        if (event.source !== window) return;
        if (event.data?.type === "dartsuite-request-boards") {
            boardsAlreadySent = false;
            fetchBoardsDirectly();
        }
        if (event.data?.type === "dartsuite-request-friends") {
            friendsAlreadySent = false;
            fetchFriendsDirectly();
        }
    });
})();
