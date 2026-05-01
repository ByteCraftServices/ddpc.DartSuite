// Infowall Interop — WebSocket event listener for live camera auto-selection
// Issue #77: Live Preview Infowall
//
// Connects to ws://<board-ip>:3180/api/events and filters for throw events.
// When a throw event arrives, calls OnThrowEvent on the Blazor component
// with a camera index hint derived from the throw zone.

window.InfowallInterop = (() => {
    let ws = null;
    let dotNetRef = null;
    let reconnectTimer = null;
    let currentWsUrl = null;
    let stopped = false;

    function parseCameraHintFromThrow(event) {
        // Autodarts throw events contain zone/sector info.
        // Simple heuristic: use the event type or coordinate to pick a camera.
        // zone 0–9 → cam 0 (front), zone 10–15 → cam 1 (left), zone 16–20 → cam 2 (right)
        const sector = event?.data?.throw?.sector ?? event?.data?.sector ?? -1;
        if (sector <= 0) return 0;
        if (sector <= 9) return 0;
        if (sector <= 15) return 1;
        return 2;
    }

    function connect(wsUrl) {
        if (ws && (ws.readyState === WebSocket.OPEN || ws.readyState === WebSocket.CONNECTING)) {
            ws.close();
        }

        try {
            ws = new WebSocket(wsUrl);
        } catch (err) {
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('OnWsError', `WS connect failed: ${err.message}`);
            }
            scheduleReconnect(wsUrl);
            return;
        }

        ws.onopen = () => {
            console.info(`[InfowallInterop] WS connected: ${wsUrl}`);
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('OnWsError', '');  // clear error
            }
        };

        ws.onmessage = (evt) => {
            try {
                const msg = JSON.parse(evt.data);
                const eventType = (msg?.event || msg?.type || '').toLowerCase();

                // Filter for throw events only (e.g. "throw", "dart-thrown", "round-throw")
                if (eventType.includes('throw') || eventType.includes('dart')) {
                    const camHint = parseCameraHintFromThrow(msg);
                    if (dotNetRef) {
                        dotNetRef.invokeMethodAsync('OnThrowEvent', camHint);
                    }
                }
            } catch { /* ignore non-JSON messages */ }
        };

        ws.onerror = (err) => {
            console.warn('[InfowallInterop] WS error', err);
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('OnWsError', 'Verbindungsfehler zur Board-API');
            }
        };

        ws.onclose = () => {
            if (!stopped) {
                scheduleReconnect(wsUrl);
            }
        };
    }

    function scheduleReconnect(wsUrl) {
        if (stopped) return;
        clearTimeout(reconnectTimer);
        reconnectTimer = setTimeout(() => {
            if (!stopped) connect(wsUrl);
        }, 5000);
    }

    return {
        startEventListener: function (wsUrl, blazorRef) {
            stopped = false;
            dotNetRef = blazorRef;
            currentWsUrl = wsUrl;
            clearTimeout(reconnectTimer);
            connect(wsUrl);
        },

        stopEventListener: function () {
            stopped = true;
            clearTimeout(reconnectTimer);
            if (ws) {
                ws.close();
                ws = null;
            }
            dotNetRef = null;
            currentWsUrl = null;
        }
    };
})();
