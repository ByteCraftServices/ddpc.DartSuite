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
