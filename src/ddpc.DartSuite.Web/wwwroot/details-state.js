(function () {
    "use strict";

    var STORAGE_PREFIX = "dartsuite.details.open";
    var currentPath = location.pathname;
    var restoreScheduled = false;

    function storageAvailable() {
        try {
            var k = "__dartsuite_details_test__";
            localStorage.setItem(k, "1");
            localStorage.removeItem(k);
            return true;
        } catch {
            return false;
        }
    }

    function detailsKey(details) {
        return STORAGE_PREFIX + "." + location.pathname + "." + details.id;
    }

    function restoreDetailsState(root) {
        if (!storageAvailable()) {
            return;
        }

        var scope = root && root.querySelectorAll ? root : document;
        var detailsNodes = scope.querySelectorAll("details[id]");
        for (var i = 0; i < detailsNodes.length; i++) {
            var details = detailsNodes[i];
            var raw = localStorage.getItem(detailsKey(details));
            if (raw !== "true" && raw !== "false") {
                continue;
            }

            var shouldBeOpen = raw === "true";
            if (details.open === shouldBeOpen) {
                continue;
            }

            details.dataset.restoringState = "1";
            details.open = shouldBeOpen;
            setTimeout(function (node) {
                return function () {
                    delete node.dataset.restoringState;
                };
            }(details), 0);
        }
    }

    function scheduleRestore() {
        if (restoreScheduled) {
            return;
        }

        restoreScheduled = true;
        requestAnimationFrame(function () {
            restoreScheduled = false;
            restoreDetailsState(document);
        });
    }

    function onDetailsToggle(event) {
        var target = event.target;
        if (!target || target.tagName !== "DETAILS" || !target.id) {
            return;
        }

        if (target.dataset.restoringState === "1") {
            return;
        }

        if (!storageAvailable()) {
            return;
        }

        localStorage.setItem(detailsKey(target), target.open ? "true" : "false");
    }

    function observeDomChanges() {
        var observer = new MutationObserver(function () {
            scheduleRestore();
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    function observeRouteChanges() {
        setInterval(function () {
            if (currentPath === location.pathname) {
                return;
            }

            currentPath = location.pathname;
            scheduleRestore();
        }, 300);
    }

    function init() {
        restoreDetailsState(document);
        document.addEventListener("toggle", onDetailsToggle, true);
        observeDomChanges();
        observeRouteChanges();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init, { once: true });
    } else {
        init();
    }
})();
