// DS: Draggable splitter interop for MatchCard Settings Modal split layout.

(function () {
    let _state = null;

    window.dartSuiteSplitter = {
        init: function (splitterEl, settingsPanelEl, dotNetRef) {
            if (!splitterEl || !settingsPanelEl) return;

            // Clean up any previous instance first.
            window.dartSuiteSplitter.dispose();

            let isDragging = false;
            let startX = 0;
            let startWidthPx = 0;
            let containerWidthPx = 0;

            function onMouseDown(e) {
                if (e.button !== 0) return;
                e.preventDefault();
                isDragging = true;
                startX = e.clientX;
                startWidthPx = settingsPanelEl.getBoundingClientRect().width;
                const parent = settingsPanelEl.parentElement;
                containerWidthPx = parent ? parent.getBoundingClientRect().width : 0;
                splitterEl.classList.add('mcsm-splitter--dragging');
                document.body.style.cursor = 'col-resize';
                document.body.style.userSelect = 'none';
            }

            function onMouseMove(e) {
                if (!isDragging || containerWidthPx <= 0) return;
                const delta = e.clientX - startX;
                const newWidthPx = startWidthPx + delta;
                const pct = Math.min(Math.max((newWidthPx / containerWidthPx) * 100, 15), 75);
                dotNetRef.invokeMethodAsync('SetSettingsPanelWidth', pct);
            }

            function onMouseUp() {
                if (!isDragging) return;
                isDragging = false;
                splitterEl.classList.remove('mcsm-splitter--dragging');
                document.body.style.cursor = '';
                document.body.style.userSelect = '';
            }

            splitterEl.addEventListener('mousedown', onMouseDown);
            document.addEventListener('mousemove', onMouseMove);
            document.addEventListener('mouseup', onMouseUp);

            _state = { splitterEl, onMouseDown, onMouseMove, onMouseUp };
        },

        dispose: function () {
            if (!_state) return;
            _state.splitterEl.removeEventListener('mousedown', _state.onMouseDown);
            document.removeEventListener('mousemove', _state.onMouseMove);
            document.removeEventListener('mouseup', _state.onMouseUp);
            _state = null;
        }
    };
})();
