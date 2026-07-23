(function (window, document) {
    "use strict";

    var EXIT_MS = 480;
    var READY_MS = 1100;
    var SETTLE_MS = 1000;
    var STORAGE_KEY = "annap_arrival_done";
    var focusTimer = null;
    var settleTimer = null;

    function clearFocusTimer() {
        if (focusTimer == null) return;
        window.clearTimeout(focusTimer);
        focusTimer = null;
    }

    function clearSettleTimer() {
        if (settleTimer == null) return;
        window.clearTimeout(settleTimer);
        settleTimer = null;
    }

    function clearArrivalTimers() {
        clearFocusTimer();
        clearSettleTimer();
    }

    function reducedMotion() {
        try {
            if (document.documentElement.classList.contains("annap-guest-no-motion")) return true;
            if (window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches) return true;
        } catch (_) {}
        return false;
    }

    function skipByQuery() {
        try {
            return /[?&]arrival=0(?:&|$)/.test(String(window.location.search || ""));
        } catch (_) {
            return false;
        }
    }

    function setActive(on) {
        document.documentElement.classList.toggle("annap-arrival-active", !!on);
    }

    function langIsEn() {
        try {
            if (window.LuxuryI18n && typeof window.LuxuryI18n.getLang === "function") {
                return String(window.LuxuryI18n.getLang() || "vi").toLowerCase() === "en";
            }
        } catch (_) {}
        try {
            return String(document.documentElement.lang || "").toLowerCase().indexOf("en") === 0;
        } catch (_) {
            return false;
        }
    }

    function paintCopy(root) {
        var en = langIsEn();
        var table = root.querySelector("#annap-arrival-table");
        var welcome = root.querySelector("#annap-arrival-welcome");
        var tableLine = root.getAttribute(en ? "data-table-en" : "data-table-vi");
        var sentence = root.getAttribute(en ? "data-welcome-en" : "data-welcome-vi");
        if (table && tableLine) table.textContent = tableLine;
        if (welcome && sentence) welcome.textContent = sentence;
        try {
            if (window.LuxuryI18n && typeof window.LuxuryI18n.applyDom === "function") {
                window.LuxuryI18n.applyDom(root);
            }
        } catch (_) {}
    }

    function markDone() {
        try { sessionStorage.setItem(STORAGE_KEY, "1"); } catch (_) {}
    }

    function finish(root, enter) {
        clearArrivalTimers();
        root.classList.add("is-done");
        root.hidden = true;
        root.setAttribute("aria-hidden", "true");
        setActive(false);
        markDone();
        try {
            window.dispatchEvent(new CustomEvent("annap-arrival-complete", {
                detail: { enter: !!enter }
            }));
        } catch (_) {}
    }

    function leave(root, enter) {
        if (root.getAttribute("data-leaving") === "1") return;
        root.setAttribute("data-leaving", "1");
        clearArrivalTimers();
        var invite = root.querySelector("#annap-arrival-invite");
        if (invite) invite.disabled = true;
        if (reducedMotion()) {
            finish(root, enter);
            return;
        }
        root.classList.add("is-leaving");
        window.setTimeout(function () { finish(root, enter); }, EXIT_MS);
    }

    function show(root) {
        setActive(true);
        paintCopy(root);
        root.hidden = false;
        root.removeAttribute("aria-hidden");
        root.classList.remove("is-settled");

        var invite = root.querySelector("#annap-arrival-invite");
        if (invite) {
            invite.addEventListener("click", function (ev) {
                ev.preventDefault();
                leave(root, true);
            });
        }

        var menu = root.querySelector("#annap-arrival-menu");
        if (menu) {
            menu.addEventListener("click", function () {
                clearArrivalTimers();
                markDone();
                setActive(false);
            });
        }

        void root.offsetWidth;
        window.requestAnimationFrame(function () {
            root.classList.add("is-live");
            var quiet = reducedMotion();
            clearArrivalTimers();
            focusTimer = window.setTimeout(function () {
                focusTimer = null;
                try { if (invite) invite.focus(); } catch (_) {}
            }, quiet ? 0 : READY_MS);
            settleTimer = window.setTimeout(function () {
                settleTimer = null;
                try { root.classList.add("is-settled"); } catch (_) {}
            }, quiet ? 0 : SETTLE_MS);
        });
    }

    function boot() {
        var root = document.getElementById("annap-arrival");
        if (!root) return;

        try {
            if (sessionStorage.getItem(STORAGE_KEY) === "1") {
                finish(root, false);
                return;
            }
        } catch (_) {}

        if (skipByQuery()) {
            finish(root, false);
            return;
        }

        function start() { show(root); }

        if (window.LuxuryI18n && window.LuxuryI18n.ready && typeof window.LuxuryI18n.ready.then === "function") {
            window.LuxuryI18n.ready.then(start).catch(start);
        } else {
            start();
        }
    }

    window.addEventListener("load", boot, { once: true });

    window.addEventListener("pageshow", function (ev) {
        if (!ev.persisted) return;
        clearArrivalTimers();
        var root = document.getElementById("annap-arrival");
        var done = false;
        try { done = sessionStorage.getItem(STORAGE_KEY) === "1"; } catch (_) {}

        if (!done) {
            // Incomplete Arrival restored from bfcache — keep the scene interactive.
            if (root && !root.classList.contains("is-done")) setActive(true);
            return;
        }

        setActive(false);
        if (root) {
            root.classList.add("is-done");
            root.hidden = true;
            root.setAttribute("aria-hidden", "true");
        }
        // Menu / prior dismiss: re-signal so Sommelier can open after Back restore.
        try {
            window.dispatchEvent(new CustomEvent("annap-arrival-complete", {
                detail: { enter: false, bfcache: true }
            }));
        } catch (_) {}
    });

    document.addEventListener("luxury:i18n-changed", function () {
        var root = document.getElementById("annap-arrival");
        if (!root || root.classList.contains("is-done") || root.hidden) return;
        paintCopy(root);
    });
})(window, document);
