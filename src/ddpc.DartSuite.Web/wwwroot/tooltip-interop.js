// DS-038: Disabled button tooltip support (click/touch), without restarting Blazor.

(function () {
    if (window.dartSuiteTooltipInitialized) {
        return;
    }
    window.dartSuiteTooltipInitialized = true;

    const state = {
        dismissTimer: null,
        hydrationQueued: false,
        observer: null
    };

    const statusTooltipMap = {
        "aktiv": "Aktiv: Match oder Turnier laeuft gerade.",
        "gestartet": "Gestartet: Match laeuft gerade.",
        "geplant": "Geplant: Termin steht, Start ist noch offen.",
        "erstellt": "Erstellt: Eintrag angelegt, wartet auf weitere Schritte.",
        "beendet": "Beendet: Vorgang wurde abgeschlossen.",
        "warten": "Warten: Es wird auf ein vorheriges Ergebnis oder eine Freigabe gewartet.",
        "walkover": "Walkover: Match wurde kampflos gewertet.",
        "ontime": "OnTime: Zeitplan ohne bekannte Verzoegerung.",
        "delayed": "Delayed: Zeitplan ist verzoegert.",
        "ahead": "Ahead: Zeitplan liegt vor der geplanten Zeit.",
        "verbunden": "Verbunden: Verbindung ist aktiv.",
        "getrennt": "Getrennt: Verbindung ist aktuell nicht aktiv.",
        "online": "Online: System ist erreichbar.",
        "offline": "Offline: System ist derzeit nicht erreichbar."
    };

    function ensureTitle(button) {
        if (!button.getAttribute("title")) {
            const fallback = button.getAttribute("aria-label") || button.textContent?.trim() || "Button ist deaktiviert";
            button.setAttribute("title", fallback);
        }
    }

    function ensureAriaLabel(button) {
        if (button.hasAttribute("aria-label")) {
            return;
        }

        const title = button.getAttribute("title")?.trim();
        if (title) {
            button.setAttribute("aria-label", title);
            return;
        }

        const text = (button.textContent || "").trim().replace(/\s+/g, " ");
        if (text.length > 0) {
            button.setAttribute("aria-label", text);
        }
    }

    function deriveBadgeTooltip(badge) {
        const text = (badge.textContent || "").trim();
        if (!text) {
            return null;
        }

        if (/^S\s+\d+/i.test(text)) {
            return "Sets: Aktueller Set-Stand dieses Spielers oder Teams.";
        }

        if (/^L\s+\d+/i.test(text)) {
            return "Legs: Aktueller Leg-Stand dieses Spielers oder Teams.";
        }

        if (/^\d+\s*:\s*\d+$/.test(text)) {
            return "Spielstand: Aktuelles oder finales Ergebnis.";
        }

        if (/^live$/i.test(text)) {
            return "Live: Daten werden in Echtzeit aktualisiert.";
        }

        if (/^am\s+zug$/i.test(text)) {
            return "Aktiver Spieler: Diese Seite ist aktuell am Zug.";
        }

        if (text.includes("🔒") || /gesperrt/i.test(text)) {
            return "Gesperrt: Dieser Bereich ist aktuell gegen Aenderungen geschuetzt.";
        }

        const normalized = text.toLowerCase();
        if (statusTooltipMap[normalized]) {
            return statusTooltipMap[normalized];
        }

        return `Info: ${text}`;
    }

    function hydrateAccessibility(root) {
        const scope = root instanceof Element ? root : document;

        scope.querySelectorAll("button, [role='button']").forEach((button) => {
            if (!(button instanceof HTMLElement)) {
                return;
            }
            ensureAriaLabel(button);
            if (button.hasAttribute("disabled")) {
                ensureTitle(button);
            }
        });

        scope.querySelectorAll(".badge:not([title])").forEach((badge) => {
            if (!(badge instanceof HTMLElement)) {
                return;
            }

            const tooltip = deriveBadgeTooltip(badge);
            if (tooltip) {
                badge.setAttribute("title", tooltip);
            }
        });
    }

    function queueHydration() {
        if (state.hydrationQueued) {
            return;
        }

        state.hydrationQueued = true;
        requestAnimationFrame(() => {
            state.hydrationQueued = false;
            hydrateAccessibility(document);
        });
    }

    function showToast(message, isTouch) {
        const toast = document.getElementById("tooltipToast");
        const body = document.getElementById("tooltipBody");
        if (!toast || !body) {
            return;
        }

        body.textContent = message;
        toast.classList.add("show");
        toast.style.display = "block";

        if (isTouch) {
            clearTimeout(state.dismissTimer);
            state.dismissTimer = setTimeout(() => {
                hideToast();
            }, 5000);
        }
    }

    function hideToast() {
        const toast = document.getElementById("tooltipToast");
        if (!toast) {
            return;
        }
        toast.classList.remove("show");
        toast.style.display = "none";
        clearTimeout(state.dismissTimer);
    }

    function handleInteraction(event, isTouch) {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const button = target.closest("button[disabled], .dropdown-item[disabled], [role='button'][disabled]");
        if (!button) {
            return;
        }

        ensureTitle(button);
        const message = button.getAttribute("title") || "Button ist deaktiviert";

        if (button.classList.contains("dropdown-item")) {
            event.preventDefault();
            event.stopPropagation();
        }

        showToast(message, isTouch);
    }

    function handleKeyboardInteraction(event) {
        const isActionKey = event.key === "Enter" || event.key === " ";
        if (!isActionKey) {
            return;
        }

        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const button = target.closest("button[disabled], .dropdown-item[disabled], [role='button'][disabled]");
        if (!button) {
            return;
        }

        ensureTitle(button);
        const message = button.getAttribute("title") || "Button ist deaktiviert";
        event.preventDefault();
        event.stopPropagation();
        showToast(message, false);
    }

    document.addEventListener("mouseenter", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }
        if (target.matches("button, [role='button']")) {
            ensureAriaLabel(target);
        }
        const button = target.closest("button[disabled], [role='button'][disabled]");
        if (button) {
            ensureTitle(button);
        }
    }, { capture: true });

    document.addEventListener("click", (event) => handleInteraction(event, false), { capture: true });
    document.addEventListener("touchend", (event) => handleInteraction(event, true), { capture: true });
    document.addEventListener("keydown", handleKeyboardInteraction, { capture: true });

    document.addEventListener("click", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }
        if (target.id === "tooltipCloseBtn") {
            hideToast();
        }
    }, { capture: true });

    window.dartSuiteTooltip = {
        showTooltip: (message) => showToast(message || "Button ist deaktiviert", false),
        hideTooltip: () => hideToast()
    };

    hydrateAccessibility(document);

    state.observer = new MutationObserver((mutations) => {
        for (const mutation of mutations) {
            if (mutation.type === "childList" && mutation.addedNodes.length > 0) {
                queueHydration();
                return;
            }
        }
    });

    state.observer.observe(document.body, { childList: true, subtree: true });
})();
