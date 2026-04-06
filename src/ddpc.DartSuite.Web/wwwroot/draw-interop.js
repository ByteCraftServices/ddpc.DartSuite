window.dartSuiteDraw = {
    getRelativeCenter: function (containerId, elementId) {
        const container = document.getElementById(containerId);
        const element = document.getElementById(elementId);
        if (!container || !element) {
            return null;
        }

        const c = container.getBoundingClientRect();
        const e = element.getBoundingClientRect();

        return {
            Left: e.left - c.left + (e.width / 2),
            Top: e.top - c.top + (e.height / 2)
        };
    }
};

window.dartSuiteUi = window.dartSuiteUi || {
    isDocumentVisible: function () {
        return document.visibilityState === "visible";
    },
    isCompactViewport: function () {
        return window.matchMedia("(max-width: 640.98px)").matches;
    },
    localStorageGet: function (key) {
        try { return localStorage.getItem(key); } catch (e) { return null; }
    },
    localStorageSet: function (key, value) {
        try { localStorage.setItem(key, value); } catch (e) { }
    },
    initDetailsStorage: function (id, key) {
        const el = document.getElementById(id);
        if (!el) return;
        try {
            const stored = localStorage.getItem(key);
            if (stored !== null) el.open = (stored === "true");
        } catch (e) { }
        if (!el._dartsuiteStorageInit) {
            el._dartsuiteStorageInit = true;
            el.addEventListener("toggle", function () {
                try { localStorage.setItem(key, el.open.toString()); } catch (e) { }
            });
        }
    }
};
