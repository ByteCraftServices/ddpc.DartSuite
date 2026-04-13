window.dartSuiteDraw = {
    _flipSnapshots: {},

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
    },

    captureListPositions: function (containerId, itemSelector) {
        const container = document.getElementById(containerId);
        if (!container || !itemSelector) {
            return;
        }

        const snapshot = {};
        container.querySelectorAll(itemSelector).forEach((element) => {
            const key = element.dataset.flipKey || element.id;
            if (!key) {
                return;
            }

            const rect = element.getBoundingClientRect();
            snapshot[key] = {
                left: rect.left,
                top: rect.top
            };
        });

        this._flipSnapshots[containerId] = snapshot;
    },

    playCapturedListAnimation: function (containerId, itemSelector, durationMs) {
        const container = document.getElementById(containerId);
        const before = this._flipSnapshots[containerId];
        if (!container || !itemSelector || !before) {
            return;
        }

        const duration = Number.isFinite(durationMs) ? durationMs : 320;
        const easing = "cubic-bezier(.22,.61,.36,1)";

        container.querySelectorAll(itemSelector).forEach((element) => {
            const key = element.dataset.flipKey || element.id;
            if (!key || !before[key]) {
                return;
            }

            const currentRect = element.getBoundingClientRect();
            const dx = before[key].left - currentRect.left;
            const dy = before[key].top - currentRect.top;

            if (Math.abs(dx) < 1 && Math.abs(dy) < 1) {
                return;
            }

            element.style.transition = "none";
            element.style.transform = "translate(" + dx + "px, " + dy + "px)";
            void element.offsetWidth;
            element.style.transition = "transform " + duration + "ms " + easing;
            element.style.transform = "translate(0, 0)";

            window.setTimeout(() => {
                element.style.transition = "";
                element.style.transform = "";
            }, duration + 40);
        });

        delete this._flipSnapshots[containerId];
    }
};

