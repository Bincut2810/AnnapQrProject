/* Annap — shared order tray dock (Menu + seated Home) */
function annapBootOff(key) {
    var b = typeof window.AnnapGuestBoot !== "undefined" ? window.AnnapGuestBoot : {};
    return b[key] === true;
}
const GUEST_CTX = (function () {
    const n = document.getElementById("menu-guest-context");
    if (!n) return {};
    try {
        return JSON.parse(n.textContent || "{}");
    } catch (_annap) {
        return {};
    }
})();
let VENUE_TABLE_ID = "";
let menuOrderIdempotencyKey = null;
let menuOrderSubmitInFlight = false;
function __annapTrayDevOn() {
    var b = typeof window.AnnapGuestBoot !== "undefined" ? window.AnnapGuestBoot : {};
    return window.__ANNAP_DEBUG === true || b.showBootChecklist === true;
}
function __annapTrayLog() {
    if (!__annapTrayDevOn()) return;
    try {
        window.console.log.apply(window.console, arguments);
    } catch (_e) {}
}
function __annapTrayErr() {
    if (!__annapTrayDevOn()) return;
    try {
        window.console.error.apply(window.console, arguments);
    } catch (_e2) {}
}

function __annapModalDebugOn() {
    try {
        if (window.__ANNAP_DEBUG === true) return true;
        if (typeof sessionStorage !== "undefined" && sessionStorage.getItem("annap_modal_debug") === "1") return true;
    } catch (_) {}
    return false;
}
function __annapModalLog(msg, data) {
    if (!__annapModalDebugOn()) return;
    try {
        var prefix = "[ANNAP MODAL] " + msg;
        if (data !== undefined) window.console.log(prefix, data);
        else window.console.log(prefix);
    } catch (_) {}
}

/** Opt-in: set window.__ANNAP_MENU_BOOT_TRACE = true, or sessionStorage annap_menu_boot_trace=1, then reload. */
function __annapMenuBootTraceOn() {
    try {
        if (typeof window !== "undefined" && window.__ANNAP_MENU_BOOT_TRACE === true) return true;
        if (typeof window !== "undefined" && window.__ANNAP_DEBUG === true) return true;
        var b = typeof window.AnnapGuestBoot !== "undefined" ? window.AnnapGuestBoot : {};
        if (b.showBootChecklist === true) return true;
        if (typeof sessionStorage !== "undefined" && sessionStorage.getItem("annap_menu_boot_trace") === "1")
            return true;
    } catch (_t) {}
    return false;
}

function __annapMenuBootErr() {
    try {
        window.console.error.apply(window.console, arguments);
    } catch (_e) {}
}

