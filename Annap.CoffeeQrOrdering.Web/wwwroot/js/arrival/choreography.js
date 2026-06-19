/**
 * Scene 01 — QR Arrival conductor. Ceremonial sealed letter pacing.
 * Transform/opacity/clip-path choreography only.
 * Preserves: finish(), setHtmlChrome(), boot(), pageshow bfcache handler.
 */
(function (window, document) {
    "use strict";

    var CLS = "annap-arrival-scene--";

    var T = {
        deskSettle:      350,
        envelopeArrive:  980,
        pauseSealed:     680,
        flapOpen:       1080,
        lightBloom:      460,
        letterRise:      780,
        textReveal:      680,
        textPause:      1350,
        markAppear:      560,
        markPause:       680,
        dismissDuration: 900
    };

    function wait(ms) {
        return new Promise(function (resolve) { setTimeout(resolve, ms); });
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

    function applyI18n(root) {
        try {
            if (window.LuxuryI18n && typeof window.LuxuryI18n.applyDom === "function") {
                window.LuxuryI18n.applyDom(root);
            }
        } catch (_) {}
    }

    function setHtmlChrome(on) {
        var html = document.documentElement;
        if (on) {
            html.classList.add("annap-arrival-scene-active");
            html.classList.remove("annap-arrival-scene-unlock");
            html.classList.add("annap-arrival-scene--arrival-overflow");
        } else {
            html.classList.remove("annap-arrival-scene-active");
            html.classList.remove("annap-arrival-scene-unlock");
            html.classList.remove("annap-arrival-scene--arrival-overflow");
        }
    }

    function unlockMainInteractivity() {
        document.documentElement.classList.add("annap-arrival-scene-unlock");
    }

    function finish(scene, opts) {
        scene.classList.add(CLS + "done");
        scene.setAttribute("aria-hidden", "true");
        setHtmlChrome(false);
        try { sessionStorage.setItem("annap_arrival_done", "1"); } catch (_) {}
        try { window.dispatchEvent(new CustomEvent("annap-arrival-complete", { detail: opts || { full: false } })); } catch (_) {}
    }

    function phase(scene, name) {
        scene.classList.add(CLS + "choreo-" + name);
    }

    function seedTableLabel(scene) {
        try {
            var lbl = scene.querySelector(".aas-table-label");
            if (!lbl) return;
            var root = document.getElementById("guest-experience-root");
            var label = root ? root.getAttribute("data-table-label") : null;
            if (label) {
                lbl.textContent = label;
            } else {
                lbl.style.display = "none";
            }
        } catch (_) {}
    }

    function skipImmediate(scene) {
        scene.classList.add(CLS + "reduced");
        ["desk","arrive","open","light","letter","text","mark","dismiss"].forEach(function (p) {
            scene.classList.add(CLS + "choreo-" + p);
        });
        setHtmlChrome(false);
        scene.classList.add(CLS + "done");
        scene.setAttribute("aria-hidden", "true");
        try { sessionStorage.setItem("annap_arrival_done", "1"); } catch (_) {}
        try { window.dispatchEvent(new CustomEvent("annap-arrival-complete", { detail: { full: false } })); } catch (_) {}
    }

    async function performNormal(scene) {
        setHtmlChrome(true);
        applyI18n(scene);
        seedTableLabel(scene);

        phase(scene, "desk");
        await wait(T.deskSettle);

        phase(scene, "arrive");
        await wait(T.envelopeArrive);

        await wait(T.pauseSealed);

        phase(scene, "open");
        await wait(T.flapOpen);

        phase(scene, "light");
        await wait(T.lightBloom);

        phase(scene, "letter");
        await wait(T.letterRise);

        phase(scene, "text");
        await wait(T.textReveal);

        await wait(T.textPause);

        phase(scene, "mark");
        await wait(T.markAppear);

        await wait(T.markPause);

        unlockMainInteractivity();
        phase(scene, "dismiss");
        await wait(T.dismissDuration);

        finish(scene, { full: true });
    }

    async function performReduced(scene) {
        setHtmlChrome(true);
        scene.classList.add(CLS + "reduced");
        applyI18n(scene);
        seedTableLabel(scene);
        ["desk","arrive","open","light","letter","text","mark"].forEach(function (p) {
            scene.classList.add(CLS + "choreo-" + p);
        });
        unlockMainInteractivity();
        await wait(32);
        phase(scene, "dismiss");
        await wait(80);
        finish(scene);
    }

    function boot() {
        var scene = document.getElementById("annap-arrival-scene");
        if (!scene) return;

        try {
            if (sessionStorage.getItem("annap_arrival_done") === "1") {
                skipImmediate(scene);
                return;
            }
        } catch (_) {}

        if (skipByQuery()) {
            skipImmediate(scene);
            return;
        }

        scene.style.pointerEvents = "none";

        function start() {
            applyI18n(scene);
            if (reducedMotion()) {
                void performReduced(scene);
            } else {
                void performNormal(scene);
            }
        }

        if (window.LuxuryI18n && window.LuxuryI18n.ready && typeof window.LuxuryI18n.ready.then === "function") {
            window.LuxuryI18n.ready.then(start).catch(start);
        } else {
            start();
        }
    }

    window.addEventListener("load", boot, { once: true });

    window.addEventListener("pageshow", function (ev) {
        if (!ev.persisted) return;
        setHtmlChrome(false);
        var scene = document.getElementById("annap-arrival-scene");
        if (scene) {
            scene.classList.add(CLS + "done");
            scene.setAttribute("aria-hidden", "true");
        }
    });
})(window, document);
