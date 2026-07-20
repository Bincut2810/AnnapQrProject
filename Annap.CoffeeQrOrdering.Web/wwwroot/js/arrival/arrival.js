(function (window, document) {
    "use strict";

    var EXIT_MS = 480;
    var READY_MS = 3000;
    var STORAGE_KEY = "annap_arrival_done";

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
        var name = root.querySelector("#annap-arrival-name");
        var sub = root.querySelector("#annap-arrival-sub");
        var title = root.getAttribute(en ? "data-title-en" : "data-title-vi");
        var sentence = root.getAttribute(en ? "data-sub-en" : "data-sub-vi");
        if (name && title) name.textContent = title;
        if (sub && sentence) sub.textContent = sentence;
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
                markDone();
                setActive(false);
            });
        }

        void root.offsetWidth;
        window.requestAnimationFrame(function () {
            root.classList.add("is-live");
            window.setTimeout(function () {
                try { if (invite) invite.focus(); } catch (_) {}
            }, reducedMotion() ? 0 : READY_MS);
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
        setActive(false);
        var root = document.getElementById("annap-arrival");
        if (root) {
            root.classList.add("is-done");
            root.hidden = true;
            root.setAttribute("aria-hidden", "true");
        }
    });

    document.addEventListener("luxury:i18n-changed", function () {
        var root = document.getElementById("annap-arrival");
        if (!root || root.classList.contains("is-done") || root.hidden) return;
        paintCopy(root);
    });
})(window, document);
