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
    _swipeHandlers: {},
    _touchDnDHandlers: {},
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
            dotNetRef.invokeMethodAsync(callbackMethodName, direction).catch(() => { });
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

        const LONG_PRESS_MS = 280;
        const MOVE_CANCEL_PX = 10;

        const state = {
            timer: null,
            sourceEl: null,
            dropEl: null,
            ghostEl: null,
            touchId: null,
            startX: 0,
            startY: 0,
            dragging: false
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
            state.sourceEl = null;
            state.dropEl = null;
            state.ghostEl = null;
            state.touchId = null;
            state.dragging = false;
        };

        const findTouch = (touchList) => {
            if (!touchList || state.touchId === null) return null;
            for (let i = 0; i < touchList.length; i++) {
                if (touchList[i].identifier === state.touchId) {
                    return touchList[i];
                }
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

        const fire = (node, type) => {
            if (!node) return;
            try {
                node.dispatchEvent(new Event(type, { bubbles: true, cancelable: true }));
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
            state.startX = touch.clientX;
            state.startY = touch.clientY;

            state.timer = setTimeout(() => {
                if (!state.sourceEl) return;
                state.dragging = true;
                state.ghostEl = createGhost(state.sourceEl, state.startX, state.startY);
                fire(state.sourceEl, "dragstart");
            }, LONG_PRESS_MS);
        };

        const onTouchMove = (ev) => {
            const touch = findTouch(ev.touches);
            if (!touch || !state.sourceEl) return;

            const dx = touch.clientX - state.startX;
            const dy = touch.clientY - state.startY;

            if (!state.dragging && (Math.abs(dx) > MOVE_CANCEL_PX || Math.abs(dy) > MOVE_CANCEL_PX)) {
                clearTimer();
                return;
            }

            if (!state.dragging) return;

            ev.preventDefault();

            if (state.ghostEl) {
                state.ghostEl.style.left = (touch.clientX + 10) + "px";
                state.ghostEl.style.top = (touch.clientY + 10) + "px";
            }

            const candidate = document.elementFromPoint(touch.clientX, touch.clientY);
            if (!candidate) return;

            state.dropEl = candidate;
            fire(candidate, "dragover");
        };

        const onTouchEnd = (ev) => {
            const touch = findTouch(ev.changedTouches);
            if (!touch || !state.sourceEl) {
                reset();
                return;
            }

            if (state.dragging) {
                const dropCandidate = state.dropEl || document.elementFromPoint(touch.clientX, touch.clientY);
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

        root.addEventListener("touchstart", onTouchStart, { passive: true });
        root.addEventListener("touchmove", onTouchMove, { passive: false });
        root.addEventListener("touchend", onTouchEnd, { passive: true });
        root.addEventListener("touchcancel", onTouchCancel, { passive: true });

        this._touchDnDHandlers[elementId] = {
            root: root,
            onTouchStart: onTouchStart,
            onTouchMove: onTouchMove,
            onTouchEnd: onTouchEnd,
            onTouchCancel: onTouchCancel
        };
    },
    unregisterTouchDragDrop: function (elementId) {
        const reg = this._touchDnDHandlers[elementId];
        if (!reg) return;

        try {
            reg.root.removeEventListener("touchstart", reg.onTouchStart);
            reg.root.removeEventListener("touchmove", reg.onTouchMove);
            reg.root.removeEventListener("touchend", reg.onTouchEnd);
            reg.root.removeEventListener("touchcancel", reg.onTouchCancel);
        } catch (e) {
            // best effort only
        }

        delete this._touchDnDHandlers[elementId];
    }
};
