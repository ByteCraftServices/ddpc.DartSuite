(function () {
    "use strict";

    function isSupportedInputType(input) {
        var type = (input.getAttribute("type") || "text").toLowerCase();
        return type !== "checkbox"
            && type !== "radio"
            && type !== "hidden"
            && type !== "file"
            && type !== "range"
            && type !== "color"
            && type !== "button"
            && type !== "submit"
            && type !== "reset";
    }

    function isFloatingControl(control) {
        if (!control) {
            return false;
        }

        var tag = control.tagName;
        if (tag === "INPUT") {
            return control.classList.contains("form-control") && isSupportedInputType(control);
        }

        if (tag === "SELECT") {
            return control.classList.contains("form-select");
        }

        if (tag === "TEXTAREA") {
            return control.classList.contains("form-control");
        }

        return false;
    }

    function ensureForBinding(label, control, uid) {
        var id = control.getAttribute("id");
        if (!id) {
            id = "auto-float-" + uid;
            control.setAttribute("id", id);
        }

        if (label.getAttribute("for") !== id) {
            label.setAttribute("for", id);
        }
    }

    function normalizeLabelClasses(label) {
        label.classList.remove("form-label", "mb-0", "mb-1");
    }

    function needsSmallVariant(label, control) {
        return label.classList.contains("small")
            || control.classList.contains("form-control-sm")
            || control.classList.contains("form-select-sm");
    }

    function addPlaceholderIfNeeded(control) {
        var tag = control.tagName;
        if ((tag === "INPUT" || tag === "TEXTAREA") && !control.hasAttribute("placeholder")) {
            control.setAttribute("placeholder", " ");
        }
    }

    function upgradeLabelPair(label, uid) {
        if (!label || label.dataset.floatingUpgraded === "true") {
            return false;
        }

        if (label.classList.contains("form-check-label") || label.closest(".form-check")) {
            return false;
        }

        if (label.closest(".form-floating")) {
            return false;
        }

        var control = label.nextElementSibling;
        var inputGroup = null;
        if (!isFloatingControl(control)) {
            if (!control || !control.classList || !control.classList.contains("input-group")) {
                return false;
            }

            inputGroup = control;
            control = inputGroup.querySelector("input.form-control, select.form-select, textarea.form-control");
            if (!isFloatingControl(control) || control.closest(".form-floating")) {
                return false;
            }
        }

        var parent = label.parentElement;
        if (!parent || parent.classList.contains("input-group")) {
            return false;
        }

        var wrapper = document.createElement("div");
        wrapper.className = "form-floating";
        if (needsSmallVariant(label, control)) {
            wrapper.classList.add("form-floating-sm");
        }
        if (inputGroup) {
            wrapper.classList.add("flex-grow-1");
        }

        if (inputGroup) {
            inputGroup.insertBefore(wrapper, control);
            wrapper.appendChild(control);
            wrapper.appendChild(label);
        } else {
            parent.insertBefore(wrapper, label);
            wrapper.appendChild(control);
            wrapper.appendChild(label);
        }

        addPlaceholderIfNeeded(control);
        ensureForBinding(label, control, uid);
        normalizeLabelClasses(label);

        label.dataset.floatingUpgraded = "true";
        control.dataset.floatingUpgraded = "true";

        return true;
    }

    function deriveLabelText(control) {
        var ariaLabel = (control.getAttribute("aria-label") || "").trim();
        if (ariaLabel) {
            return ariaLabel;
        }

        var title = (control.getAttribute("title") || "").trim();
        if (title && title.length <= 40) {
            return title;
        }

        var placeholder = (control.getAttribute("placeholder") || "").trim();
        if (placeholder) {
            return placeholder;
        }

        return "";
    }

    function upgradeUnlabeledControls(root) {
        var scope = root || document;
        var controls = scope.querySelectorAll("input.form-control, select.form-select, textarea.form-control");
        var upgrades = 0;

        for (var i = 0; i < controls.length; i++) {
            var control = controls[i];

            if (!isFloatingControl(control)) {
                continue;
            }

            if (control.closest(".form-floating") || control.closest(".form-check") || control.closest(".input-group")) {
                continue;
            }

            var parent = control.parentElement;
            if (!parent) {
                continue;
            }

            var text = deriveLabelText(control);
            if (!text) {
                continue;
            }

            var wrapper = document.createElement("div");
            wrapper.className = "form-floating";
            if (control.classList.contains("form-control-sm") || control.classList.contains("form-select-sm")) {
                wrapper.classList.add("form-floating-sm");
            }

            var label = document.createElement("label");
            label.textContent = text;

            parent.insertBefore(wrapper, control);
            wrapper.appendChild(control);
            wrapper.appendChild(label);

            addPlaceholderIfNeeded(control);
            ensureForBinding(label, control, "auto" + i);
            upgrades++;
        }

        return upgrades;
    }

    function applyFloatingLabels(root) {
        var scope = root || document;
        var labels = scope.querySelectorAll("label.form-label");
        var changes = 0;

        for (var i = 0; i < labels.length; i++) {
            if (upgradeLabelPair(labels[i], i + 1)) {
                changes++;
            }
        }

        changes += upgradeUnlabeledControls(scope);

        return changes;
    }

    function startObserver() {
        if (!window.MutationObserver) {
            return;
        }

        var pending = false;
        var observer = new MutationObserver(function () {
            if (pending) {
                return;
            }

            pending = true;
            window.requestAnimationFrame(function () {
                pending = false;
                applyFloatingLabels(document);
            });
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    function init() {
        applyFloatingLabels(document);
        startObserver();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
