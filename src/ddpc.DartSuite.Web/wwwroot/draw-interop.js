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

        const LONG_PRESS_MS = 220;
        const MOVE_CANCEL_PX = 18;
        const DRAG_START_CANCEL_PX = 6;

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
            try {
                document.body.style.touchAction = "";
                document.body.style.overscrollBehavior = "";
            } catch (e) {
                // best effort only
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

            // Prevent native touch scrolling/selection on potential drag sources.
            ev.preventDefault();
            try {
                document.body.style.touchAction = "none";
                document.body.style.overscrollBehavior = "none";
            } catch (e) {
                // best effort only
            }

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

        root.addEventListener("touchstart", onTouchStart, { passive: false });
        root.addEventListener("touchmove", onTouchMove, { passive: false });
        root.addEventListener("touchend", onTouchEnd, { passive: true });
        root.addEventListener("touchcancel", onTouchCancel, { passive: true });
        root.addEventListener("pointerdown", onPointerDown, { passive: false });
        root.addEventListener("pointermove", onPointerMove, { passive: false });
        root.addEventListener("pointerup", onPointerUp, { passive: false });
        root.addEventListener("pointercancel", onPointerCancel, { passive: true });

        this._touchDnDHandlers[elementId] = {
            root: root,
            onTouchStart: onTouchStart,
            onTouchMove: onTouchMove,
            onTouchEnd: onTouchEnd,
            onTouchCancel: onTouchCancel,
            onPointerDown: onPointerDown,
            onPointerMove: onPointerMove,
            onPointerUp: onPointerUp,
            onPointerCancel: onPointerCancel
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
            reg.root.removeEventListener("pointerdown", reg.onPointerDown);
            reg.root.removeEventListener("pointermove", reg.onPointerMove);
            reg.root.removeEventListener("pointerup", reg.onPointerUp);
            reg.root.removeEventListener("pointercancel", reg.onPointerCancel);
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

            dotNetRef.invokeMethodAsync(callbackMethodName).catch(() => { });
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
