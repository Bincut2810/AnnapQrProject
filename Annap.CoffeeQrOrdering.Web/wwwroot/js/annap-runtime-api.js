/**
 * Depends on window.__annapRuntime from _AnnapRuntimeHead (server).
 */
(function (w) {
    "use strict";

    function apiBase() {
        var r = w.__annapRuntime || {};
        var s = r.apiBase || r.baseUrl || "";
        return String(s).replace(/\/$/, "");
    }

    w.__annapApiUrl = function (path) {
        var p = path == null ? "/" : String(path);
        if (p.charAt(0) !== "/") p = "/" + p;
        var b = apiBase();
        return b ? b + p : p;
    };

    w.__annapHubUrl = function () {
        var r = w.__annapRuntime || {};
        if (r.signalRBase) return String(r.signalRBase).replace(/\/$/, "");
        return "/hubs/orders";
    };
})(window);
