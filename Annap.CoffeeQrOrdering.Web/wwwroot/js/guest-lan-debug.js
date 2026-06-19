/**
 * Development-only LAN connectivity overlay (guest layout).
 * Pages update via window.AnnapGuestLanDebug.setMenu / setHub / log / mark.
 */
(function () {
    "use strict";

    if (window.__ANNAP_DEV_MODE !== true) {
        return;
    }

    var state = {
        menu: "—",
        hub: "—",
        log: []
    };

    var bootMarks = [];

    function el() {
        var d = document.getElementById("annap-lan-debug");
        if (!d) return null;
        return d;
    }

    function mark(name) {
        var t = typeof performance !== "undefined" && performance.now ? performance.now() : Date.now();
        bootMarks.push({ n: String(name || ""), t: t });
        if (bootMarks.length > 28) bootMarks.shift();
        render();
    }

    function bootSummary() {
        if (bootMarks.length === 0) return "—";
        var t0 = bootMarks[0].t;
        return bootMarks
            .map(function (b) {
                return b.n + "+" + Math.round(b.t - t0) + "ms";
            })
            .join(" · ");
    }

    function render() {
        var root = el();
        if (!root) return;
        var o = "";
        try {
            o = location.origin;
            var r = window.__annapRuntime || {};
            if (r.apiBase || r.baseUrl) o = String(r.apiBase || r.baseUrl);
        } catch (e) {
            o = "—";
        }
        root.innerHTML =
            "<strong>LAN debug</strong><br>" +
            "Origin: <code>" +
            escapeHtml(o) +
            "</code><br>" +
            "API base: <code>" +
            escapeHtml(o) +
            "</code> <small>(relative <code>/api/…</code>)</small><br>" +
            "Menu: <span data-dm>" +
            escapeHtml(state.menu) +
            "</span><br>" +
            "SignalR: <span data-dh>" +
            escapeHtml(state.hub) +
            "</span><br>" +
            "<small>Boot marks: " +
            escapeHtml(bootSummary()) +
            "</small><br>" +
            "<small style=opacity:.75>" +
            escapeHtml(state.log.slice(-4).join(" · ") || "—") +
            "</small>";
    }

    function escapeHtml(s) {
        return String(s)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function setMenu(label) {
        state.menu = label;
        render();
    }

    function setHub(label) {
        state.hub = label;
        render();
    }

    function log(line) {
        try {
            console.info("[annap-lan]", line);
        } catch (e) {
            /* ignore */
        }
        state.log.push(("0" + new Date().getSeconds()).slice(-2) + " " + line);
        if (state.log.length > 14) state.log.shift();
        render();
    }

    window.AnnapGuestLanDebug = {
        setMenu: setMenu,
        setHub: setHub,
        log: log,
        mark: mark,
        render: render
    };

    mark("guest-lan-debug-js");
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", function () {
            mark("DOMContentLoaded");
        });
    } else {
        mark("DOMContentLoaded-early");
    }
    window.addEventListener("load", function () {
        mark("window-load");
    });

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", render);
    else render();
    setInterval(render, 1200);
})();