window.dartSuiteUi = window.dartSuiteUi || {
    _swipeHandlers: {},
    _touchDnDHandlers: {},
    _outsideClickHandlers: {},
    _savedScrollY: 0,
    saveScrollY: function () {
        this._savedScrollY = window.scrollY;
    },
    restoreScrollY: function () {
        window.scrollTo({ top: this._savedScrollY, behavior: "instant" });
    },
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
        // Always update the active storage key (supports tournament change on same element)
        el._dartsuiteStorageKey = key;
        // Apply the stored state for the given key
        try {
            const stored = localStorage.getItem(key);
            if (stored !== null) el.open = (stored === "true");
        } catch (e) { }
        // Register toggle listener once; handler reads the current key from the element
        if (!el._dartsuiteStorageInit) {
            el._dartsuiteStorageInit = true;
            el.addEventListener("toggle", function () {
                try { localStorage.setItem(el._dartsuiteStorageKey, el.open.toString()); } catch (e) { }
            });
        }
    },
    collapseAllTournamentSettingsPanels: function (tournamentId) {
        // tournamentId must be in 'N' format (no hyphens, lowercase) to match localStorage keys
        const prefix = "ds-spanel-" + tournamentId + "-";
        // Persist collapsed state for all matching keys in localStorage
        try {
            const keys = Object.keys(localStorage).filter(function (k) { return k.startsWith(prefix); });
            keys.forEach(function (k) { localStorage.setItem(k, "false"); });
        } catch (e) { }
        // Collapse any currently mounted <details> elements managed by this storage
        document.querySelectorAll("details").forEach(function (el) {
            if (el._dartsuiteStorageKey && el._dartsuiteStorageKey.startsWith(prefix)) {
                el.open = false;
            }
        });
    },
    registerHorizontalSwipe: function (elementId, dotNetRef, callbackMethodName) {
        const el = document.getElementById(elementId);
        if (!el || !dotNetRef || !callbackMethodName) return;

        this.unregisterHorizontalSwipe(elementId);

        const state = {
            x0: 0,
            y0: 0,
            tracking: false
        };

        const onStart = (ev) => {
            if (!ev.touches || ev.touches.length !== 1) return;
            state.x0 = ev.touches[0].clientX;
            state.y0 = ev.touches[0].clientY;
            state.tracking = true;
        };

        const onEnd = (ev) => {
            if (!state.tracking || !ev.changedTouches || ev.changedTouches.length !== 1) return;
            state.tracking = false;

            const x1 = ev.changedTouches[0].clientX;
            const y1 = ev.changedTouches[0].clientY;
            const dx = x1 - state.x0;
            const dy = y1 - state.y0;

            if (Math.abs(dx) < 45 || Math.abs(dx) <= Math.abs(dy)) return;

            const direction = dx < 0 ? "left" : "right";
            try {
                const invokeResult = dotNetRef.invokeMethodAsync(callbackMethodName, direction);
                if (invokeResult && typeof invokeResult.catch === "function") {
                    invokeResult.catch(() => { });
                }
            } catch (e) {
                // Connection may already be closed; ignore best-effort callback.
            }
        };

        el.addEventListener("touchstart", onStart, { passive: true });
        el.addEventListener("touchend", onEnd, { passive: true });

        this._swipeHandlers[elementId] = { el, onStart, onEnd };
    },
    unregisterHorizontalSwipe: function (elementId) {
        const reg = this._swipeHandlers[elementId];
        if (!reg) return;

        try {
            reg.el.removeEventListener("touchstart", reg.onStart);
            reg.el.removeEventListener("touchend", reg.onEnd);
        } catch (e) { }

        delete this._swipeHandlers[elementId];
    },
    registerTouchDragDrop: function (elementId) {
        const root = document.getElementById(elementId);
        if (!root) return;

        this.unregisterTouchDragDrop(elementId);

        const LONG_PRESS_MS = 220;
        const MOVE_CANCEL_PX = 18;
        const DRAG_START_CANCEL_PX = 6;
        const AUTO_SCROLL_EDGE_PX = 72;
        const AUTO_SCROLL_STEP_PX = 20;

        const state = {
            timer: null,
            sourceEl: null,
            sourceWasDraggable: false,
            dropEl: null,
            ghostEl: null,
            touchId: null,
            startX: 0,
            startY: 0,
            startAtMs: 0,
            dragging: false,
            suppressContextMenu: false
        };

        const clearTimer = () => {
            if (state.timer) {
                clearTimeout(state.timer);
                state.timer = null;
            }
        };

        const reset = () => {
            clearTimer();
            if (state.ghostEl && state.ghostEl.parentNode) {
                state.ghostEl.parentNode.removeChild(state.ghostEl);
            }
            if (state.sourceEl) {
                state.sourceEl.draggable = state.sourceWasDraggable;
            }
            try {
                document.body.style.touchAction = "";
                document.body.style.overscrollBehavior = "";
                document.body.style.webkitUserSelect = "";
                document.body.style.userSelect = "";
                document.body.style.webkitTouchCallout = "";
            } catch (e) {
                // best effort only
            }
            state.sourceEl = null;
            state.sourceWasDraggable = false;
            state.dropEl = null;
            state.ghostEl = null;
            state.touchId = null;
            state.startAtMs = 0;
            state.dragging = false;
            state.suppressContextMenu = false;
        };

        const beginDragIfPossible = () => {
            if (state.dragging || !state.sourceEl) {
                return;
            }

            clearTimer();
            state.dragging = true;
            try {
                document.body.style.touchAction = "none";
                document.body.style.overscrollBehavior = "none";
                document.body.style.webkitUserSelect = "none";
                document.body.style.userSelect = "none";
                document.body.style.webkitTouchCallout = "none";
            } catch (e) {
                // best effort only
            }
            state.ghostEl = createGhost(state.sourceEl, state.startX, state.startY);
            fire(state.sourceEl, "dragstart");
        };

        const findTouch = (touchList) => {
            if (!touchList || touchList.length === 0) return null;

            if (state.touchId !== null) {
                const stateTouchId = Number(state.touchId);
                for (let i = 0; i < touchList.length; i++) {
                    if (Number(touchList[i].identifier) === stateTouchId) {
                        return touchList[i];
                    }
                }
            }

            // Fallback for browsers/devices where identifier mapping is unstable.
            if (touchList.length === 1) {
                return touchList[0];
            }

            return null;
        };

        const createGhost = (source, x, y) => {
            const ghost = source.cloneNode(true);
            ghost.style.position = "fixed";
            ghost.style.left = (x + 10) + "px";
            ghost.style.top = (y + 10) + "px";
            ghost.style.zIndex = "2000";
            ghost.style.pointerEvents = "none";
            ghost.style.opacity = "0.85";
            ghost.style.maxWidth = "80vw";
            ghost.style.transform = "scale(0.98)";
            document.body.appendChild(ghost);
            return ghost;
        };

        const createDataTransferFallback = () => ({
            dropEffect: "move",
            effectAllowed: "all",
            files: [],
            items: [],
            types: [],
            setData: () => { },
            getData: () => "",
            clearData: () => { }
        });

        const autoScrollAtPoint = (x, y) => {
            const viewportHeight = window.innerHeight || document.documentElement.clientHeight || 0;
            if (viewportHeight > 0) {
                if (y >= viewportHeight - AUTO_SCROLL_EDGE_PX) {
                    window.scrollBy(0, AUTO_SCROLL_STEP_PX);
                } else if (y <= AUTO_SCROLL_EDGE_PX) {
                    window.scrollBy(0, -AUTO_SCROLL_STEP_PX);
                }
            }

            if (root.scrollHeight > root.clientHeight) {
                const rect = root.getBoundingClientRect();
                if (y >= rect.bottom - AUTO_SCROLL_EDGE_PX) {
                    root.scrollTop += AUTO_SCROLL_STEP_PX;
                } else if (y <= rect.top + AUTO_SCROLL_EDGE_PX) {
                    root.scrollTop -= AUTO_SCROLL_STEP_PX;
                }
            }
        };

        const resolveDropCandidate = (x, y) => {
            const rawCandidate = document.elementFromPoint(x, y);
            if (!rawCandidate || !rawCandidate.closest) return rawCandidate;

            return rawCandidate.closest(
                ".team-seed-insert-marker, .team-seed-card > .card, .team-slot-area, .draw-group-dropzone, .ko-draw-slot-card, .schedule-board-dropzone, .schedule-board-header, .schedule-timeline-match"
            ) || rawCandidate;
        };

        const fire = (node, type) => {
            if (!node) return;

            let dataTransfer = null;
            try {
                if (typeof DataTransfer === "function") {
                    dataTransfer = new DataTransfer();
                }
            } catch (e) {
                dataTransfer = null;
            }

            if (!dataTransfer) {
                dataTransfer = createDataTransferFallback();
            }

            try {
                if (typeof DragEvent === "function") {
                    const dragEvent = new DragEvent(type, {
                        bubbles: true,
                        cancelable: true,
                        dataTransfer: dataTransfer
                    });

                    if (!dragEvent.dataTransfer) {
                        Object.defineProperty(dragEvent, "dataTransfer", {
                            value: dataTransfer,
                            configurable: true
                        });
                    }

                    node.dispatchEvent(dragEvent);
                    return;
                }
            } catch (e) {
                // fall through to generic event dispatch
            }

            try {
                const fallbackEvent = new Event(type, { bubbles: true, cancelable: true });
                Object.defineProperty(fallbackEvent, "dataTransfer", {
                    value: dataTransfer,
                    configurable: true
                });
                node.dispatchEvent(fallbackEvent);
            } catch (e) {
                // best effort only
            }
        };

        const onTouchStart = (ev) => {
            if (!ev.touches || ev.touches.length !== 1) return;

            const touch = ev.touches[0];
            const draggable = touch.target && touch.target.closest ? touch.target.closest("[draggable='true']") : null;
            if (!draggable || !root.contains(draggable)) return;

            reset();

            state.touchId = touch.identifier;
            state.sourceEl = draggable;
            state.sourceWasDraggable = !!draggable.draggable;
            // Disable native HTML5 drag on touch devices; we emulate DnD manually.
            draggable.draggable = false;
            state.startX = touch.clientX;
            state.startY = touch.clientY;
            state.startAtMs = Date.now();
            state.suppressContextMenu = true;

            state.timer = setTimeout(() => {
                beginDragIfPossible();
            }, LONG_PRESS_MS);
        };

        const onContextMenu = (ev) => {
            const target = ev && ev.target ? ev.target : null;
            if (!target || !target.closest) return;

            const draggable = target.closest("[draggable='true']");
            if ((state.suppressContextMenu || state.dragging) && draggable && root.contains(draggable)) {
                ev.preventDefault();
                ev.stopPropagation();
            }
        };

        const onTouchMove = (ev) => {
            const touch = findTouch(ev.touches);
            if (!touch || !state.sourceEl) return;

            const dx = touch.clientX - state.startX;
            const dy = touch.clientY - state.startY;
            const distance = Math.max(Math.abs(dx), Math.abs(dy));
            const elapsedMs = Math.max(0, Date.now() - state.startAtMs);

            if (!state.dragging && elapsedMs >= LONG_PRESS_MS - 40 && distance > DRAG_START_CANCEL_PX) {
                beginDragIfPossible();
            }

            if (!state.dragging && elapsedMs < LONG_PRESS_MS && distance > MOVE_CANCEL_PX) {
                clearTimer();
                reset();
                return;
            }

            if (!state.dragging && (Math.abs(dx) > DRAG_START_CANCEL_PX || Math.abs(dy) > DRAG_START_CANCEL_PX)) {
                ev.preventDefault();
            }

            if (!state.dragging) return;

            ev.preventDefault();

            if (state.ghostEl) {
                state.ghostEl.style.left = (touch.clientX + 10) + "px";
                state.ghostEl.style.top = (touch.clientY + 10) + "px";
            }

            autoScrollAtPoint(touch.clientX, touch.clientY);

            const candidate = resolveDropCandidate(touch.clientX, touch.clientY);
            if (!candidate) return;

            state.dropEl = candidate;
            fire(candidate, "dragover");
        };

        const onTouchEnd = (ev) => {
            let touch = findTouch(ev.changedTouches);
            if (!touch && ev.changedTouches && ev.changedTouches.length === 1) {
                touch = ev.changedTouches[0];
            }
            if (!touch || !state.sourceEl) {
                reset();
                return;
            }

            if (state.dragging) {
                const dropCandidate = state.dropEl || resolveDropCandidate(touch.clientX, touch.clientY);
                fire(dropCandidate, "drop");
                fire(state.sourceEl, "dragend");
            }

            reset();
        };

        const onTouchCancel = () => {
            if (state.dragging && state.sourceEl) {
                fire(state.sourceEl, "dragend");
            }
            reset();
        };

        const onPointerDown = (ev) => {
            if (!ev || ev.pointerType !== "touch") return;
            if (state.sourceEl) return;

            onTouchStart({
                touches: [{ identifier: ev.pointerId, clientX: ev.clientX, clientY: ev.clientY }],
                changedTouches: [{ identifier: ev.pointerId, clientX: ev.clientX, clientY: ev.clientY }],
                target: ev.target,
                preventDefault: () => ev.preventDefault()
            });
        };

        const onPointerMove = (ev) => {
            if (!ev || ev.pointerType !== "touch") return;
            if (state.touchId !== null && ev.pointerId !== state.touchId) return;

            onTouchMove({
                touches: [{ identifier: ev.pointerId, clientX: ev.clientX, clientY: ev.clientY }],
                changedTouches: [{ identifier: ev.pointerId, clientX: ev.clientX, clientY: ev.clientY }],
                preventDefault: () => ev.preventDefault()
            });
        };

        const onPointerUp = (ev) => {
            if (!ev || ev.pointerType !== "touch") return;
            if (state.touchId !== null && ev.pointerId !== state.touchId) return;

            onTouchEnd({
                touches: [],
                changedTouches: [{ identifier: ev.pointerId, clientX: ev.clientX, clientY: ev.clientY }],
                preventDefault: () => ev.preventDefault()
            });
        };

        const onPointerCancel = (ev) => {
            if (!ev || ev.pointerType !== "touch") return;
            onTouchCancel();
        };

        const hasNativeTouch = ("ontouchstart" in window) || (navigator && navigator.maxTouchPoints > 0);

        if (hasNativeTouch) {
            // On mobile we use native touch events only to avoid duplicate pointer+touch streams.
            root.addEventListener("touchstart", onTouchStart, { passive: false });
            document.addEventListener("touchmove", onTouchMove, { passive: false, capture: true });
            document.addEventListener("touchend", onTouchEnd, { passive: true, capture: true });
            document.addEventListener("touchcancel", onTouchCancel, { passive: true, capture: true });
        } else {
            // Pointer fallback for environments without touch events.
            root.addEventListener("pointerdown", onPointerDown, { passive: false });
            document.addEventListener("pointermove", onPointerMove, { passive: false, capture: true });
            document.addEventListener("pointerup", onPointerUp, { passive: false, capture: true });
            document.addEventListener("pointercancel", onPointerCancel, { passive: true, capture: true });
        }
        root.addEventListener("contextmenu", onContextMenu, true);

        this._touchDnDHandlers[elementId] = {
            root: root,
            hasNativeTouch: hasNativeTouch,
            onTouchStart: onTouchStart,
            onTouchMove: onTouchMove,
            onTouchEnd: onTouchEnd,
            onTouchCancel: onTouchCancel,
            onPointerDown: onPointerDown,
            onPointerMove: onPointerMove,
            onPointerUp: onPointerUp,
            onPointerCancel: onPointerCancel,
            onContextMenu: onContextMenu
        };
    },
    unregisterTouchDragDrop: function (elementId) {
        const reg = this._touchDnDHandlers[elementId];
        if (!reg) return;

        try {
            if (reg.hasNativeTouch) {
                reg.root.removeEventListener("touchstart", reg.onTouchStart);
                document.removeEventListener("touchmove", reg.onTouchMove, true);
                document.removeEventListener("touchend", reg.onTouchEnd, true);
                document.removeEventListener("touchcancel", reg.onTouchCancel, true);
            } else {
                reg.root.removeEventListener("pointerdown", reg.onPointerDown);
                document.removeEventListener("pointermove", reg.onPointerMove, true);
                document.removeEventListener("pointerup", reg.onPointerUp, true);
                document.removeEventListener("pointercancel", reg.onPointerCancel, true);
            }
            reg.root.removeEventListener("contextmenu", reg.onContextMenu, true);
        } catch (e) {
            // best effort only
        }

        delete this._touchDnDHandlers[elementId];
    },
    registerOutsideClick: function (key, dotNetRef, callbackMethodName, ignoreSelector) {
        if (!key || !dotNetRef || !callbackMethodName) return;

        this.unregisterOutsideClick(key);

        const onPointerDown = (ev) => {
            const target = ev && ev.target ? ev.target : null;
            if (!target) return;

            if (ignoreSelector && typeof ignoreSelector === "string") {
                try {
                    if (target.closest(ignoreSelector)) {
                        return;
                    }
                } catch (e) {
                    // ignore invalid selector
                }
            }

            try {
                const invokeResult = dotNetRef.invokeMethodAsync(callbackMethodName);
                if (invokeResult && typeof invokeResult.catch === "function") {
                    invokeResult.catch(() => { });
                }
            } catch (e) {
                // Connection may already be closed; ignore best-effort callback.
            }
        };

        document.addEventListener("pointerdown", onPointerDown, true);
        this._outsideClickHandlers[key] = { onPointerDown: onPointerDown };
    },
    unregisterOutsideClick: function (key) {
        const reg = this._outsideClickHandlers[key];
        if (!reg) return;

        try {
            document.removeEventListener("pointerdown", reg.onPointerDown, true);
        } catch (e) {
            // best effort only
        }

        delete this._outsideClickHandlers[key];
    },
    scrollActiveTabIntoView: function (containerSelector) {
        const container = document.querySelector(containerSelector || ".tournament-tabs-nowrap");
        if (!container) return;
        const active = container.querySelector(".nav-link.active");
        if (!active) return;
        active.scrollIntoView({ block: "nearest", inline: "nearest", behavior: "smooth" });
    },
    focusAndSelect: function (selector) {
        if (!selector || typeof selector !== "string") return;

        const input = document.querySelector(selector);
        if (!input || typeof input.focus !== "function") return;

        try {
            input.focus({ preventScroll: true });
        } catch (e) {
            input.focus();
        }

        if (typeof input.select === "function") {
            try {
                input.select();
            } catch (e) {
                // ignore select errors for non-text inputs
            }
        }
    }
};