function __annapMenuRuntimeJsonSelfCheck() {
    var problems = [];
    function checkJsonScript(id, label) {
        var node = document.getElementById(id);
        if (!node) {
            problems.push({ id: id, label: label, issue: "missing element" });
            return;
        }
        var raw = node.textContent || "";
        if (!String(raw).trim()) {
            problems.push({ id: id, label: label, issue: "empty body" });
            return;
        }
        try {
            JSON.parse(raw);
        } catch (e) {
            problems.push({ id: id, label: label, issue: "parse failed", message: e && e.message ? e.message : String(e) });
        }
    }
    checkJsonScript("menu-catalog-json", "catalog");
    checkJsonScript("menu-guest-context", "guest context");
    if (!problems.length) return;
    __annapMenuBootErr("[ANNAP MENU] runtime JSON invalid or missing", problems);
}
        function syncCartKey() {
            if (typeof GuestInteractionContract === "undefined") {
                VENUE_TABLE_ID = "";
                const hid0 = document.getElementById("menuVenueTableId");
                if (hid0) hid0.value = "";
                return;
            }
            var serverVtHint = String((GUEST_CTX && GUEST_CTX.venueTableId) || "").trim();
            GuestInteractionContract.bindPageContext({
                serverVt: serverVtHint,
                guestCtx: GUEST_CTX,
                migrateCatalog: {
                    catalogRow,
                    selectionFallback: () => tOrder("cart.selectionFallback") || "Selection"
                }
            });
            VENUE_TABLE_ID = GuestInteractionContract.getVenueTableId();
            const hid = document.getElementById("menuVenueTableId");
            if (hid) hid.value = VENUE_TABLE_ID || "";
        }

        function tOrder(k) {
            return (window.LuxuryI18n && window.LuxuryI18n.t(k)) || "";
        }

        function tfmt(path, vars) {
            if (window.LuxuryI18n && window.LuxuryI18n.tf) {
                const s = window.LuxuryI18n.tf(path, vars);
                if (s) return s;
            }
            let s = tOrder(path);
            if (!s || !vars) return s;
            for (const k of Object.keys(vars)) s = s.split("{" + k + "}").join(String(vars[k]));
            return s;
        }

        function refreshTableIdentityUi() {
            syncCartKey();
            const idBlock = document.getElementById("menu-table-identity");
            const needQr = document.getElementById("menu-table-need-qr");
            const primary = document.getElementById("menu-table-primary");
            const btn = document.getElementById("menuSubmitBtn");
            const has = !!VENUE_TABLE_ID;
            if (idBlock) idBlock.classList.toggle("hidden", !has);
            if (needQr) needQr.classList.toggle("hidden", has);
            if (btn) {
                if (has) btn.removeAttribute("disabled");
                else btn.setAttribute("disabled", "disabled");
            }
            document.querySelectorAll(".menu-add-btn").forEach((b) => {
                b.disabled = !has;
                b.classList.toggle("opacity-45", !has);
                b.classList.toggle("pointer-events-none", !has);
            });
            if (primary) {
                const lab = String(GUEST_CTX.label || "").trim();
                if (lab) {
                    const seated = tOrder("cart.seatedAt");
                    primary.textContent = seated ? `${seated} ${lab}` : lab;
                } else primary.textContent = tOrder("cart.linkedTable") || "Your table";
            }
            if (window.LuxuryI18n) window.LuxuryI18n.applyDom();
        }

        function formatMoneySafe(n) {
            const v = Number(n);
            if (!Number.isFinite(v)) return "";
            if (typeof GuestInteractionContract !== "undefined" && GuestInteractionContract && GuestInteractionContract.formatMoney) {
                try {
                    return GuestInteractionContract.formatMoney(v);
                } catch (_fmt) {}
            }
            try {
                const isVi =
                    (window.LuxuryI18n && window.LuxuryI18n.getLang && window.LuxuryI18n.getLang() === "vi") ||
                    (document.documentElement.lang || "").toLowerCase().startsWith("vi");
                const rounded = Math.round(v);
                if (isVi) {
                    return new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 0 }).format(rounded) + "đ";
                }
                return new Intl.NumberFormat("en-US", { style: "currency", currency: "USD", maximumFractionDigits: 0 }).format(rounded);
            } catch (_intl) {
                return String(v);
            }
        }

        const money = { format: formatMoneySafe };

        const CATALOG = (function () {
            const node = document.getElementById("menu-catalog-json");
            if (!node) return [];
            try {
                return JSON.parse(node.textContent || "[]");
            } catch (_annap) {
                return [];
            }
        })();

        function catalogRow(id) {
            return CATALOG.find((r) => String(r.id) === String(id));
        }

        function cartLineKey(l) {
            const gl = (l.guestLabel && String(l.guestLabel).trim()) || "";
            return String(l.menuItemId) + "\u0001" + gl;
        }

        const cartItems = new Map(); /* line key -> { id, guestLabel, name, unitPrice, qty } */

        const orderTrayRoot = document.getElementById("order-tray-root");
        const orderTrayBackdrop = document.getElementById("order-tray-backdrop");
        const orderTraySheet = document.getElementById("order-tray-sheet");
        const orderTrayChip = document.getElementById("order-tray-chip");
        const orderTrayChevron = orderTrayRoot ? orderTrayRoot.querySelector(".order-tray-chevron") : null;
        const orderTrayHeader = orderTrayRoot ? orderTrayRoot.querySelector(".annap-tray-header") : null;
        const orderTrayImpact = document.getElementById("order-tray-chip-impact");
        if (orderTrayChip) orderTrayChip.setAttribute("data-cart-target", "order-tray");
        if (window.AddToOrderAnimationProvider) {
            window.AddToOrderAnimationProvider.registerCartTarget(orderTrayImpact || orderTrayChip);
        }
        let trayOpen = false;
        let trayDrag = null;
        function linesToCartMap(lines) {
            cartItems.clear();
            for (const l of lines) {
                const gl = (l.guestLabel && String(l.guestLabel).trim()) || "";
                cartItems.set(cartLineKey(l), {
                    id: l.menuItemId,
                    guestLabel: gl,
                    name: l.name,
                    unitPrice: l.unitPrice,
                    qty: l.qty,
                    imageSrc: l.imageSrc || ""
                });
            }
        }

        function loadCart() {
            if (typeof GuestInteractionContract === "undefined") {
                cartItems.clear();
                return;
            }
            syncCartKey();
            linesToCartMap(GuestInteractionContract.getCartLines());
        }

        function imageFromSource(el) {
            if (!el) return "";
            const ds = el.dataset || {};
            if (ds.itemImage) return String(ds.itemImage).trim();
            const root = el.closest ? el.closest("[data-drink-card], .ge-result-card, .ge-disc-rec, .dd-pairing, .dd-passport, article, section") : null;
            const img = root && root.querySelector ? root.querySelector("img") : null;
            if (img) return String(img.currentSrc || img.getAttribute("src") || "").trim();
            const detailImg = document.querySelector("#drink-detail-mount .dd-product__img");
            return detailImg ? String(detailImg.currentSrc || detailImg.getAttribute("src") || "").trim() : "";
        }

        function lineFromSource(menuItemId, el) {
            const ds = el && el.dataset;
            const name = ds && ds.itemName != null ? String(ds.itemName).trim() : "";
            const priceRaw = ds ? ds.itemPrice : void 0;
            const price = priceRaw != null && priceRaw !== "" ? Number(priceRaw) : NaN;
            const imageSrc = imageFromSource(el);
            if (name && Number.isFinite(price)) {
                return { id: menuItemId, name: name, unitPrice: price, qty: 1, imageSrc };
            }
            const row = catalogRow(menuItemId);
            const rowName = row && row.name;
            const rowPrice = row && row.price;
            return {
                id: menuItemId,
                name: rowName !== null && rowName !== undefined ? rowName : (tOrder("cart.selectionFallback") || "Selection"),
                unitPrice: Number(rowPrice !== null && rowPrice !== undefined ? rowPrice : 0),
                qty: 1,
                imageSrc
            };
        }

        let trayLockScrollY = 0;

        function setTrayOpenImmediate(open) {
            const next = !!open;
            const useBodyLock =
                typeof window.matchMedia === "function" && window.matchMedia("(min-width: 640px)").matches;

            if (next && !trayOpen && useBodyLock) {
                trayLockScrollY = window.scrollY || document.documentElement.scrollTop || 0;
                document.documentElement.style.setProperty("--tray-lock-y", `-${trayLockScrollY}px`);
                document.body.style.top = `-${trayLockScrollY}px`;
            }
            if (!next && trayOpen) {
                document.body.style.top = "";
                document.documentElement.style.removeProperty("--tray-lock-y");
                const wasLocked = document.body.classList.contains("order-tray-locked");
                document.body.classList.remove("order-tray-locked");
                if (wasLocked) {
                    try {
                        window.scrollTo(0, trayLockScrollY);
                    } catch (_st) {
                        /* ignore */
                    }
                }
            }

            trayOpen = next;
            document.body.classList.toggle("order-tray-locked", !!(trayOpen && useBodyLock));
            if (orderTrayRoot) orderTrayRoot.classList.toggle("is-open", trayOpen);
            if (orderTraySheet) {
                orderTraySheet.classList.toggle("order-tray-sheet--closed", !trayOpen);
                orderTraySheet.classList.toggle("pointer-events-none", !trayOpen);
                orderTraySheet.classList.toggle("pointer-events-auto", trayOpen);
            }
            if (orderTrayChip) orderTrayChip.setAttribute("aria-expanded", trayOpen ? "true" : "false");
            if (orderTrayRoot) orderTrayRoot.classList.toggle("order-tray-root--sheet-open", trayOpen);
            if (orderTrayBackdrop) {
                if (trayOpen) {
                    orderTrayBackdrop.classList.remove("hidden", "pointer-events-none", "opacity-0");
                    orderTrayBackdrop.classList.add("pointer-events-auto", "opacity-100", "is-visible");
                    orderTrayBackdrop.setAttribute("aria-hidden", "false");
                } else {
                    orderTrayBackdrop.classList.add("hidden", "pointer-events-none", "opacity-0");
                    orderTrayBackdrop.classList.remove("pointer-events-auto", "opacity-100", "is-visible");
                    orderTrayBackdrop.setAttribute("aria-hidden", "true");
                }
            }
        }

        function setTrayOpen(open, opts) {
            opts = opts || {};
            if (open && !opts.userIntent) {
                return;
            }
            setTrayOpenImmediate(!!open);
        }

        function toggleTray() {
            setTrayOpen(!trayOpen, { userIntent: true });
        }

        if (orderTrayChip) orderTrayChip.addEventListener("click", function () {
            toggleTray();
        });
        if (orderTrayBackdrop) orderTrayBackdrop.addEventListener("click", function () {
            setTrayOpen(false, { userIntent: true });
        });

        const trayCloseBtn = document.getElementById("annap-tray-close");
        if (trayCloseBtn && !trayCloseBtn.dataset.annapBound) {
            trayCloseBtn.dataset.annapBound = "1";
            trayCloseBtn.addEventListener("click", function (ev) {
                ev.preventDefault();
                ev.stopPropagation();
                setTrayOpen(false, { userIntent: true });
            });
        }

        function trayIsMobileSheet() {
            try {
                return typeof window.matchMedia === "function" && window.matchMedia("(max-width: 640px)").matches;
            } catch (_tm) {
                return false;
            }
        }

        function resetTrayDragStyles() {
            if (!orderTraySheet) return;
            orderTraySheet.classList.remove("order-tray-sheet--dragging");
            orderTraySheet.style.removeProperty("--order-tray-drag-y");
        }

        function bindTraySwipeToClose() {
            if (!orderTrayHeader || !orderTraySheet) return;
            if (orderTrayHeader.dataset.annapSwipeCloseBound === "1") return;
            orderTrayHeader.dataset.annapSwipeCloseBound = "1";

            orderTrayHeader.addEventListener("pointerdown", function (ev) {
                if (!trayOpen || !trayIsMobileSheet()) return;
                if (ev.button !== 0 || ev.isPrimary === false) return;
                const target = ev.target;
                if (target && target.closest && target.closest("button,a,input,select,textarea")) return;

                trayDrag = {
                    id: ev.pointerId,
                    x: ev.clientX,
                    y: ev.clientY,
                    lastY: ev.clientY,
                    startedAt: Date.now()
                };
                orderTraySheet.classList.add("order-tray-sheet--dragging");
                try {
                    orderTrayHeader.setPointerCapture(ev.pointerId);
                } catch (_pc) {
                    /* ignore */
                }
            }, { passive: true });

            orderTrayHeader.addEventListener("pointermove", function (ev) {
                if (!trayDrag || ev.pointerId !== trayDrag.id || !orderTraySheet) return;
                const dy = Math.max(0, ev.clientY - trayDrag.y);
                const dx = Math.abs(ev.clientX - trayDrag.x);
                if (dx > 56 && dx > dy * 1.2) return;
                trayDrag.lastY = ev.clientY;
                orderTraySheet.style.setProperty("--order-tray-drag-y", Math.min(96, dy).toFixed(0) + "px");
                if (dy > 6) {
                    try { ev.preventDefault(); } catch (_pd) { /* ignore */ }
                }
            }, { passive: false });

            function finish(ev) {
                if (!trayDrag || ev.pointerId !== trayDrag.id) return;
                const dy = ev.clientY - trayDrag.y;
                const elapsed = Math.max(1, Date.now() - trayDrag.startedAt);
                const velocity = dy / elapsed;
                const shouldClose = dy > 58 || (dy > 34 && velocity > 0.45);
                trayDrag = null;
                resetTrayDragStyles();
                if (shouldClose) setTrayOpen(false, { userIntent: true });
            }

            orderTrayHeader.addEventListener("pointerup", finish, { passive: true });
            orderTrayHeader.addEventListener("pointercancel", function (ev) {
                if (!trayDrag || ev.pointerId !== trayDrag.id) return;
                trayDrag = null;
                resetTrayDragStyles();
            }, { passive: true });
        }

        bindTraySwipeToClose();
        /* Closed tray: backdrop is full-viewport; setTrayOpen(false) is a no-op when trayOpen is
           already false — must call setTrayOpenImmediate so backdrop releases hit-testing immediately. */
        try {
            setTrayOpenImmediate(false);
        } catch (_trayInit) {}
        try {
            syncCartKey();
            refreshTableIdentityUi();
        } catch (_earlyTray) {
            /* GuestInteractionContract or tray DOM not ready yet — bootMenuTray will retry. */
        }

        let _annapMenuActiveCatId = null;

        function setActiveCategory(catId) {
            if (catId != null && String(catId) === String(_annapMenuActiveCatId)) return;
            _annapMenuActiveCatId = catId != null ? String(catId) : null;
            document.querySelectorAll("[data-cat-nav]").forEach((btn) => {
                const on = String(btn.getAttribute("data-cat-nav") || "") === String(catId != null ? catId : "");
                btn.classList.toggle("is-active", on);
                btn.classList.toggle("text-[rgb(var(--fg))]", on);
                btn.classList.toggle("ring-[rgb(var(--accent))]/35", on);
                btn.classList.toggle("bg-white/[0.06]", on);
                btn.classList.toggle("shadow-[0_0_0_1px_rgba(199,168,122,0.2)]", on);
            });
        }

        /* ── Editorial scroll engine ────────────────────────────────────────────────
           Category navigation must feel like drifting into the next section of a
           tasting journal — not snapping to a filter tab. Native scrollIntoView uses
           browser-controlled timing that reads as mechanical and app-like.

           Easing: cubic-bezier(0.16, 1, 0.3, 1) — doctrine --ease-arrive curve.
           The section arrives quickly (~88% of distance in first 30% of time), then
           the view breathes and settles gently for the remaining duration.

           Duration: 880ms for vertical section scroll (Ceremonial tier — scene arrival).
           Duration: 360ms for horizontal chip rail centering (Brief/Standard).
        ─────────────────────────────────────────────────────────────────────────── */
        var _annapEaseArrive = (function () {
            var p1x = 0.16, p1y = 1.0, p2x = 0.3, p2y = 1.0;
            var c1x = 3 * p1x;
            var c2x = 3 * (p2x - p1x) - c1x;
            var c3x = 1 - c1x - c2x;
            var c1y = 3 * p1y;
            var c2y = 3 * (p2y - p1y) - c1y;
            var c3y = 1 - c1y - c2y;
            function sx(t) { return ((c3x * t + c2x) * t + c1x) * t; }
            function sy(t) { return ((c3y * t + c2y) * t + c1y) * t; }
            function dsx(t) { return (3 * c3x * t + 2 * c2x) * t + c1x; }
            return function (x) {
                if (x <= 0) return 0;
                if (x >= 1) return 1;
                var t = x;
                for (var i = 0; i < 8; i++) {
                    var slope = dsx(t);
                    if (Math.abs(slope) < 0.000001) break;
                    t -= (sx(t) - x) / slope;
                }
                return sy(t);
            };
        }());

        var _annapSectionRaf = 0;

        function annapEditorialScrollTo(targetY, durationMs) {
            if (_annapSectionRaf) { window.cancelAnimationFrame(_annapSectionRaf); _annapSectionRaf = 0; }
            var startY = window.scrollY !== undefined ? window.scrollY : (document.documentElement.scrollTop || 0);
            var dist = targetY - startY;
            if (Math.abs(dist) < 2) return;
            var t0 = 0;
            function step(ts) {
                if (!t0) t0 = ts;
                var p = Math.min((ts - t0) / durationMs, 1);
                window.scrollTo(0, startY + dist * _annapEaseArrive(p));
                if (p < 1) {
                    _annapSectionRaf = window.requestAnimationFrame(step);
                } else {
                    _annapSectionRaf = 0;
                }
            }
            _annapSectionRaf = window.requestAnimationFrame(step);
        }

        var _annapRailRaf = 0;

        // Exposed for the chip rail horizontal scroll in Menu/Index.cshtml inline script.
        window.annapEditorialScrollH = function (el, targetLeft, durationMs) {
            if (_annapRailRaf) { window.cancelAnimationFrame(_annapRailRaf); _annapRailRaf = 0; }
            var startLeft = el.scrollLeft;
            var dist = targetLeft - startLeft;
            if (Math.abs(dist) < 2) return;
            var t0 = 0;
            function step(ts) {
                if (!t0) t0 = ts;
                var p = Math.min((ts - t0) / durationMs, 1);
                el.scrollLeft = startLeft + dist * _annapEaseArrive(p);
                if (p < 1) {
                    _annapRailRaf = window.requestAnimationFrame(step);
                } else {
                    _annapRailRaf = 0;
                }
            }
            _annapRailRaf = window.requestAnimationFrame(step);
        };

        window.addEventListener(
            "pagehide",
            function () {
                if (_annapSectionRaf) {
                    window.cancelAnimationFrame(_annapSectionRaf);
                    _annapSectionRaf = 0;
                }
                if (_annapRailRaf) {
                    window.cancelAnimationFrame(_annapRailRaf);
                    _annapRailRaf = 0;
                }
            },
            { passive: true }
        );

        /**
         * Jump to a menu category using editorial RAF scroll with the doctrine
         * --ease-arrive curve. Reads scroll-margin-top from computed styles so
         * the sticky category rail is always respected.
         */
        function scrollToCategory(catId) {
            const raw = catId != null ? String(catId) : "";
            if (!raw) return;
            const esc = typeof CSS !== "undefined" && CSS.escape ? CSS.escape(raw) : raw.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
            const el = document.querySelector(`[data-cat-section="${esc}"]`);
            if (!el) return;
            const reduceMotion =
                typeof window.matchMedia === "function" && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
            const coarsePointer =
                typeof window.matchMedia === "function" &&
                (window.matchMedia("(hover: none)").matches || window.matchMedia("(pointer: coarse)").matches);
            if (reduceMotion || coarsePointer) {
                try { el.scrollIntoView({ block: "start", inline: "nearest", behavior: "auto" }); } catch (_e) { try { el.scrollIntoView(true); } catch (_e2) { /* ignore */ } }
                return;
            }
            const rect = el.getBoundingClientRect();
            const scrollY = window.scrollY !== undefined ? window.scrollY : (document.documentElement.scrollTop || 0);
            const absoluteTop = rect.top + scrollY;
            let scrollMarginTop = 0;
            try { scrollMarginTop = parseFloat(window.getComputedStyle(el).scrollMarginTop) || 0; } catch (_m) {}
            annapEditorialScrollTo(absoluteTop - scrollMarginTop, 880);
        }

        document.querySelectorAll("[data-cat-nav]").forEach((btn) => {
            btn.addEventListener("click", function (ev) {
                ev.preventDefault();
                const id = btn.getAttribute("data-cat-nav");
                if (id) {
                    setActiveCategory(id);
                    scrollToCategory(id);
                }
            });
        });

        const sections = Array.from(document.querySelectorAll("[data-cat-section]"));
        if (sections.length) {
            if (!annapBootOff("disableGuestObservers") && typeof IntersectionObserver !== "undefined") {
                const coarseIo =
                    typeof window.matchMedia === "function" &&
                    (window.matchMedia("(hover: none)").matches || window.matchMedia("(pointer: coarse)").matches);
                let catObsRaf = 0;
                let catObsPendingId = null;
                const io = new IntersectionObserver(
                    (entries) => {
                        const visible = entries
                            .filter((e) => e.isIntersecting)
                            .sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0];
                        if (!visible || !visible.target) return;
                        const id = visible.target.getAttribute("data-cat-section");
                        if (!id) return;
                        catObsPendingId = id;
                        if (catObsRaf) return;
                        catObsRaf = window.requestAnimationFrame(function () {
                            catObsRaf = 0;
                            const pid = catObsPendingId;
                            catObsPendingId = null;
                            if (pid) setActiveCategory(pid);
                        });
                    },
                    coarseIo
                        ? { root: null, rootMargin: "-14% 0px -22% 0px", threshold: [0.03, 0.08, 0.14, 0.22] }
                        : { root: null, rootMargin: "-38% 0px -45% 0px", threshold: [0.08, 0.15, 0.25, 0.4] }
                );
                sections.forEach((s) => io.observe(s));
                if (window.AnnapGuestBootHarness) AnnapGuestBootHarness.row("observers", "ok");
            } else {
                if (window.AnnapGuestBootHarness) AnnapGuestBootHarness.markSkipped("observers");
            }
            const first = sections[0].getAttribute("data-cat-section");
            if (first) setActiveCategory(first);
        }

        function totalQty() {
            let n = 0;
            for (const l of cartItems.values()) n += l.qty;
            return n;
        }

        function subtotal() {
            let s = 0;
            for (const l of cartItems.values()) s += l.qty * l.unitPrice;
            return s;
        }

        function isSommelierFlowActive() {
            return !!(document.body && document.body.classList.contains("ge-sommelier-flow"));
        }

        function reducedMotionTray() {
            try {
                return (
                    document.documentElement.classList.contains("annap-guest-no-motion") ||
                    (typeof window.matchMedia === "function" &&
                        window.matchMedia("(prefers-reduced-motion: reduce)").matches)
                );
            } catch (_rm) {
                return false;
            }
        }

        function trayCopyFallback(key, vi, en) {
            const t = tOrder(key);
            if (t) return t;
            const isVi =
                (window.LuxuryI18n && window.LuxuryI18n.getLang && window.LuxuryI18n.getLang() === "vi") ||
                (document.documentElement.lang || "").toLowerCase().startsWith("vi");
            return isVi ? vi : en;
        }

        function updateTraySummary(opts) {
            opts = opts || {};
            const n = totalQty();
            const line = document.getElementById("trayCountLine");
            const chipTitle = document.getElementById("order-tray-chip-title");
            const chipSub = document.getElementById("order-tray-chip-sub");
            const chipStack = document.getElementById("order-tray-chip-stack");
            const chipTotal = document.getElementById("order-tray-chip-total");
            const subEl = document.getElementById("order-tray-subtotal");
            const st = subtotal();
            if (orderTrayRoot) orderTrayRoot.classList.toggle("order-tray-root--empty", n === 0);
            if (orderTrayChip) orderTrayChip.classList.toggle("order-tray-chip--has-items", n > 0);

            if (line) {
                if (n === 0) line.textContent = trayCopyFallback("menuTray.countNone", "Chưa có món trên khay.", "No items on your tray yet.");
                else if (n === 1) line.textContent = trayCopyFallback("menuTray.countOne", "Một món trên khay.", "One item on your tray.");
                else line.textContent = tfmt("menuTray.countMany", { n }) || trayCopyFallback("menuTray.countMany", n + " món trên khay.", n + " items on your tray.");
            }

            if (chipTitle) {
                chipTitle.classList.remove("order-tray-chip__count--settling");
                if (n === 0) {
                    chipTitle.textContent = isSommelierFlowActive()
                        ? trayCopyFallback("menuTray.chipEmptySomm", "Khay đang chờ", "Tray awaiting")
                        : trayCopyFallback("menuTray.chipEmpty", "Khay đang chờ", "Tray awaiting");
                } else if (n === 1) {
                    chipTitle.textContent = trayCopyFallback("menuTray.chipOne", "1 món đã đặt", "1 item placed");
                } else {
                    chipTitle.textContent = isSommelierFlowActive()
                        ? tfmt("menuTray.chipManySomm", { n }) ||
                          tfmt("menuTray.chipMany", { n }) ||
                          trayCopyFallback("menuTray.chipMany", n + " món đã đặt", n + " items placed")
                        : tfmt("menuTray.chipMany", { n }) ||
                          trayCopyFallback("menuTray.chipMany", n + " món đã đặt", n + " items placed");
                }
                if (opts.animateChip && n > 0 && !reducedMotionTray()) {
                    void chipTitle.offsetWidth;
                    chipTitle.classList.add("order-tray-chip__count--settling");
                }
            }
            if (chipSub) {
                chipSub.textContent =
                    n === 0
                        ? isSommelierFlowActive()
                            ? trayCopyFallback("menuTray.chipSubSomm", "Chọn một ly để bắt đầu.", "Choose a drink to begin.")
                            : trayCopyFallback("menuTray.chipSubEmpty", "Chọn một ly để bắt đầu.", "Choose a drink to begin.")
                        : trayCopyFallback("menuTray.chipSubFilled", "Chạm để xem khay", "Tap to view tray");
            }
            if (chipTotal) {
                if (n === 0) {
                    chipTotal.textContent = "\u2014";
                    chipTotal.classList.add("order-tray-chip__total--empty");
                    chipTotal.setAttribute("aria-hidden", "true");
                } else {
                    chipTotal.textContent = money.format(st);
                    chipTotal.classList.remove("order-tray-chip__total--empty");
                    chipTotal.removeAttribute("aria-hidden");
                }
            }
            if (chipStack) {
                const recent = Array.from(cartItems.values()).slice(-1);
                if (!recent.length) {
                    chipStack.innerHTML = '<span class="order-tray-chip__empty-line"></span>';
                    chipStack.classList.remove("annap-tray-slot-receive");
                } else {
                    chipStack.innerHTML = recent
                        .map((l, i) => {
                            const initial = escapeHtml(String(l.name || "A").trim().charAt(0).toUpperCase() || "A");
                            const img = l.imageSrc
                                ? `<img src="${escapeHtml(l.imageSrc)}" alt="" decoding="async" loading="lazy" />`
                                : initial;
                            return `<span class="order-tray-chip__card" style="--tray-card-i:${i}">${img}</span>`;
                        })
                        .join("");
                    if (opts.animateChip && !reducedMotionTray()) {
                        chipStack.classList.remove("annap-tray-slot-receive");
                        void chipStack.offsetWidth;
                        chipStack.classList.add("annap-tray-slot-receive");
                    }
                }
            }
            if (subEl) {
                if (n === 0) {
                    subEl.textContent = "\u2014";
                    subEl.classList.add("order-tray-subtotal--empty");
                } else {
                    subEl.textContent = money.format(st);
                    subEl.classList.remove("order-tray-subtotal--empty");
                }
            }
            const chipGuest = document.getElementById("order-tray-chip-guest");
            if (chipGuest) {
                const gb =
                    typeof window.__annapGroupBrowseActive !== "undefined" &&
                    window.__annapGroupBrowseActive &&
                    typeof window.__annapGroupGuestCount !== "undefined" &&
                    parseInt(window.__annapGroupGuestCount, 10) >= 1;
                const activeLab =
                    typeof window.__annapActiveGroupGuestLabel === "string"
                        ? String(window.__annapActiveGroupGuestLabel).trim()
                        : "";
                if (gb && activeLab) {
                    const pour = tOrder("menuTray.chipGuestPouring") || "Pouring for";
                    chipGuest.textContent = `${pour} · ${activeLab}`;
                    chipGuest.classList.remove("hidden");
                } else {
                    chipGuest.textContent = "";
                    chipGuest.classList.add("hidden");
                }
            }
        }

        function addToCart(menuItemId, btn) {
            syncCartKey();
            if (!VENUE_TABLE_ID) return;
            if (typeof GuestInteractionContract === "undefined" || !GuestInteractionContract) {
                __annapMenuBootErr("[ANNAP MENU] addToCart: GuestInteractionContract missing");
                return;
            }
            const incoming = lineFromSource(menuItemId, btn);
            const gl =
                typeof window.__annapActiveGroupGuestLabel === "string"
                    ? window.__annapActiveGroupGuestLabel.trim()
                    : "";
            GuestInteractionContract.addItem({
                menuItemId,
                name: incoming.name,
                unitPrice: incoming.unitPrice,
                imageSrc: incoming.imageSrc,
                selectionFallback: tOrder("cart.selectionFallback") || "Selection",
                sourceElement: btn || null,
                guestLabel: gl
            });
            linesToCartMap(GuestInteractionContract.getCartLines());
            renderCart();
            updateTraySummary({ animateChip: !!btn });
        }

        window.annapAddToCart = (id, el) => addToCart(id, el);
        window.__detailAdd = window.annapAddToCart;

        function _detailModal() {
            return typeof window.DrinkDetailModal !== "undefined" ? window.DrinkDetailModal : null;
        }

        const _menuBrowseTapMeta = new Map();
        let _menuBrowseTouchHandledAt = 0; /* timestamp of last touch-pointerup action; guards click dedup */

        function annapClearMenuCardPress() {
            document.querySelectorAll(".menu-browse [data-drink-card].menu-card-reveal--pressed").forEach(function (c) {
                c.classList.remove("menu-card-reveal--pressed");
            });
        }

        function tryActivateMenuBrowseFromTarget(t, ev) {
            if (!t || !t.closest) return false;
            const browse = t.closest(".menu-browse");
            if (!browse) return false;

            const detailBtn = t.closest("[data-menu-detail]");
            if (detailBtn && browse.contains(detailBtn)) {
                if (ev) ev.preventDefault();
                const cid = detailBtn.getAttribute("data-menu-detail");
                if (cid) void openDrinkDetail(cid);
                return true;
            }

            const addBtn = t.closest(".menu-add-btn,[data-add]");
            if (addBtn && browse.contains(addBtn)) {
                if (addBtn.disabled) return false;
                if (ev) ev.preventDefault();
                const aid = addBtn.getAttribute("data-add");
                if (aid && typeof addToCart === "function") addToCart(aid, addBtn);
                return true;
            }

            const card = t.closest("[data-drink-card]");
            if (card && browse.contains(card) && !t.closest("button,a")) {
                if (ev) ev.preventDefault();
                const cid = card.getAttribute("data-drink-card");
                if (cid) void openDrinkDetail(cid);
                return true;
            }
            return false;
        }

        function annapOnMenuBrowsePointerUp(ev) {
            annapClearMenuCardPress();
            if (ev.pointerType !== "touch" && ev.pointerType !== "pen") return;
            if (!ev.isPrimary || ev.button !== 0) return;
            const meta = _menuBrowseTapMeta.get(ev.pointerId);
            _menuBrowseTapMeta.delete(ev.pointerId);
            if (!meta) return;
            const dx = ev.clientX - meta.x;
            const dy = ev.clientY - meta.y;
            if (dx * dx + dy * dy > 900) return;
            const t = ev.target;
            if (!t || !t.closest) return;
            const browse = t.closest(".menu-browse");
            if (!browse || browse !== meta.browse) return;
            if (tryActivateMenuBrowseFromTarget(t, ev)) {
                _menuBrowseTouchHandledAt = Date.now();
                try {
                    ev.preventDefault();
                } catch (_pe) {
                    /* ignore */
                }
            }
        }

        function annapOnMenuBrowsePointerCancel(ev) {
            _menuBrowseTapMeta.delete(ev.pointerId);
            annapClearMenuCardPress();
        }

        function annapBindMenuCatalogInteractions() {
            if (document.documentElement.dataset.annapMenuCatalogUi === "1") return;
            document.documentElement.dataset.annapMenuCatalogUi = "1";

            document.addEventListener(
                "click",
                function (ev) {
                    /* Skip if a touch pointerup already handled this exact tap (< 600 ms ago).
                       Safari iOS fires pointerup then a synthetic click on the same gesture. */
                    if (Date.now() - _menuBrowseTouchHandledAt < 600) return;
                    const t = ev.target;
                    if (!t || !t.closest) return;
                    const browse = t.closest(".menu-browse");
                    if (!browse) return;
                    tryActivateMenuBrowseFromTarget(t, ev);
                },
                false
            );

            document.addEventListener(
                "keydown",
                function (ev) {
                    if (ev.key !== "Enter" && ev.key !== " ") return;
                    const el = ev.target;
                    if (!el || !el.closest) return;
                    const browse = el.closest(".menu-browse");
                    if (!browse) return;
                    if (el.closest && el.closest("button,a")) return;
                    const card = el.closest("[data-drink-card]");
                    if (!card || !browse.contains(card)) return;
                    ev.preventDefault();
                    const cid = card.getAttribute("data-drink-card");
                    if (cid) void openDrinkDetail(cid);
                },
                false
            );

            document.addEventListener(
                "pointerdown",
                function (ev) {
                    const t = ev.target;
                    if (!t || !t.closest) return;
                    const browse = t.closest(".menu-browse");
                    if (!browse) return;
                    if ((ev.pointerType === "touch" || ev.pointerType === "pen") && ev.isPrimary && ev.button === 0) {
                        _menuBrowseTapMeta.set(ev.pointerId, {
                            x: ev.clientX,
                            y: ev.clientY,
                            browse: browse
                        });
                    }
                    const card = t.closest("[data-drink-card]");
                    if (!card || !browse.contains(card)) return;
                    if (t.closest("button,a")) return;
                    if (ev.pointerType === "mouse" && ev.button !== 0) return;
                    card.classList.add("menu-card-reveal--pressed");
                },
                true
            );
            document.addEventListener("pointerup", annapOnMenuBrowsePointerUp, true);
            document.addEventListener("pointercancel", annapOnMenuBrowsePointerCancel, true);
        }

        function annapBindDrinkDetailModal() {
            if (window._annapDrinkDetailModalBound) return;
            window._annapDrinkDetailModalBound = true;
            document.documentElement.dataset.annapDrinkDetailModal = "1";
            var dm = _detailModal();
            if (dm && typeof dm.init === "function") dm.init();
        }

        async function openDrinkDetail(id, options) {
            if (!document.getElementById("drink-detail-modal")) return;
            var dm = _detailModal();
            if (!dm || typeof dm.open !== "function") {
                __annapMenuBootErr("[ANNAP MENU] openDrinkDetail: DrinkDetailModal missing");
                return;
            }
            setTrayOpen(false);
            __annapModalLog("open", { id: id });
            return dm.open(id, options);
        }

        function closeDrinkDetail() {
            var dm = _detailModal();
            if (!dm || typeof dm.close !== "function") return;
            __annapModalLog("close");
            dm.close();
        }

        if (!window._annapTrayEscapeBound) {
            window._annapTrayEscapeBound = true;
            document.addEventListener("keydown", function (e) {
                if (e.key !== "Escape") return;
                var dm = _detailModal();
                if (dm && typeof dm.isOpen === "function" && dm.isOpen()) return;
                if (trayOpen) setTrayOpen(false);
            });
        }

        var _trayClear = document.getElementById("order-tray-clear");
        if (_trayClear) _trayClear.addEventListener("click", function () {
            clearCart();
        });

        function clearCart() {
            GuestInteractionContract.clearCart();
            linesToCartMap(GuestInteractionContract.getCartLines());
            renderCart();
            const orderResult = document.getElementById("orderResult");
            if (orderResult) orderResult.textContent = "";
            const btn = document.getElementById("menuSubmitBtn");
            if (btn) {
                btn.classList.remove("order-tray-submit--success", "order-tray-submit--pulse");
                btn.textContent = tOrder("menuTray.requestPreparation") || "Request preparation";
            }
            updateTraySummary();
            refreshTableIdentityUi();
        }

        function setQty(id, delta, guestLabel) {
            const gl = guestLabel !== undefined && guestLabel !== null ? String(guestLabel).trim() : "";
            GuestInteractionContract.adjustItemQuantity(id, delta, gl);
            linesToCartMap(GuestInteractionContract.getCartLines());
            renderCart();
            updateTraySummary();
        }

        function removeLine(id, guestLabel) {
            const gl = guestLabel !== undefined && guestLabel !== null ? String(guestLabel).trim() : "";
            GuestInteractionContract.removeItem(id, gl);
            linesToCartMap(GuestInteractionContract.getCartLines());
            renderCart();
            updateTraySummary();
        }

        function renderCart() {
            const el = document.getElementById("cart");
            if (!el) return;
            const allLines = Array.from(cartItems.values());
            const guestCount =
                typeof window.__annapGroupGuestCount !== "undefined" && window.__annapGroupGuestCount != null
                    ? parseInt(window.__annapGroupGuestCount, 10)
                    : 0;
            const hasGic = typeof GuestInteractionContract !== "undefined" && GuestInteractionContract;
            const useGrouped =
                hasGic &&
                typeof GuestInteractionContract.buildGroupOrderTraySections === "function" &&
                typeof window.__annapGroupBrowseActive !== "undefined" &&
                window.__annapGroupBrowseActive &&
                guestCount >= 1;

            if (cartItems.size === 0) {
                el.className = "order-tray-lines order-tray-lines--correspondence pb-1 text-sm";
                const emptyMsg = escapeHtml(tOrder("menuTray.emptyBody") || "");
                el.innerHTML = `<p class="order-tray-empty">${emptyMsg || trayCopyFallback("menuTray.emptyBody", "Thêm một ly từ gợi ý hoặc thực đơn — khay sẽ hiện tại đây.", "Add a cup from a suggestion or the menu — it will appear here.")}</p>`;
                return;
            }

            function descriptorForLine(line) {
                const gl = line.guestLabel || "";
                if (gl) return gl;
                return trayCopyFallback("menuTray.lineDescriptor", "Pha tại 106/1", "Curated at 106/1");
            }

            function appendMenuLineRow(line, rowIndex) {
                const card = document.createElement("article");
                card.className = "tray-correspondence-card";
                card.style.setProperty("--tray-line-i", String(rowIndex || 0));

                const shadow = document.createElement("div");
                shadow.className = "tray-correspondence-card__shadow";
                shadow.setAttribute("aria-hidden", "true");

                const layer = document.createElement("div");
                layer.className = "tray-correspondence-card__layer";
                layer.setAttribute("aria-hidden", "true");

                const body = document.createElement("div");
                body.className = "tray-correspondence-card__body";

                const visual = document.createElement("div");
                visual.className = "tray-correspondence-card__visual";
                if (line.imageSrc) {
                    const img = document.createElement("img");
                    img.alt = "";
                    img.decoding = "async";
                    img.loading = "lazy";
                    img.src = line.imageSrc;
                    visual.appendChild(img);
                } else {
                    visual.textContent = String(line.name || "A").trim().charAt(0).toUpperCase() || "A";
                }

                const letter = document.createElement("div");
                letter.className = "tray-correspondence-card__letter";
                letter.innerHTML = `<p class="tray-correspondence-card__name">${escapeHtml(line.name)}</p>
                    <p class="tray-correspondence-card__note">${escapeHtml(descriptorForLine(line))}</p>`;

                const actions = document.createElement("div");
                actions.className = "tray-correspondence-card__actions";

                const qtyRing = document.createElement("div");
                qtyRing.className = "tray-correspondence-card__qty-ring";

                const minus = document.createElement("button");
                minus.type = "button";
                minus.className = "order-tray-qty guest-hit";
                minus.textContent = "\u2212";
                minus.addEventListener("click", () => setQty(line.id, -1, line.guestLabel));

                const qtyEl = document.createElement("span");
                qtyEl.className = "tray-correspondence-card__qty";
                qtyEl.textContent = String(line.qty);

                const plus = document.createElement("button");
                plus.type = "button";
                plus.className = "order-tray-qty guest-hit";
                plus.textContent = "+";
                plus.addEventListener("click", () => setQty(line.id, 1, line.guestLabel));

                qtyRing.appendChild(minus);
                qtyRing.appendChild(qtyEl);
                qtyRing.appendChild(plus);

                const lineTotal = document.createElement("span");
                lineTotal.className = "tray-correspondence-card__price";
                lineTotal.textContent = money.format(line.qty * line.unitPrice);

                const removeBtn = document.createElement("button");
                removeBtn.type = "button";
                removeBtn.className = "tray-correspondence-card__release order-tray-remove";
                removeBtn.setAttribute("aria-label", (tOrder("menuTray.remove") || "Remove") + " " + line.name);
                removeBtn.textContent = "×";
                removeBtn.addEventListener("click", () => removeLine(line.id, line.guestLabel));

                actions.appendChild(qtyRing);
                actions.appendChild(lineTotal);
                actions.appendChild(removeBtn);

                body.appendChild(visual);
                body.appendChild(letter);
                body.appendChild(actions);
                card.appendChild(shadow);
                card.appendChild(layer);
                card.appendChild(body);
                el.appendChild(card);
            }

            el.className = "order-tray-lines order-tray-lines--correspondence pb-1 text-sm";
            el.innerHTML = "";
            let rowIndex = 0;

            if (useGrouped) {
                const sections = GuestInteractionContract.buildGroupOrderTraySections(allLines, guestCount);
                for (let si = 0; si < sections.length; si++) {
                    const sec = sections[si];
                    const head = document.createElement("p");
                    head.className =
                        "mb-1 mt-4 first:mt-0 text-[10px] font-medium tracking-[0.22em] uppercase text-[rgb(var(--accent))]/90";
                    head.textContent = sec.label;
                    el.appendChild(head);
                    if (!sec.lines || sec.lines.length === 0) {
                        const emptyP = document.createElement("p");
                        emptyP.className =
                            "rounded-2xl bg-[rgba(253,249,244,0.55)] px-4 py-3 text-sm text-[rgb(var(--muted))] ring-1 ring-[rgba(212,184,150,0.4)] mb-2";
                        emptyP.textContent =
                            tOrder("menuTray.emptyGuestSection") || "No cups for this guest yet.";
                        el.appendChild(emptyP);
                        continue;
                    }
                    for (const line of sec.lines) appendMenuLineRow(line, rowIndex++);
                }
                return;
            }

            const visible =
                hasGic && typeof GuestInteractionContract.filterCartLinesForActiveGuest === "function"
                    ? GuestInteractionContract.filterCartLinesForActiveGuest(allLines)
                    : allLines.slice();

            if (visible.length === 0 && allLines.length > 0) {
                const anyGuestLabel = allLines.some((line) => line.guestLabel && String(line.guestLabel).trim());
                if (!anyGuestLabel) {
                    for (const line of allLines) appendMenuLineRow(line, rowIndex++);
                    return;
                }
                const msg =
                    tOrder("menuTray.emptyGuest") || "This guest has no cups on the tray yet — tap a drink to add one.";
                el.innerHTML = `<p class="rounded-2xl bg-white/[0.03] px-4 py-8 text-center text-sm text-[rgb(var(--muted))] ring-1 ring-white/[0.06]">${escapeHtml(msg)}</p>`;
                return;
            }

            for (const line of visible) appendMenuLineRow(line, rowIndex++);
        }

        function escapeHtml(s) {
            return String(s)
                .replace(/&/g, "&amp;")
                .replace(/</g, "&lt;")
                .replace(/>/g, "&gt;")
                .replace(/"/g, "&quot;");
        }

        async function submitOrder() {
            syncCartKey();
            const orderResult = document.getElementById("orderResult");
            const btn = document.getElementById("menuSubmitBtn");
            if (orderResult) orderResult.textContent = "";

            if (!VENUE_TABLE_ID) {
                if (orderResult)
                    orderResult.textContent =
                        tOrder("order.needTableScan") || tOrder("order.needTable") || "Please scan the table QR to begin.";
                return;
            }
            if (cartItems.size === 0) {
                if (orderResult) orderResult.textContent = tOrder("order.minOne") || "Your tray is empty.";
                return;
            }
            if (menuOrderSubmitInFlight) return;
            menuOrderSubmitInFlight = true;
            try {
                if (!menuOrderIdempotencyKey && window.crypto && typeof window.crypto.randomUUID === "function")
                    menuOrderIdempotencyKey = window.crypto.randomUUID();
                else if (!menuOrderIdempotencyKey) menuOrderIdempotencyKey = String(Date.now());
                if (!menuOrderIdempotencyKey) {
                    if (orderResult)
                        orderResult.textContent =
                            tOrder("order.submitFailed") || "We could not prepare your tray receipt. Refresh and try again.";
                    return;
                }

                if (btn) btn.setAttribute("disabled", "disabled");
                const prepLabel = tOrder("menuTray.requestPreparation") || "Send to the 106/1 bar";
                if (btn) {
                    btn.textContent = tOrder("order.submitting") || "Sending to the 106/1 bar…";
                    btn.classList.remove("order-tray-submit--success", "order-tray-submit--pulse");
                    btn.classList.add("order-tray-submit--preparing");
                }

                const items = Array.from(cartItems.values()).map((l) => {
                    const g = (l.guestLabel && String(l.guestLabel).trim()) || "";
                    return {
                        menuItemId: l.id,
                        quantity: l.qty,
                        notes: g ? `Guest: ${g}` : null
                    };
                });

                let res;
                try {
                    res = await fetch(typeof window.__annapApiUrl === "function" ? window.__annapApiUrl("/api/orders") : "/api/orders", {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                            Accept: "application/json",
                            "Idempotency-Key": menuOrderIdempotencyKey
                        },
                        body: JSON.stringify({ venueTableId: VENUE_TABLE_ID, items, idempotencyKey: menuOrderIdempotencyKey })
                    });
                } catch (_net) {
                    if (window.GuestOrderQueue) window.GuestOrderQueue.enqueue(VENUE_TABLE_ID, items, menuOrderIdempotencyKey);
                    if (orderResult)
                        orderResult.textContent =
                            tOrder("order.queuedSoft") ||
                            "The line softened for a moment — we will place this tray when the room is steady again.";
                    if (btn) {
                        btn.textContent = prepLabel;
                        btn.classList.remove("order-tray-submit--preparing");
                        btn.removeAttribute("disabled");
                        refreshTableIdentityUi();
                    }
                    return;
                }

                const payload = await res.json().catch(function () {
                    return null;
                });
                if (!res.ok) {
                    const er = String((payload && payload.error) || "").toLowerCase();
                    let msg = tOrder("order.submitFailed") || "We could not send the tray just now.";
                    if (res.status === 400) {
                        menuOrderIdempotencyKey = null;
                        if (er.includes("scan") || er.includes("table qr") || er.includes("seat"))
                            msg = tOrder("order.needTableScan") || msg;
                        else if (er.includes("table") && (er.includes("not available") || er.includes("inactive")))
                            msg = tOrder("order.tableUnavailable") || msg;
                        else if (er.includes("menu") || er.includes("refresh"))
                            msg = tOrder("order.menuChanged") || msg;
                        else if (er.includes("at least one") || er.includes("tray"))
                            msg = tOrder("order.minOne") || msg;
                    } else if (res.status >= 500 || res.status === 429) {
                        if (window.GuestOrderQueue) window.GuestOrderQueue.enqueue(VENUE_TABLE_ID, items, menuOrderIdempotencyKey);
                        msg =
                            tOrder("order.queuedSoft") ||
                            "The line softened for a moment — we will place this tray when the room is steady again.";
                    }
                    if (orderResult) orderResult.textContent = msg;
                    if (btn) {
                        btn.textContent = prepLabel;
                        btn.classList.remove("order-tray-submit--preparing");
                        btn.removeAttribute("disabled");
                        refreshTableIdentityUi();
                    }
                    return;
                }

                menuOrderIdempotencyKey = null;

                GuestInteractionContract.writeGuestOrderSession({
                    orderId: payload.id,
                    token: payload.guestSessionToken
                });

                if (orderResult) {
                    const follow = escapeHtml(tOrder("order.followLink") || "Track your order");
                    const receiptTime = new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
                    const tableLine = (document.getElementById("menu-table-primary") || {}).textContent || "Annap table";
                    const trackHref = String(
                        payload.trackUrl ||
                            `/track/${payload.id}?token=${encodeURIComponent(payload.guestSessionToken || "")}`
                    ).replace(/"/g, "&quot;");
                    orderResult.innerHTML = `<div class="order-tray-confirm-card" role="status">
                        <div class="order-tray-confirm-card__seal" aria-hidden="true">A</div>
                        <p class="order-tray-confirm-card__kicker">${escapeHtml(tOrder("order.confirmKicker") || "106/1 receipt")}</p>
                        <p class="order-tray-confirm order-tray-confirm-card__title">${escapeHtml(
                            tOrder("order.submitted") || "The Annap bar has received your tray."
                        )}</p>
                        <p class="order-tray-confirm-card__meta">${escapeHtml(tableLine)} · ${escapeHtml(receiptTime)} · 106/1 Nguyễn Thị Minh Khai</p>
                        <p class="order-tray-confirm-card__body">${escapeHtml(tOrder("order.confirmBody") || "Your selections are now with the 106/1 bar. We will prepare them with care.")}</p>
                        <a href="${trackHref}" class="order-tray-confirm-card__link">${follow}</a>
                    </div>`;
                    orderResult.classList.add("orderResult--cinematic");
                }
                document.body.classList.add("annap-order-confirming");
                GuestInteractionContract.clearCart();
                linesToCartMap(GuestInteractionContract.getCartLines());
                renderCart();
                updateTraySummary();
                if (btn) {
                    btn.removeAttribute("disabled");
                    btn.textContent = tOrder("menuTray.receivedThanks") || "Received with thanks";
                    btn.classList.remove("order-tray-submit--preparing");
                    btn.classList.add("order-tray-submit--success", "order-tray-submit--pulse");
                    window.setTimeout(() => {
                        btn.textContent = prepLabel;
                        btn.classList.remove("order-tray-submit--success", "order-tray-submit--pulse");
                        refreshTableIdentityUi();
                    }, 3600);
                }
                window.setTimeout(() => {
                    document.body.classList.remove("annap-order-confirming");
                    if (orderResult) orderResult.classList.remove("orderResult--cinematic");
                    setTrayOpen(false);
                }, 3600);
            } finally {
                menuOrderSubmitInFlight = false;
            }
        }

        function bootMenuTray() {
            if (window._annapMenuTrayBooted) return;
            window._annapMenuTrayBooted = true;
            var trace = __annapMenuBootTraceOn();
            if (trace) {
                try {
                    window.console.groupCollapsed("[ANNAP MENU BOOT]");
                    window.console.log("GuestInteractionContract:", typeof GuestInteractionContract);
                    window.console.log("LuxuryI18n:", typeof window.LuxuryI18n);
                    window.console.log("catalog node:", !!document.getElementById("menu-catalog-json"));
                    window.console.log("guest ctx node:", !!document.getElementById("menu-guest-context"));
                } catch (_g) {}
            }
            __annapTrayLog("[annap] boot menu step 2: tray init");
            try {
                setTrayOpen(false, { immediate: true });
                if (trace) try { window.console.log("step: setTrayOpen(false) ok"); } catch (_s0) {}
            } catch (e0) {
                __annapMenuBootErr("[ANNAP MENU BOOT] setTrayOpen(false) failed", e0);
                __annapTrayErr("[annap] menu: setTrayOpen failed", e0);
            }
            try {
                GuestInteractionContract.setTrayOpener(function () {
                    setTrayOpen(true, { userIntent: true });
                });
                if (trace) try { window.console.log("step: setTrayOpener ok"); } catch (_s1) {}
                __annapTrayLog("[annap] menu: tray opener ok");
            } catch (e1) {
                __annapMenuBootErr("[ANNAP MENU BOOT] tray opener failed", e1);
                __annapTrayErr("[annap] menu: tray opener failed", e1);
            }
            try {
                GuestInteractionContract.navigation.applyVtNavPatches();
                if (trace) try { window.console.log("step: nav patches ok"); } catch (_s2) {}
                __annapTrayLog("[annap] menu: nav patches ok");
            } catch (e2) {
                __annapMenuBootErr("[ANNAP MENU BOOT] nav patches failed", e2);
                __annapTrayErr("[annap] menu: nav patches failed", e2);
            }
            try {
                syncCartKey();
                if (trace) try { window.console.log("step: syncCartKey ok"); } catch (_s3) {}
                refreshTableIdentityUi();
                loadCart();
                renderCart();
                updateTraySummary();
                document.body.classList.add("annap-has-order-tray-dock");
                if (trace) try { window.console.log("step: cart render + tray summary ok"); } catch (_s4) {}
                __annapTrayLog("[annap] menu: cart/tray ok");
            } catch (e3) {
                __annapMenuBootErr("[ANNAP MENU BOOT] cart/tray init failed (first fatal area for tray UI)", e3);
                __annapTrayErr("[annap] menu: cart/tray failed", e3);
            }
            try {
                annapBindMenuCatalogInteractions();
                annapBindDrinkDetailModal();
                if (trace) try { window.console.log("step: catalog interactions + modal chrome bound"); } catch (_s5) {}
                __annapTrayLog("[annap] menu: catalog interactions bound");
            } catch (e4) {
                __annapMenuBootErr("[ANNAP MENU BOOT] catalog bind failed", e4);
                __annapTrayErr("[annap] menu: catalog bind failed", e4);
            }
            if (trace) {
                try {
                    window.console.log("bootMenuTray: done");
                    window.console.groupEnd();
                } catch (_ge) {}
            }
            __annapTrayLog("[annap] boot menu step 3: tray ready");
            if (window.AnnapGuestBootHarness) {
                AnnapGuestBootHarness.row("hydration", "ok");
                if (typeof AnnapGuestBootHarness.markHydrationDone === "function") AnnapGuestBootHarness.markHydrationDone();
            }
        }

        function annapStartMenuGuestBoot() {
            __annapMenuRuntimeJsonSelfCheck();
            if (annapBootOff("disableI18n")) {
                __annapTrayLog("[annap] menu: i18n skipped (diag/safe)");
                if (window.AnnapGuestBootHarness) AnnapGuestBootHarness.markSkipped("i18n");
                bootMenuTray();
                return;
            }
            var H = window.AnnapGuestBootHarness;
            __annapTrayLog("[annap] menu: starting i18n");
            var p = window.LuxuryI18n && window.LuxuryI18n.ready ? window.LuxuryI18n.ready : Promise.resolve();
            var gate =
                H && typeof H.runTimed === "function"
                    ? H.runTimed("i18n", 5000, function () {
                          return p;
                      })
                    : p;
            gate.then(
                function () {
                    __annapTrayLog("[annap] menu: i18n ok");
                    bootMenuTray();
                },
                function (e) {
                    __annapTrayErr("[annap] menu: i18n timeout/fail", e);
                    bootMenuTray();
                }
            );
        }

        document.addEventListener("luxury:i18n-changed", function () {
            try {
                refreshTableIdentityUi();
                updateTraySummary();
                renderCart();
                const btn = document.getElementById("menuSubmitBtn");
                if (btn && !btn.classList.contains("order-tray-submit--success"))
                    btn.textContent = tOrder("menuTray.requestPreparation") || "Request preparation";
            } catch (eI18n) {
                __annapMenuBootErr("[ANNAP MENU BOOT] luxury:i18n-changed handler failed", eI18n);
            }
        });

        document.addEventListener("annap:guest-interaction", function (ev) {
            var d = ev && ev.detail;
            if (!d || (d.type !== "cartUpdated" && d.type !== "activeGuestChanged")) return;
            try {
                linesToCartMap(GuestInteractionContract.getCartLines());
                renderCart();
                updateTraySummary();
            } catch (_m) {
                /* ignore */
            }
        });

        document.addEventListener("annap:tray-chip-settle", function () {
            try {
                updateTraySummary({ animateChip: true });
            } catch (_settle) {
                /* ignore */
            }
        });

        window.__annapRefreshTraySummary = function () {
            updateTraySummary();
        };

window.__annapMenuSubmitOrder = submitOrder;
window.addToCart = addToCart;
window.openDrinkDetail = openDrinkDetail;
window.closeDrinkDetail = closeDrinkDetail;
window.annapStartOrderTrayDock = annapStartMenuGuestBoot;

/* ── Global overlay interceptor ─────────────────────────────────────────────
   Catches any <a href="/menu/drink/{guid}"> click anywhere on the page and
   converts it to an in-page overlay open instead of full navigation.
   Only fires when #drink-detail-modal is present (seated page context).
   This is the universal safety net so every code path uses the overlay. */
(function () {
    var _re = /\/menu\/drink\/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/i;
    document.addEventListener('click', function (ev) {
        if (!ev || ev.defaultPrevented) return;
        var t = ev.target;
        var a = t && t.closest ? t.closest('a[href]') : null;
        if (!a) return;
        if (!document.getElementById('drink-detail-modal')) return;
        var href = a.getAttribute('href') || '';
        var m = href.match(_re);
        if (!m) return;
        /* Always prevent default so correspondence.js page-transition handler
           sees e.defaultPrevented = true and skips its 290ms navigation timer. */
        ev.preventDefault();
        /* If the anchor also carries data-ge-open-detail, guest-experience.js
           will handle the actual openDrinkDetail call — don't double-invoke. */
        if (!a.hasAttribute('data-ge-open-detail') && typeof window.openDrinkDetail === 'function') {
            void window.openDrinkDetail(m[1]);
        }
    }, true);
}());
