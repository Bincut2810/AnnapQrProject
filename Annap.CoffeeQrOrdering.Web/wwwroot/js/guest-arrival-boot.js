/**
 * Minimal boot for menu-first seated QR arrival (Phase 1B).
 * Binds table context, i18n DOM, vt nav patches, and order tray — skips ritual/sommelier stack.
 */
(function (global) {
    "use strict";

    function readGuestCtx() {
        var el = document.getElementById("menu-guest-context");
        if (!el || !el.textContent) return null;
        try {
            return JSON.parse(el.textContent);
        } catch (_e) {
            return null;
        }
    }

    function markBootSkipped() {
        var h = global.AnnapGuestBootHarness;
        if (!h || typeof h.markSkipped !== "function") return;
        ["menu", "mood", "sommelier", "signalr", "hydration", "queue", "observers"].forEach(function (k) {
            h.markSkipped(k);
        });
    }

    function bootSeatedArrival() {
        var ctx = readGuestCtx();
        var vt = ctx && ctx.venueTableId ? String(ctx.venueTableId) : "";
        var root = document.getElementById("guest-arrival-slim");
        if (!vt && root) {
            vt = (root.getAttribute("data-vt") || "").trim();
        }
        if (vt && global.GuestInteractionContract && global.GuestInteractionContract.navigation) {
            global.GuestInteractionContract.navigation.applyVtNavPatches();
        } else if (vt) {
            try {
                sessionStorage.setItem("annap_venue_table_id", vt);
            } catch (_ss) {
                /* ignore */
            }
        }
        if (global.GuestInteractionContract && global.GuestInteractionContract.navigation) {
            global.GuestInteractionContract.navigation.applyVtNavPatches();
        }
        if (global.LuxuryI18n && typeof global.LuxuryI18n.applyDom === "function") {
            global.LuxuryI18n.applyDom();
        }
        if (typeof global.annapStartOrderTrayDock === "function") {
            global.annapStartOrderTrayDock();
        }
        markBootSkipped();
    }

    function startSeatedArrivalBoot() {
        if (global.LuxuryI18n && global.LuxuryI18n.ready && typeof global.LuxuryI18n.ready.then === "function") {
            global.LuxuryI18n.ready.then(bootSeatedArrival).catch(bootSeatedArrival);
        } else {
            bootSeatedArrival();
        }
    }

    global.annapStartSeatedArrivalBoot = startSeatedArrivalBoot;
})(typeof window !== "undefined" ? window : globalThis);
