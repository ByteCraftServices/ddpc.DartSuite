// DS-038: Disabled button tooltip support (click/touch), without restarting Blazor.

(function () {
    if (window.dartSuiteTooltipInitialized) {
        return;
    }
    window.dartSuiteTooltipInitialized = true;

    const state = {
        dismissTimer: null
    };

    function ensureTitle(button) {
        if (!button.getAttribute("title")) {
            const fallback = button.getAttribute("aria-label") || button.textContent?.trim() || "Button ist deaktiviert";
            button.setAttribute("title", fallback);
        }
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

    document.addEventListener("mouseenter", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }
        const button = target.closest("button[disabled], [role='button'][disabled]");
        if (button) {
            ensureTitle(button);
        }
    }, { capture: true });

    document.addEventListener("click", (event) => handleInteraction(event, false), { capture: true });
    document.addEventListener("touchend", (event) => handleInteraction(event, true), { capture: true });

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
})();
