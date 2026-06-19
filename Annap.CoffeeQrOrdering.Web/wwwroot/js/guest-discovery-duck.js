/**
 * Discovery ritual — "Mail duck" motif (replaces sealed-envelope desk UX).
 * Same contract as guest-discovery-courier: GuestDiscoveryCourierRitual.play(cfg).
 * Loaded after courier.js to override window.GuestDiscoveryCourierRitual.
 */
(function (window) {
    "use strict";

    var SCENE = {
        dim: "dim",
        letter: "letter",
        open: "open",
        reveal: "reveal"
    };

    function geDuckT(path, fallback) {
        try {
            if (window.LuxuryI18n && typeof window.LuxuryI18n.t === "function") {
                var s = window.LuxuryI18n.t(path);
                if (s && String(s).trim()) return String(s).trim();
            }
        } catch (_e) {}
        return fallback || "";
    }

    /** Match pacing in guest-discovery-courier timingLetterRoomFast (fetch minAll). */
    function timingDuck(cap) {
        var L = cap >= 3 ? 3 : cap < 1 ? 1 : cap;
        if (L === 1) {
            return {
                dimAt: 80,
                dimLine: geDuckT("ge.host.wait1", "The bar goes quiet for a moment…"),
                letterAt: 400,
                letterLine: geDuckT("ge.host.wait2", "Steam rises from the pass."),
                openAt: 1900,
                openLine: geDuckT("ge.host.wait3", "I'm choosing one cup for your table."),
                revealAt: 3400,
                minAll: 4700
            };
        }
        if (L === 2) {
            return {
                dimAt: 60,
                dimLine: geDuckT("ge.host.wait4", "Let me look once more…"),
                letterAt: 320,
                letterLine: geDuckT("ge.host.wait5", "The room is still — no rush."),
                openAt: 1780,
                openLine: geDuckT("ge.host.wait6", "I want the quieter truth tonight."),
                revealAt: 3200,
                minAll: 4500
            };
        }
        return {
            dimAt: 50,
            dimLine: geDuckT("ge.host.wait7", "One last read of the evening…"),
            letterAt: 280,
            letterLine: geDuckT("ge.host.wait8", "Nguyễn Thị Minh Khai is hushed tonight."),
            openAt: 1700,
            openLine: geDuckT("ge.host.wait9", "This is the cup I'd stand behind."),
            revealAt: 3100,
            minAll: 4300
        };
    }

    function setLine(lineEl, text, soft) {
        if (!lineEl) return;
        lineEl.textContent = text || "";
        lineEl.className = soft ? "ge-discovery-line ge-discovery-line--soft" : "ge-discovery-line ge-discovery-line--enter";
    }

    function playDuck(cfg) {
        var stage = cfg.stage;
        var line = cfg.line;
        if (!stage || !cfg.fetchPromise || typeof cfg.onPack !== "function" || typeof cfg.onFail !== "function") {
            try {
                cfg.onFail && cfg.onFail("");
            } catch (_e) {}
            return;
        }
        var leg = typeof cfg.leg === "number" && cfg.leg > 0 ? cfg.leg : 1;
        var cap = leg >= 3 ? 3 : leg;
        var T = timingDuck(cap);
        var fetchPromise = cfg.fetchPromise;
        var push = cfg.pushTimer;
        var reduced = !!cfg.reducedMotion;

        if (stage) {
            stage.classList.add("ge-discovery-stage--duck");
        }
        stage.setAttribute("data-gdr-leg", String(cap));
        stage.removeAttribute("data-gdr-scene");

        function finishDuckVisual() {
            if (stage) stage.classList.remove("ge-discovery-stage--duck");
        }

        if (reduced) {
            stage.setAttribute("data-gdr-scene", SCENE.dim);
            setLine(line, geDuckT("ge.host.loading", "One moment — I'm at the bar."), false);
            Promise.all([
                fetchPromise,
                new Promise(function (resolve) {
                    var tw = window.setTimeout(resolve, 260);
                    push(tw);
                })
            ])
                .then(function (arr) {
                    finishDuckVisual();
                    stage.setAttribute("data-gdr-scene", SCENE.reveal);
                    cfg.onPack(arr[0]);
                })
                .catch(function () {
                    finishDuckVisual();
                    cfg.onFail("");
                });
            return;
        }

        function at(ms, fn) {
            var id = window.setTimeout(fn, ms);
            push(id);
        }

        at(T.dimAt, function () {
            stage.setAttribute("data-gdr-scene", SCENE.dim);
            setLine(line, T.dimLine, false);
        });

        at(T.letterAt, function () {
            stage.setAttribute("data-gdr-scene", SCENE.letter);
            setLine(line, T.letterLine, false);
        });

        at(T.openAt, function () {
            stage.setAttribute("data-gdr-scene", SCENE.open);
            setLine(line, T.openLine, false);
        });

        at(T.revealAt, function () {
            stage.setAttribute("data-gdr-scene", SCENE.reveal);
            if (line) {
                line.textContent = "";
                line.className = "ge-discovery-line";
            }
        });

        Promise.all([
            fetchPromise,
            new Promise(function (resolve) {
                var tw = window.setTimeout(resolve, T.minAll);
                push(tw);
            })
        ])
            .then(function (arr) {
                var pack = arr[0];
                if (!pack || pack.ok === false) {
                    finishDuckVisual();
                    cfg.onFail("");
                    return;
                }
                stage.setAttribute("data-gdr-scene", SCENE.reveal);
                finishDuckVisual();
                cfg.onPack(pack);
            })
            .catch(function () {
                finishDuckVisual();
                cfg.onFail("");
            });
    }

    window.GuestDiscoveryCourierRitual = {
        play: playDuck
    };
})(window);
