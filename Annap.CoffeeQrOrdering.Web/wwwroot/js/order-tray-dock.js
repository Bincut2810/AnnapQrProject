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
const TRAY_STATE = {
    EMPTY: "empty",
    EDITING: "editing",
    REVIEW: "review",
    PAYMENT_CHOICE: "paymentChoice",
    SUBMITTING: "submitting",
    SUBMITTED_COUNTER: "submittedCounterPayment",
    SUBMITTED_CASH: "submittedCashPayment",
    SUBMITTED_CARD: "submittedCardPayment",
    SUBMITTED_BANK: "submittedBankTransfer",
    SUBMITTED_PENDING: "submittedPendingPayment",
    PAID: "paid",
    COMPLETED: "completed"
};
const PAYMENT_METHOD = {
    CASH: "Cash",
    CARD: "Card",
    BANK: "BankTransfer",
    LEGACY_CASH_CARD: "CashOrCardAtCounter"
};
const TRAY_PAYMENT_FLOW_VERSION = "v5-guest-status-poll";
let checkoutStep = null;
let selectedPaymentMethod = PAYMENT_METHOD.CASH;
const expandedItemNoteKeys = new Set();
let bankTransferConfigured = null;
let traySubmittedStatus = null;
let trayStatusPollTimer = null;
let trayKnownPendingPayment = null;
const celebratedPaidOrderIds = new Set();
const TRAY_STATUS_POLL_MS = 3500;
let bankTransferQrInflightKey = "";
let bankTransferQrCache = null;
let bankTransferQrCacheKey = "";
window.__annapBankTransferDebug = window.__annapBankTransferDebug || {};
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
function __annapTrayPaymentDevLog(label, detail) {
    if (!__annapTrayDevOn()) return;
    try {
        if (detail !== undefined) window.console.log("[annap-tray-payment:" + TRAY_PAYMENT_FLOW_VERSION + "]", label, detail);
        else window.console.log("[annap-tray-payment:" + TRAY_PAYMENT_FLOW_VERSION + "]", label);
    } catch (_e) {}
}

function __annapBankTransferDebugPatch(patch) {
    if (!patch || typeof patch !== "object") return;
    const current = window.__annapBankTransferDebug || {};
    const next = { ...current, ...patch, at: new Date().toISOString() };
    window.__annapBankTransferDebug = next;
    if (__annapTrayDevOn()) __annapTrayPaymentDevLog("bank-transfer-debug", next);
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

        function formatTableMetaLine() {
            const lab = String((GUEST_CTX && GUEST_CTX.label) || "").trim();
            if (!VENUE_TABLE_ID && !lab) return "";
            const tableLabel = lab || trayCopyFallback("cart.linkedTable", "Bàn của bạn", "Your table");
            return (
                tfmt("cart.tableMetaShort", { label: tableLabel }) ||
                trayCopyFallback(
                    "cart.tableMetaShort",
                    "Bàn " + tableLabel + " · 106/1 Nguyễn Thị Minh Khai",
                    "Table " + tableLabel + " · 106/1 Nguyễn Thị Minh Khai"
                )
            );
        }

        function refreshTableIdentityUi() {
            syncCartKey();
            const idBlock = document.getElementById("menu-table-identity");
            const needQr = document.getElementById("menu-table-need-qr");
            const primary = document.getElementById("menu-table-primary");
            const tableMeta = document.getElementById("trayTableMeta");
            const btn = document.getElementById("menuSubmitBtn");
            const has = !!VENUE_TABLE_ID;
            if (idBlock) idBlock.classList.add("hidden");
            if (needQr) needQr.classList.toggle("hidden", has);
            if (tableMeta) {
                const meta = has ? formatTableMetaLine() : "";
                tableMeta.textContent = meta;
                tableMeta.classList.toggle("hidden", !meta);
            }
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
            const menuId =
                l.menuItemId != null && l.menuItemId !== ""
                    ? l.menuItemId
                    : l.id != null && l.id !== ""
                      ? l.id
                      : "";
            return String(menuId) + "\u0001" + gl;
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
                    imageSrc: l.imageSrc || "",
                    customerNote: l.customerNote && String(l.customerNote).trim() ? String(l.customerNote).trim() : ""
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

        function trayCopyFallback(key, vi, en) {
            const t = tOrder(key);
            if (t) return t;
            const isVi =
                (window.LuxuryI18n && window.LuxuryI18n.getLang && window.LuxuryI18n.getLang() === "vi") ||
                (document.documentElement.lang || "").toLowerCase().startsWith("vi");
            return isVi ? vi : en;
        }

        function readSubmittedSession() {
            if (typeof GuestInteractionContract === "undefined") return null;
            return GuestInteractionContract.readGuestOrderSession();
        }

        function buildTrackHref(sess) {
            if (!sess) return "";
            if (typeof GuestInteractionContract.buildGuestTrackUrl === "function") {
                return GuestInteractionContract.buildGuestTrackUrl(sess.orderId, sess.token);
            }
            return `/track/${encodeURIComponent(sess.orderId)}?token=${encodeURIComponent(sess.token)}`;
        }

        function buildGuestStatusApiPath(sess) {
            if (!sess) return "";
            if (typeof GuestInteractionContract.buildGuestTrackApiUrl === "function") {
                return GuestInteractionContract.buildGuestTrackApiUrl(sess.orderId, sess.token);
            }
            return `/api/track/orders/${encodeURIComponent(sess.orderId)}?token=${encodeURIComponent(sess.token)}`;
        }

        function fetchGuestOrderStatus(sess) {
            const path = buildGuestStatusApiPath(sess);
            if (!path) return Promise.reject(new Error("missing session"));
            const url = typeof window.__annapApiUrl === "function" ? window.__annapApiUrl(path) : path;
            return fetch(url, {
                headers: { Accept: "application/json" },
                cache: "no-store"
            });
        }

        function resolveSubmittedCounterState(pm) {
            const method = String(pm || "").trim();
            if (method === PAYMENT_METHOD.CARD) return TRAY_STATE.SUBMITTED_CARD;
            if (method === PAYMENT_METHOD.CASH || method === PAYMENT_METHOD.LEGACY_CASH_CARD) return TRAY_STATE.SUBMITTED_CASH;
            if (method === PAYMENT_METHOD.BANK) return TRAY_STATE.SUBMITTED_BANK;
            return TRAY_STATE.SUBMITTED_COUNTER;
        }

        function resolveTrayState() {
            const n = totalQty();
            const sess = readSubmittedSession();
            if (menuOrderSubmitInFlight) {
                if (sess && sess.orderId && sess.token && n === 0) {
                    const st = traySubmittedStatus || sess.status || TRAY_STATE.SUBMITTED_PENDING;
                    if (st === TRAY_STATE.COMPLETED || st === "completed") return TRAY_STATE.COMPLETED;
                    if (st === TRAY_STATE.PAID || st === "paid") return TRAY_STATE.PAID;
                    const pm = String(sess.paymentMethod || "").trim();
                    if (pm === PAYMENT_METHOD.BANK || st === TRAY_STATE.SUBMITTED_BANK) return TRAY_STATE.SUBMITTED_BANK;
                    if (st === TRAY_STATE.SUBMITTED_CARD || pm === PAYMENT_METHOD.CARD) return TRAY_STATE.SUBMITTED_CARD;
                    if (
                        st === TRAY_STATE.SUBMITTED_CASH ||
                        st === TRAY_STATE.SUBMITTED_COUNTER ||
                        pm === PAYMENT_METHOD.CASH ||
                        pm === PAYMENT_METHOD.LEGACY_CASH_CARD
                    )
                        return resolveSubmittedCounterState(pm);
                    return TRAY_STATE.SUBMITTED_PENDING;
                }
                return TRAY_STATE.SUBMITTING;
            }
            if (checkoutStep === "review") return TRAY_STATE.REVIEW;
            if (checkoutStep === "payment") return TRAY_STATE.PAYMENT_CHOICE;
            if (n > 0) return TRAY_STATE.EDITING;
            if (sess && sess.orderId && sess.token) {
                const st = traySubmittedStatus || sess.status || TRAY_STATE.SUBMITTED_PENDING;
                if (st === TRAY_STATE.COMPLETED || st === "completed") return TRAY_STATE.COMPLETED;
                if (st === TRAY_STATE.PAID || st === "paid") return TRAY_STATE.PAID;
                const pm = String(sess.paymentMethod || "").trim();
                if (pm === PAYMENT_METHOD.BANK || st === TRAY_STATE.SUBMITTED_BANK) return TRAY_STATE.SUBMITTED_BANK;
                if (st === TRAY_STATE.SUBMITTED_CARD || pm === PAYMENT_METHOD.CARD) return TRAY_STATE.SUBMITTED_CARD;
                if (
                    st === TRAY_STATE.SUBMITTED_CASH ||
                    st === TRAY_STATE.SUBMITTED_COUNTER ||
                    pm === PAYMENT_METHOD.CASH ||
                    pm === PAYMENT_METHOD.LEGACY_CASH_CARD
                )
                    return resolveSubmittedCounterState(pm);
                return TRAY_STATE.SUBMITTED_PENDING;
            }
            return TRAY_STATE.EMPTY;
        }

        function isSubmittedTrayState(state) {
            return (
                state === TRAY_STATE.SUBMITTED_PENDING ||
                state === TRAY_STATE.SUBMITTED_COUNTER ||
                state === TRAY_STATE.SUBMITTED_CASH ||
                state === TRAY_STATE.SUBMITTED_CARD ||
                state === TRAY_STATE.SUBMITTED_BANK ||
                state === TRAY_STATE.PAID ||
                state === TRAY_STATE.COMPLETED
            );
        }

        function restoreSubmitButtonState(btn, prepLabel) {
            if (!btn) return;
            btn.classList.remove("order-tray-submit--preparing", "order-tray-submit--success", "order-tray-submit--pulse");
            btn.removeAttribute("disabled");
            if (prepLabel) btn.textContent = prepLabel;
        }

        function renderInlineTransferFallback(host, qr, options) {
            options = options || {};
            const q = qr || {};
            const amount = q.amountFormatted || q.amount || "—";
            const memo = q.memo || "—";
            const bankName = q.bankName || "ACB";
            const account = q.accountNumber || "7385268";
            const holder = q.accountName || "HO KINH DOANH ANNAP";
            const qrImage = q.qrImageUrl ? String(q.qrImageUrl) : "";
            const warningLine = options.showWarning
                ? `<p class="order-tray-submitted-card__note">${escapeHtml(
                      trayCopyFallback(
                          "checkout.transferQrLoadFailed",
                          "Không tải được mã QR. Bạn vẫn có thể chuyển khoản bằng thông tin bên dưới.",
                          "Could not load the QR code. You can still transfer using the details below."
                      )
                  )}</p>`
                : "";
            host.innerHTML = `<div class="guest-bank-transfer guest-bank-transfer--tray" role="status">
                ${
                    qrImage
                        ? `<div class="guest-bank-transfer__qr-wrap">
                    <img class="guest-bank-transfer__qr" src="${escapeHtml(qrImage)}" alt="${escapeHtml(
                              trayCopyFallback("checkout.transferQrAlt", "Mã QR chuyển khoản Annap", "Annap transfer QR")
                          )}" loading="eager" decoding="async" referrerpolicy="no-referrer" />
                    <p class="guest-bank-transfer__qr-fallback hidden" role="status">${escapeHtml(
                        trayCopyFallback(
                            "checkout.transferQrLoadFailed",
                            "Không tải được mã QR. Bạn vẫn có thể chuyển khoản bằng thông tin bên dưới.",
                            "Could not load the QR code. You can still transfer using the details below."
                        )
                    )}</p>
                </div>`
                        : ""
                }
                <p class="guest-bank-transfer__amount">${escapeHtml(String(amount))}</p>
                <div class="guest-bank-transfer__keep-open" role="status">
                    <p class="guest-bank-transfer__keep-open-title">${escapeHtml(
                        trayCopyFallback(
                            "checkout.bankTransferKeepOpen",
                            "Vui lòng giữ nguyên màn hình chuyển khoản để nhân viên ra kiểm tra và xác nhận thanh toán.",
                            "Please keep the bank transfer screen open so staff can come verify and confirm payment."
                        )
                    )}</p>
                    <p class="guest-bank-transfer__keep-open-sub">${escapeHtml(
                        trayCopyFallback(
                            "checkout.bankTransferStaffConfirmNote",
                            "Đơn chỉ chuyển sang Đã thanh toán sau khi nhân viên xác nhận.",
                            "Your order is marked paid only after staff confirm payment."
                        )
                    )}</p>
                </div>
                <dl class="guest-bank-transfer__meta">
                    <div><dt>${escapeHtml(trayCopyFallback("checkout.transferMemo", "Nội dung", "Transfer memo"))}</dt><dd class="guest-bank-transfer__memo">${escapeHtml(
                        String(memo)
                    )}</dd></div>
                    <div><dt>${escapeHtml(trayCopyFallback("checkout.transferBank", "Ngân hàng", "Bank"))}</dt><dd>${escapeHtml(
                        String(bankName)
                    )}</dd></div>
                    <div><dt>${escapeHtml(trayCopyFallback("checkout.transferAccountNumber", "Số tài khoản", "Account number"))}</dt><dd>${escapeHtml(
                        String(account)
                    )}</dd></div>
                    <div><dt>${escapeHtml(trayCopyFallback("checkout.transferAccountHolder", "Chủ tài khoản", "Account holder"))}</dt><dd>${escapeHtml(
                        String(holder)
                    )}</dd></div>
                </dl>
                <div class="guest-bank-transfer__actions">
                    <button type="button" class="guest-bank-transfer__copy guest-hit" data-copy-memo="${escapeHtml(String(memo))}">${escapeHtml(
                        trayCopyFallback("checkout.copyMemo", "Sao chép nội dung", "Copy memo")
                    )}</button>
                    <button type="button" class="guest-bank-transfer__copy guest-hit" data-copy-amount="${escapeHtml(
                        String(q.amount || "")
                    )}">${escapeHtml(trayCopyFallback("checkout.copyAmount", "Sao chép số tiền", "Copy amount"))}</button>
                </div>
                ${warningLine}
            </div>`;
            const img = host.querySelector(".guest-bank-transfer__qr");
            const fallback = host.querySelector(".guest-bank-transfer__qr-fallback");
            if (img && fallback) {
                img.addEventListener("load", function () {
                    __annapBankTransferDebugPatch({
                        qrImageLoadState: "loaded",
                        qrImageNaturalWidth: img.naturalWidth || 0,
                        qrImageNaturalHeight: img.naturalHeight || 0
                    });
                });
                img.addEventListener("error", function () {
                    img.classList.add("hidden");
                    fallback.classList.remove("hidden");
                    __annapBankTransferDebugPatch({
                        qrImageLoadState: "error",
                        qrImageNaturalWidth: img.naturalWidth || 0,
                        qrImageNaturalHeight: img.naturalHeight || 0
                    });
                });
            }
            host.querySelectorAll("[data-copy-memo]").forEach(function (btn) {
                btn.addEventListener("click", function () {
                    if (window.GuestBankTransfer && typeof window.GuestBankTransfer.copyText === "function")
                        void window.GuestBankTransfer.copyText(btn.getAttribute("data-copy-memo"), null);
                });
            });
            host.querySelectorAll("[data-copy-amount]").forEach(function (btn) {
                btn.addEventListener("click", function () {
                    if (window.GuestBankTransfer && typeof window.GuestBankTransfer.copyText === "function")
                        void window.GuestBankTransfer.copyText(btn.getAttribute("data-copy-amount"), null);
                });
            });
        }

        function renderTransferFatalFallback(host, onRetry) {
            host.innerHTML = `<div class="guest-bank-transfer guest-bank-transfer--tray" role="alert">
                <p class="guest-bank-transfer__message">${escapeHtml(
                    trayCopyFallback(
                        "checkout.transferUnavailableInTray",
                        "Không tải được thông tin chuyển khoản. Vui lòng gọi nhân viên.",
                        "Could not load transfer details. Please call staff."
                    )
                )}</p>
                <div class="guest-bank-transfer__actions">
                    <button type="button" class="guest-bank-transfer__copy guest-hit" data-bt-retry="1">${escapeHtml(
                        trayCopyFallback("checkout.transferRetry", "Tải lại mã chuyển khoản", "Retry transfer QR")
                    )}</button>
                </div>
            </div>`;
            const retryBtn = host.querySelector("[data-bt-retry]");
            if (retryBtn && typeof onRetry === "function") {
                retryBtn.addEventListener("click", function (ev) {
                    ev.preventDefault();
                    onRetry();
                });
            }
        }

        function ensureBankTransferQrMounted(sess) {
            if (!sess || String(sess.paymentMethod || "").trim() !== PAYMENT_METHOD.BANK) return Promise.resolve(false);
            const state = resolveTrayState();
            if (state !== TRAY_STATE.SUBMITTED_BANK) return Promise.resolve(false);
            const host = document.getElementById("order-tray-transfer-host");
            const cacheKey = `${sess.orderId || ""}|${sess.token || ""}`;
            __annapBankTransferDebugPatch({
                appVersion: TRAY_PAYMENT_FLOW_VERSION,
                trayState: state,
                submittedStatus: traySubmittedStatus || sess.status || "",
                paymentMethod: sess.paymentMethod || "",
                orderId: sess.orderId || "",
                hasGuestToken: !!sess.token,
                qrMountFound: !!host
            });
            if (!host || !sess.orderId || !sess.token) return Promise.resolve(false);
            host.innerHTML =
                '<p class="order-tray-submitted-card__note">' +
                escapeHtml(
                    trayCopyFallback("checkout.transferQrLoading", "Đang tải mã chuyển khoản...", "Loading transfer QR...")
                ) +
                "</p>";
            if (__annapTrayDevOn()) {
                host.insertAdjacentHTML(
                    "beforeend",
                    `<p class="order-tray-submitted-card__note">BT debug: state=${escapeHtml(state)} key=${escapeHtml(cacheKey)}</p>`
                );
            }
            if (bankTransferQrCache && bankTransferQrCacheKey === cacheKey) {
                if (window.GuestBankTransfer && typeof window.GuestBankTransfer.mountTransferCard === "function")
                    window.GuestBankTransfer.mountTransferCard(host, bankTransferQrCache, { compact: true });
                else renderInlineTransferFallback(host, bankTransferQrCache);
                __annapBankTransferDebugPatch({ qrRenderCalled: true, qrCardInserted: true, qrFetchStatus: "cache" });
                return Promise.resolve(true);
            }
            if (bankTransferQrInflightKey === cacheKey) return Promise.resolve(true);
            bankTransferQrInflightKey = cacheKey;
            __annapBankTransferDebugPatch({ qrFetchStarted: true });
            const fetchQr =
                window.GuestBankTransfer && typeof window.GuestBankTransfer.fetchTransferQr === "function"
                    ? window.GuestBankTransfer.fetchTransferQr(sess.orderId, sess.token)
                    : Promise.resolve(null);
            return fetchQr
                .then(function (qr) {
                    __annapBankTransferDebugPatch({
                        qrFetchStatus: "200",
                        qrFetchBodyHasQrImageUrl: !!(qr && qr.qrImageUrl)
                    });
                    if (qr) {
                        bankTransferQrCache = qr;
                        bankTransferQrCacheKey = cacheKey;
                    }
                    if (qr && window.GuestBankTransfer && typeof window.GuestBankTransfer.mountTransferCard === "function")
                        window.GuestBankTransfer.mountTransferCard(host, qr, { compact: true });
                    else if (qr) renderInlineTransferFallback(host, qr);
                    else renderTransferFatalFallback(host, function () { void ensureBankTransferQrMounted(sess); });
                    const card = host.querySelector(".guest-bank-transfer");
                    const rect = card ? card.getBoundingClientRect() : null;
                    __annapBankTransferDebugPatch({
                        qrRenderCalled: true,
                        qrCardInserted: !!card,
                        qrCardVisible: !!(rect && rect.height > 0 && rect.width > 0),
                        qrCardRect: rect
                            ? { top: rect.top, left: rect.left, width: rect.width, height: rect.height }
                            : null
                    });
                    return !!card;
                })
                .catch(function (err) {
                    __annapBankTransferDebugPatch({ lastError: String(err && err.message ? err.message : err), qrFetchStatus: "error" });
                    const fallbackPath = `/api/track/orders/${encodeURIComponent(sess.orderId)}?token=${encodeURIComponent(sess.token)}`;
                    const fallbackUrl = typeof window.__annapApiUrl === "function" ? window.__annapApiUrl(fallbackPath) : fallbackPath;
                    return fetch(fallbackUrl, { headers: { Accept: "application/json" }, cache: "no-store" })
                        .then(function (res) {
                            if (!res.ok) throw new Error("fallback track fetch failed");
                            return res.json();
                        })
                        .then(function (track) {
                            const qr = track && track.transferQr ? track.transferQr : null;
                            if (qr) {
                                bankTransferQrCache = qr;
                                bankTransferQrCacheKey = cacheKey;
                            }
                            if (qr && window.GuestBankTransfer && typeof window.GuestBankTransfer.mountTransferCard === "function")
                                window.GuestBankTransfer.mountTransferCard(host, qr, { compact: true });
                            else if (qr) renderInlineTransferFallback(host, qr, { showWarning: true });
                            else renderTransferFatalFallback(host, function () { void ensureBankTransferQrMounted(sess); });
                            return !!host.querySelector(".guest-bank-transfer");
                        })
                        .catch(function () {
                            renderTransferFatalFallback(host, function () { void ensureBankTransferQrMounted(sess); });
                            return false;
                        });
                })
                .finally(function () {
                    bankTransferQrInflightKey = "";
                });
        }

        function applySubmitSuccessUi(payload, paymentMethod, btn, prepLabel) {
            const submittedStatus =
                paymentMethod === PAYMENT_METHOD.BANK
                    ? TRAY_STATE.SUBMITTED_BANK
                    : paymentMethod === PAYMENT_METHOD.CARD
                      ? TRAY_STATE.SUBMITTED_CARD
                      : TRAY_STATE.SUBMITTED_CASH;

            GuestInteractionContract.writeGuestOrderSession({
                orderId: payload.id,
                token: payload.guestSessionToken,
                venueTableId: VENUE_TABLE_ID,
                submittedAt: new Date().toISOString(),
                status: submittedStatus,
                paymentMethod: paymentMethod
            });
            expandedItemNoteKeys.clear();
            traySubmittedStatus = submittedStatus;
            trayKnownPendingPayment = true;
            menuOrderSubmitInFlight = false;
            resetCheckoutStep();

            GuestInteractionContract.clearCart();
            linesToCartMap(GuestInteractionContract.getCartLines());
            renderCart();
            updateCheckoutUi();
            updateTraySummary();
            setTrayOpen(true, { userIntent: true });
            startTrayStatusPolling();

            if (btn) {
                btn.classList.remove("order-tray-submit--preparing");
                btn.setAttribute("disabled", "disabled");
            }

            __annapTrayPaymentDevLog("submit success ui", {
                paymentMethod: paymentMethod,
                renderState: submittedStatus,
                orderId: payload.id ? "present" : "missing",
                token: payload.guestSessionToken ? "present" : "missing",
                trackUrl: payload.trackUrl ? "present" : "missing"
            });

            document.body.classList.add("annap-order-confirming");
            window.setTimeout(function () {
                document.body.classList.remove("annap-order-confirming");
            }, 1200);
        }

        function resetCheckoutStep() {
            checkoutStep = null;
            selectedPaymentMethod = PAYMENT_METHOD.CASH;
        }

        function setCheckoutStep(step) {
            checkoutStep = step === "review" || step === "payment" ? step : null;
            if (checkoutStep === "review") selectedPaymentMethod = PAYMENT_METHOD.CASH;
            if (checkoutStep === "payment" && typeof refreshBankTransferAvailability === "function") {
                refreshBankTransferAvailability();
            }
            updateCheckoutUi();
            updateTraySummary();
            if (checkoutStep === "payment") {
                window.requestAnimationFrame(function () {
                    scrollCheckoutCtaIntoView();
                });
            }
        }

        async function refreshBankTransferAvailability() {
            if (!window.GuestBankTransfer || typeof window.GuestBankTransfer.fetchAvailability !== "function") {
                bankTransferConfigured = null;
                applyBankTransferAvailabilityUi();
                return;
            }
            try {
                const data = await window.GuestBankTransfer.fetchAvailability();
                bankTransferConfigured = !!data.enabled;
            } catch (_e) {
                bankTransferConfigured = false;
            }
            applyBankTransferAvailabilityUi();
        }

        function applyBankTransferAvailabilityUi() {
            const bankBtn = document.querySelector('.order-tray-payment-option[data-payment-method="BankTransfer"]');
            if (!bankBtn) return;
            const unavailable = bankTransferConfigured === false;
            bankBtn.disabled = unavailable;
            bankBtn.classList.toggle("order-tray-payment-option--disabled", unavailable);
            bankBtn.setAttribute("aria-disabled", unavailable ? "true" : "false");
            let hint = bankBtn.querySelector(".order-tray-payment-option__hint");
            if (unavailable) {
                if (!hint) {
                    hint = document.createElement("span");
                    hint.className = "order-tray-payment-option__hint";
                    bankBtn.appendChild(hint);
                }
                hint.textContent = trayCopyFallback(
                    "checkout.bankTransferUnavailable",
                    "Chuyển khoản hiện chưa khả dụng. Vui lòng thanh toán tại quầy.",
                    "Bank transfer is currently unavailable. Please pay at the counter."
                );
            } else if (hint) {
                hint.remove();
            }
            if (unavailable && selectedPaymentMethod === PAYMENT_METHOD.BANK) {
                selectedPaymentMethod = PAYMENT_METHOD.CASH;
                updatePaymentOptionUi();
            }
        }

        function renderCheckoutReviewLines() {
            const host = document.getElementById("order-tray-checkout-lines");
            const totalEl = document.getElementById("order-tray-checkout-total");
            if (!host) return;
            const lines = Array.from(cartItems.values());
            host.innerHTML = lines
                .map(function (line) {
                    const lineTotal = line.qty * line.unitPrice;
                    return `<li class="order-tray-checkout__line">
                        <span class="order-tray-checkout__line-name">${escapeHtml(line.name)}</span>
                        <span class="order-tray-checkout__line-qty">×${line.qty}</span>
                        <span class="order-tray-checkout__line-total">${escapeHtml(money.format(lineTotal))}</span>
                    </li>`;
                })
                .join("");
            if (totalEl) totalEl.textContent = money.format(subtotal());
        }

        function getPaymentSubmitLabel(method) {
            if (method === PAYMENT_METHOD.BANK) {
                return (
                    tOrder("checkout.submitForQr") ||
                    trayCopyFallback("checkout.submitForQr", "Gửi đơn — lấy mã QR", "Send order — get QR code")
                );
            }
            if (method === PAYMENT_METHOD.CARD) {
                return (
                    tOrder("checkout.submitCard") ||
                    trayCopyFallback("checkout.submitCard", "Gửi đơn — thanh toán bằng thẻ", "Send order — pay by card")
                );
            }
            return (
                tOrder("checkout.submitCash") ||
                trayCopyFallback("checkout.submitCash", "Gửi đơn — thanh toán tiền mặt", "Send order — pay cash")
            );
        }

        function updatePaymentPreviewUi() {
            const preview = document.getElementById("order-tray-payment-preview");
            const titleEl = document.getElementById("order-tray-payment-preview-title");
            const bodyEl = document.getElementById("order-tray-payment-preview-body");
            const noteEl = document.getElementById("order-tray-payment-preview-note");
            const keepOpenEl = document.getElementById("order-tray-payment-preview-keepopen");
            if (!preview || checkoutStep !== "payment" || !selectedPaymentMethod) {
                if (preview) {
                    preview.classList.add("hidden");
                    preview.hidden = true;
                }
                if (keepOpenEl) {
                    keepOpenEl.classList.add("hidden");
                    keepOpenEl.hidden = true;
                }
                return;
            }
            let title;
            let body;
            let note;
            if (selectedPaymentMethod === PAYMENT_METHOD.BANK) {
                title = trayCopyFallback("checkout.bankPreviewTitle", "Chuyển khoản", "Bank transfer");
                body = trayCopyFallback(
                    "checkout.bankPreviewBody",
                    "Sau khi gửi đơn, mã QR chuyển khoản đúng số tiền sẽ hiện tại đây.",
                    "After you send the order, a transfer QR for the exact amount will appear here."
                );
                note = trayCopyFallback(
                    "checkout.bankPreviewNote",
                    "Vui lòng chuyển đúng số tiền và nội dung để nhân viên xác nhận nhanh hơn.",
                    "Please transfer the exact amount and memo so staff can confirm faster."
                );
                if (keepOpenEl) {
                    keepOpenEl.textContent = trayCopyFallback(
                        "checkout.bankPreviewKeepOpen",
                        "Sau khi chuyển khoản, vui lòng giữ nguyên màn hình chuyển khoản để nhân viên ra kiểm tra.",
                        "After transferring, please keep the bank transfer screen open for staff to verify."
                    );
                    keepOpenEl.classList.remove("hidden");
                    keepOpenEl.hidden = false;
                }
            } else if (selectedPaymentMethod === PAYMENT_METHOD.CARD) {
                title = trayCopyFallback("checkout.cardPreviewTitle", "Thanh toán bằng thẻ", "Pay by card");
                body = trayCopyFallback(
                    "checkout.cardPreviewBody",
                    "Sau khi gửi đơn, vui lòng đến quầy để thanh toán bằng thẻ. Nhân viên sẽ kiểm tra lại đơn của bạn.",
                    "After you send the order, please go to the counter to pay by card. Staff will check your order."
                );
                note = trayCopyFallback(
                    "checkout.counterPreviewNote",
                    "Đơn chỉ chuyển sang Đã Thanh Toán sau khi nhân viên xác nhận.",
                    "Your order is marked paid only after staff confirm payment."
                );
                if (keepOpenEl) {
                    keepOpenEl.textContent = "";
                    keepOpenEl.classList.add("hidden");
                    keepOpenEl.hidden = true;
                }
            } else {
                title = trayCopyFallback("checkout.cashPreviewTitle", "Thanh toán tiền mặt", "Pay cash");
                body = trayCopyFallback(
                    "checkout.cashPreviewBody",
                    "Sau khi gửi đơn, vui lòng đến quầy để thanh toán bằng tiền mặt. Nhân viên sẽ kiểm tra lại đơn của bạn.",
                    "After you send the order, please go to the counter to pay cash. Staff will check your order."
                );
                note = trayCopyFallback(
                    "checkout.counterPreviewNote",
                    "Đơn chỉ chuyển sang Đã Thanh Toán sau khi nhân viên xác nhận.",
                    "Your order is marked paid only after staff confirm payment."
                );
                if (keepOpenEl) {
                    keepOpenEl.textContent = "";
                    keepOpenEl.classList.add("hidden");
                    keepOpenEl.hidden = true;
                }
            }
            if (titleEl) titleEl.textContent = title;
            if (bodyEl) bodyEl.textContent = body;
            if (noteEl) noteEl.textContent = note;
            preview.classList.remove("hidden");
            preview.hidden = false;
        }

        function scrollCheckoutCtaIntoView() {
            const sticky = document.getElementById("order-tray-checkout-sticky");
            const btn = document.getElementById("menuSubmitBtn");
            const target = sticky || btn;
            if (!target || typeof target.scrollIntoView !== "function") return;
            try {
                target.scrollIntoView({ block: "nearest", behavior: reducedMotionTray() ? "auto" : "smooth" });
            } catch (_e) {
                target.scrollIntoView(false);
            }
        }

        function updatePaymentOptionUi() {
            document.querySelectorAll(".order-tray-payment-option").forEach(function (btn) {
                const method = btn.getAttribute("data-payment-method");
                const on = method === selectedPaymentMethod;
                btn.classList.toggle("order-tray-payment-option--selected", on);
                btn.setAttribute("aria-pressed", on ? "true" : "false");
            });
            updatePaymentPreviewUi();
            const btn = document.getElementById("menuSubmitBtn");
            if (btn && checkoutStep === "payment" && !menuOrderSubmitInFlight && !isSubmittedTrayState(resolveTrayState())) {
                btn.textContent = getPaymentSubmitLabel(selectedPaymentMethod);
            }
        }

        function updateCheckoutUi() {
            const review = document.getElementById("order-tray-checkout-review");
            const payment = document.getElementById("order-tray-checkout-payment");
            const cartFooter = document.getElementById("order-tray-cart-footer");
            const backRow = document.getElementById("order-tray-checkout-actions");
            const stickyBar = document.getElementById("order-tray-checkout-sticky");
            const clearBtn = document.getElementById("order-tray-clear");
            const btn = document.getElementById("menuSubmitBtn");
            const inCheckout = checkoutStep === "review" || checkoutStep === "payment";
            const inPayment = checkoutStep === "payment";
            const submitted = isSubmittedTrayState(resolveTrayState());

            if (review) {
                const showReview = checkoutStep === "review" || checkoutStep === "payment";
                review.classList.toggle("hidden", !showReview);
                review.hidden = !showReview;
                review.classList.toggle("order-tray-checkout--compact", inPayment);
            }
            if (payment) {
                payment.classList.toggle("hidden", !inPayment);
                payment.hidden = !inPayment;
            }
            if (cartFooter) {
                const hideSubtotal = inCheckout;
                cartFooter.classList.toggle("order-tray-cart-footer--checkout", inCheckout);
                const subtotalRow = cartFooter.querySelector(".flex.items-baseline");
                if (subtotalRow) subtotalRow.classList.toggle("hidden", hideSubtotal);
            }
            if (backRow) {
                backRow.classList.toggle("hidden", !inCheckout);
                backRow.hidden = !inCheckout;
            }
            if (stickyBar) {
                stickyBar.classList.toggle("order-tray-checkout-sticky--pinned", inPayment);
            }
            if (clearBtn) clearBtn.classList.toggle("hidden", inCheckout || submitted);

            const linesWrap = document.getElementById("order-tray-lines-wrap");
            if (linesWrap) {
                const showLines = !inCheckout || submitted;
                linesWrap.classList.toggle("hidden", !showLines);
                linesWrap.hidden = !showLines;
            }
            if (orderTrayRoot) {
                orderTrayRoot.classList.toggle("order-tray-root--checkout", inCheckout);
                orderTrayRoot.classList.toggle("order-tray-root--checkout-payment", inPayment);
            }

            if (checkoutStep === "review") renderCheckoutReviewLines();
            if (checkoutStep === "payment") updatePaymentOptionUi();
            if (inCheckout || submitted) refreshTableIdentityUi();

            if (btn && !submitted && !menuOrderSubmitInFlight) {
                if (checkoutStep === "review") {
                    btn.textContent = tOrder("checkout.continueToPayment") || trayCopyFallback("checkout.continueToPayment", "Chọn phương thức thanh toán", "Choose payment method");
                    btn.removeAttribute("disabled");
                } else if (checkoutStep === "payment") {
                    btn.textContent = getPaymentSubmitLabel(selectedPaymentMethod);
                    btn.removeAttribute("disabled");
                } else if (totalQty() > 0 && VENUE_TABLE_ID) {
                    btn.textContent = tOrder("checkout.reviewOrder") || trayCopyFallback("checkout.reviewOrder", "Kiểm tra đơn", "Review order");
                    btn.removeAttribute("disabled");
                } else {
                    btn.textContent = tOrder("checkout.reviewOrder") || trayCopyFallback("checkout.reviewOrder", "Kiểm tra đơn", "Review order");
                }
            } else if (btn && submitted) {
                btn.setAttribute("disabled", "disabled");
            }
        }

        function handlePrimaryTrayAction() {
            if (menuOrderSubmitInFlight) return;
            if (!VENUE_TABLE_ID) {
                const orderResult = document.getElementById("orderResult");
                if (orderResult)
                    orderResult.textContent =
                        tOrder("order.needTableScan") || tOrder("order.needTable") || "Please scan the table QR to begin.";
                return;
            }
            if (cartItems.size === 0) {
                const orderResult = document.getElementById("orderResult");
                if (orderResult) orderResult.textContent = tOrder("order.minOne") || "Your tray is empty.";
                return;
            }
            if (!checkoutStep) {
                setTrayOpen(true, { userIntent: true });
                setCheckoutStep("review");
                return;
            }
            if (checkoutStep === "review") {
                setCheckoutStep("payment");
                return;
            }
            if (checkoutStep === "payment") {
                void submitOrder();
            }
        }

        function isPendingTrackPayload(data) {
            if (!data || data.isCancelled || data.isComplete) return false;
            if (data.pendingPayment === true) return true;
            const phase = data.phaseKey != null ? String(data.phaseKey) : "";
            return phase === "awaiting_payment" || Number(data.step) === 1;
        }

        function isPaidTrackPayload(data) {
            return isOrderPaidForGuest(data);
        }

        function isOrderPaidForGuest(data) {
            if (!data || data.isCancelled || data.isComplete) return false;
            if (data.pendingPayment === true) return false;
            if (data.paidAtUtc) return true;
            const phase = data.phaseKey != null ? String(data.phaseKey) : "";
            if (phase === "paid_preparing" || phase.startsWith("paid") || phase === "completed") return true;
            if (data.showBill === true && data.pendingPayment === false) return true;
            const statusRaw = data.status != null ? String(data.status) : "";
            if (/^(Paid|InProgress|FinishingTouches|Ready)$/i.test(statusRaw)) return true;
            return false;
        }

        function isSubmittedPendingTrayState(state) {
            return (
                state === TRAY_STATE.SUBMITTED_PENDING ||
                state === TRAY_STATE.SUBMITTED_COUNTER ||
                state === TRAY_STATE.SUBMITTED_CASH ||
                state === TRAY_STATE.SUBMITTED_CARD ||
                state === TRAY_STATE.SUBMITTED_BANK
            );
        }

        function shouldPollSubmittedTrayStatus() {
            if (!readSubmittedSession()) return false;
            return isSubmittedPendingTrayState(resolveTrayState());
        }

        function stopTrayStatusPolling() {
            if (trayStatusPollTimer) {
                window.clearInterval(trayStatusPollTimer);
                trayStatusPollTimer = null;
            }
        }

        function startTrayStatusPolling() {
            if (!shouldPollSubmittedTrayStatus()) return;
            function pollTick() {
                if (!shouldPollSubmittedTrayStatus()) {
                    stopTrayStatusPolling();
                    return;
                }
                refreshSubmittedTrayStatus()
                    .then(function (result) {
                        if (result && result.uiChanged) {
                            updateTraySummary();
                            renderCart();
                        }
                    })
                    .catch(function () {
                        /* ignore */
                    });
            }
            pollTick();
            if (trayStatusPollTimer) return;
            trayStatusPollTimer = window.setInterval(pollTick, TRAY_STATUS_POLL_MS);
        }

        function dismissPaymentSuccessCelebration() {
            const host = document.getElementById("order-tray-payment-celebration");
            if (!host) return;
            host.classList.remove("is-visible");
            window.setTimeout(function () {
                if (host.parentNode) host.parentNode.removeChild(host);
            }, 280);
        }

        function runTrayConfettiBurst() {
            if (reducedMotionTray()) return;
            const wrap = document.createElement("div");
            wrap.className = "order-tray-confetti";
            wrap.setAttribute("aria-hidden", "true");
            const colors = ["#f5c842", "#e8a838", "#6fcf97", "#56ccf2", "#bb6bd9", "#f2994a"];
            for (let i = 0; i < 52; i++) {
                const piece = document.createElement("span");
                piece.className = "order-tray-confetti__piece";
                piece.style.setProperty("--x", String(Math.random()));
                piece.style.setProperty("--delay", String(Math.random() * 0.35) + "s");
                piece.style.setProperty("--rot", String(Math.random() * 360) + "deg");
                piece.style.setProperty("--drift", String((Math.random() - 0.5) * 80) + "px");
                piece.style.background = colors[i % colors.length];
                wrap.appendChild(piece);
            }
            document.body.appendChild(wrap);
            window.setTimeout(function () {
                if (wrap.parentNode) wrap.parentNode.removeChild(wrap);
            }, 2000);
        }

        function showPaymentSuccessCelebration(sess) {
            dismissPaymentSuccessCelebration();
            const host = document.createElement("div");
            host.id = "order-tray-payment-celebration";
            host.className = "order-tray-payment-celebration";
            host.setAttribute("role", "dialog");
            host.setAttribute("aria-modal", "true");
            host.setAttribute("aria-live", "polite");
            const title = trayCopyFallback("menuTray.chipPaidTitle", "Thanh toán thành công", "Payment successful");
            const body = trayCopyFallback(
                "menuTray.paymentSuccessCelebrationBody",
                "Đơn của bạn đã được nhân viên xác nhận. Chúng tôi đang chuẩn bị món.",
                "Staff have confirmed your order. We are preparing your drinks."
            );
            const trackLabel = trayCopyFallback("menuTray.chipTrackCta", "Theo dõi đơn", "Track order");
            const closeLabel = trayCopyFallback("menuTray.paymentSuccessClose", "Đóng", "Close");
            const trackHref = String(buildTrackHref(sess)).replace(/"/g, "&quot;");
            host.innerHTML =
                '<div class="order-tray-payment-celebration__backdrop" data-celebration-act="close"></div>' +
                '<div class="order-tray-payment-celebration__card surface-glass">' +
                '<p class="order-tray-payment-celebration__title">' +
                escapeHtml(title) +
                "</p>" +
                '<p class="order-tray-payment-celebration__body">' +
                escapeHtml(body) +
                "</p>" +
                '<div class="order-tray-payment-celebration__actions">' +
                '<a href="' +
                trackHref +
                '" class="order-tray-payment-celebration__track guest-hit">' +
                escapeHtml(trackLabel) +
                "</a>" +
                '<button type="button" class="order-tray-payment-celebration__close guest-hit" data-celebration-act="close">' +
                escapeHtml(closeLabel) +
                "</button>" +
                "</div></div>";
            host.addEventListener("click", function (ev) {
                const act = ev.target && ev.target.closest ? ev.target.closest("[data-celebration-act]") : null;
                if (!act) return;
                ev.preventDefault();
                dismissPaymentSuccessCelebration();
            });
            document.body.appendChild(host);
            window.requestAnimationFrame(function () {
                host.classList.add("is-visible");
            });
            runTrayConfettiBurst();
        }

        function handlePaymentConfirmedCelebration(sess) {
            setTrayOpen(false);
            updateTraySummary();
            renderCart();
            showPaymentSuccessCelebration(sess);
            __annapTrayPaymentDevLog("payment confirmed celebration", { orderId: sess && sess.orderId });
        }

        async function refreshSubmittedTrayStatus() {
            const sess = readSubmittedSession();
            if (!sess) {
                traySubmittedStatus = null;
                trayKnownPendingPayment = null;
                stopTrayStatusPolling();
                return { uiChanged: false };
            }
            try {
                const res = await fetchGuestOrderStatus(sess);
                if (res.status === 404) {
                    GuestInteractionContract.removeGuestOrderSession();
                    traySubmittedStatus = null;
                    trayKnownPendingPayment = null;
                    stopTrayStatusPolling();
                    return { uiChanged: true };
                }
                if (!res.ok) return { uiChanged: false };
                let data;
                try {
                    data = await res.json();
                } catch (_json) {
                    __annapTrayPaymentDevLog("guest status poll: non-json response", { status: res.status });
                    return { uiChanged: false };
                }
                if (data.isCancelled) {
                    GuestInteractionContract.removeGuestOrderSession();
                    traySubmittedStatus = null;
                    trayKnownPendingPayment = null;
                    stopTrayStatusPolling();
                    return { uiChanged: true };
                }
                const wasPending =
                    trayKnownPendingPayment === true ||
                    (trayKnownPendingPayment !== false &&
                        isSubmittedPendingTrayState(traySubmittedStatus || sess.status || TRAY_STATE.SUBMITTED_PENDING));
                const isPending = isPendingTrackPayload(data);
                const isPaid = isPaidTrackPayload(data);
                if (data.isComplete) {
                    traySubmittedStatus = TRAY_STATE.COMPLETED;
                    GuestInteractionContract.updateGuestOrderSessionStatus(TRAY_STATE.COMPLETED);
                    trayKnownPendingPayment = false;
                    stopTrayStatusPolling();
                } else if (isPaid) {
                    traySubmittedStatus = TRAY_STATE.PAID;
                    GuestInteractionContract.updateGuestOrderSessionStatus(TRAY_STATE.PAID);
                    trayKnownPendingPayment = false;
                    stopTrayStatusPolling();
                } else {
                    const pendingState = resolveSubmittedCounterState(sess.paymentMethod);
                    traySubmittedStatus = pendingState;
                    GuestInteractionContract.updateGuestOrderSessionStatus(pendingState);
                    if (isPending) trayKnownPendingPayment = true;
                }
                let celebrated = false;
                const orderKey = sess.orderId ? String(sess.orderId) : "";
                if (wasPending && isPaid && orderKey && !celebratedPaidOrderIds.has(orderKey)) {
                    celebratedPaidOrderIds.add(orderKey);
                    handlePaymentConfirmedCelebration(sess);
                    celebrated = true;
                }
                if (isPending) startTrayStatusPolling();
                return { uiChanged: true, celebrated: celebrated };
            } catch (_st) {
                return { uiChanged: false };
            }
        }

        function updateTraySheetForState(state) {
            const footer = document.getElementById("order-tray-sheet-footer");
            const clearBtn = document.getElementById("order-tray-clear");
            const submitted = isSubmittedTrayState(state);
            if (footer) footer.classList.toggle("order-tray-sheet-footer--hidden", submitted);
            if (clearBtn) clearBtn.classList.toggle("hidden", submitted);
        }

        function renderSubmittedTraySheet(sess, state) {
            const el = document.getElementById("cart");
            if (!el || !sess) return;
            let title;
            let body;
            let statusLabel;
            let note;
            if (state === TRAY_STATE.COMPLETED) {
                title = trayCopyFallback("menuTray.chipCompleteTitle", "Đơn đã hoàn thành", "Order complete");
                body = trayCopyFallback("menuTray.chipCompleteBody", "Cảm ơn bạn đã ghé Annap.", "Thank you for visiting Annap.");
                statusLabel = "";
                note = "";
            } else if (state === TRAY_STATE.PAID) {
                title = trayCopyFallback("menuTray.chipPaidTitle", "Thanh toán thành công", "Payment successful");
                body = trayCopyFallback(
                    "menuTray.paymentSuccessCelebrationBody",
                    "Đơn của bạn đã được nhân viên xác nhận. Chúng tôi đang chuẩn bị món.",
                    "Staff have confirmed your order. We are preparing your drinks."
                );
                statusLabel = "";
                note = "";
            } else if (state === TRAY_STATE.SUBMITTED_BANK) {
                title = trayCopyFallback("checkout.waitingBankTransfer", "Chờ chuyển khoản", "Waiting for bank transfer");
                body = trayCopyFallback(
                    "checkout.bankTransferPendingBodyShort",
                    "Quét mã QR hoặc chuyển khoản theo thông tin bên dưới.",
                    "Scan the QR or transfer using the details below."
                );
                statusLabel = "";
                note = "";
            } else if (state === TRAY_STATE.SUBMITTED_CARD) {
                title = trayCopyFallback("menuTray.chipSubmittedTitle", "Đơn đã được gửi", "Order sent");
                body = trayCopyFallback(
                    "checkout.cardSubmittedBodyShort",
                    "Vui lòng đến quầy để thanh toán bằng thẻ.",
                    "Please go to the counter to pay by card."
                );
                statusLabel = trayCopyFallback(
                    "checkout.waitingCardPayment",
                    "Chờ thanh toán bằng thẻ tại quầy",
                    "Waiting for card payment at counter"
                );
                note = "";
            } else {
                title = trayCopyFallback("menuTray.chipSubmittedTitle", "Đơn đã được gửi", "Order sent");
                body = trayCopyFallback(
                    "checkout.cashSubmittedBodyShort",
                    "Vui lòng đến quầy để thanh toán bằng tiền mặt.",
                    "Please go to the counter to pay cash."
                );
                statusLabel = trayCopyFallback(
                    "checkout.waitingCashPayment",
                    "Chờ thanh toán tiền mặt tại quầy",
                    "Waiting for cash payment at counter"
                );
                note = "";
            }
            const cardClass =
                state === TRAY_STATE.SUBMITTED_BANK
                    ? "order-tray-submitted-card order-tray-submitted-card--bank"
                    : "order-tray-submitted-card order-tray-submitted-card--compact";
            const keepOpen = trayCopyFallback(
                "menuTray.chipSubmittedNote",
                "Bạn có thể giữ màn hình này mở để nhận cập nhật.",
                "You can keep this screen open for updates."
            );
            const cta = trayCopyFallback("menuTray.chipTrackCta", "Theo dõi đơn", "Track order");
            const href = String(buildTrackHref(sess)).replace(/"/g, "&quot;");
            const statusHtml = statusLabel
                ? `<p class="order-tray-submitted-card__status">${escapeHtml(statusLabel)}</p>`
                : "";
            const noteHtml = note ? `<p class="order-tray-submitted-card__note">${escapeHtml(note)}</p>` : `<p class="order-tray-submitted-card__note">${escapeHtml(keepOpen)}</p>`;
            el.className = "order-tray-lines order-tray-lines--correspondence pb-0 text-sm";
            el.innerHTML = `<div class="${cardClass}" role="status">
                <div class="order-tray-submitted-card__seal" aria-hidden="true">A</div>
                <p class="order-tray-submitted-card__title">${escapeHtml(title)}</p>
                ${statusHtml}
                <p class="order-tray-submitted-card__body">${escapeHtml(body)}</p>
                ${noteHtml}
                <div id="order-tray-transfer-host"></div>
                <a href="${href}" class="order-tray-submitted-card__link">${escapeHtml(cta)}</a>
            </div>`;
            if (state === TRAY_STATE.SUBMITTED_BANK) void ensureBankTransferQrMounted(sess);
        }

        function updateTraySummary(opts) {
            opts = opts || {};
            const n = totalQty();
            const state = resolveTrayState();
            const submitted = isSubmittedTrayState(state);
            const isEmpty = n === 0 && !submitted;
            const line = document.getElementById("trayCountLine");
            const sheetTitle = document.getElementById("traySheetTitle");
            const chipTitle = document.getElementById("order-tray-chip-title");
            const chipSub = document.getElementById("order-tray-chip-sub");
            const chipStack = document.getElementById("order-tray-chip-stack");
            const chipTotal = document.getElementById("order-tray-chip-total");
            const subEl = document.getElementById("order-tray-subtotal");
            const st = subtotal();
            if (orderTrayRoot) {
                orderTrayRoot.classList.toggle("order-tray-root--empty", isEmpty);
                orderTrayRoot.classList.toggle(
                    "order-tray-root--submitted",
                    submitted && state !== TRAY_STATE.PAID && state !== TRAY_STATE.COMPLETED
                );
                orderTrayRoot.classList.toggle("order-tray-root--submitted-paid", state === TRAY_STATE.PAID);
                orderTrayRoot.classList.toggle("order-tray-root--submitted-complete", state === TRAY_STATE.COMPLETED);
            }
            if (orderTrayChip) orderTrayChip.classList.toggle("order-tray-chip--has-items", n > 0 && !submitted);

            if (sheetTitle) {
                if (submitted) {
                    if (state === TRAY_STATE.COMPLETED) {
                        sheetTitle.textContent = trayCopyFallback("menuTray.chipCompleteTitle", "Đơn đã hoàn thành", "Order complete");
                    } else if (state === TRAY_STATE.PAID) {
                        sheetTitle.textContent = trayCopyFallback("menuTray.chipPaidTitle", "Thanh toán thành công", "Payment successful");
                    } else if (state === TRAY_STATE.SUBMITTED_BANK) {
                        sheetTitle.textContent = trayCopyFallback("checkout.waitingBankTransfer", "Chờ chuyển khoản", "Waiting for bank transfer");
                    } else {
                        sheetTitle.textContent = trayCopyFallback("menuTray.chipSubmittedTitle", "Đơn đã được gửi", "Order sent");
                    }
                } else if (checkoutStep === "review" || checkoutStep === "payment") {
                    sheetTitle.textContent =
                        n === 1
                            ? trayCopyFallback("menuTray.sheetTitleOne", "Khay · 1 món", "Tray · 1 item")
                            : n > 1
                              ? tfmt("menuTray.sheetTitleMany", { n }) ||
                                trayCopyFallback("menuTray.sheetTitleMany", "Khay · " + n + " món", "Tray · " + n + " items")
                              : trayCopyFallback("menuTray.sheetTitle", "Khay của bạn", "Your tray");
                } else {
                    sheetTitle.textContent = trayCopyFallback("menuTray.sheetTitle", "Khay của bạn", "Your tray");
                }
            }

            if (line) {
                if (submitted) {
                    if (state === TRAY_STATE.SUBMITTED_BANK) {
                        line.textContent = trayCopyFallback(
                            "checkout.waitingBankTransfer",
                            "Chờ chuyển khoản",
                            "Waiting for bank transfer"
                        );
                    } else if (state === TRAY_STATE.SUBMITTED_CARD) {
                        line.textContent = trayCopyFallback(
                            "checkout.waitingCardPayment",
                            "Chờ thanh toán bằng thẻ tại quầy",
                            "Waiting for card payment at counter"
                        );
                    } else if (
                        state === TRAY_STATE.SUBMITTED_CASH ||
                        state === TRAY_STATE.SUBMITTED_COUNTER ||
                        state === TRAY_STATE.SUBMITTED_PENDING
                    ) {
                        line.textContent = trayCopyFallback(
                            "checkout.waitingCashPayment",
                            "Chờ thanh toán tiền mặt tại quầy",
                            "Waiting for cash payment at counter"
                        );
                    } else {
                        line.textContent = trayCopyFallback(
                            "menuTray.chipSubmittedNote",
                            "Bạn có thể giữ màn hình này mở để nhận cập nhật.",
                            "You can keep this screen open for updates."
                        );
                    }
                } else if (n === 0) line.textContent = trayCopyFallback("menuTray.countNone", "Chưa có món trên khay.", "No items on your tray yet.");
                else if (n === 1) line.textContent = trayCopyFallback("menuTray.countOne", "Một món trên khay.", "One item on your tray.");
                else line.textContent = tfmt("menuTray.countMany", { n }) || trayCopyFallback("menuTray.countMany", n + " món trên khay.", n + " items on your tray.");
            }

            if (chipTitle) {
                chipTitle.classList.remove("order-tray-chip__count--settling");
                if (submitted) {
                    if (state === TRAY_STATE.COMPLETED) {
                        chipTitle.textContent = trayCopyFallback("menuTray.chipCompleteTitle", "Đơn đã hoàn thành", "Order complete");
                    } else if (state === TRAY_STATE.PAID) {
                        chipTitle.textContent = trayCopyFallback("menuTray.chipPaidTitle", "Thanh toán thành công", "Payment successful");
                    } else {
                        chipTitle.textContent = trayCopyFallback("menuTray.chipSubmittedTitle", "Đơn đã được gửi", "Order sent");
                    }
                } else if (n === 0) {
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
                if (submitted) {
                    if (state === TRAY_STATE.COMPLETED) {
                        chipSub.textContent = trayCopyFallback("menuTray.chipCompleteBody", "Cảm ơn bạn đã ghé Annap.", "Thank you for visiting Annap.");
                    } else if (state === TRAY_STATE.PAID) {
                        chipSub.textContent = trayCopyFallback(
                            "menuTray.chipPreparingShort",
                            "Đơn đang chuẩn bị",
                            "Order in preparation"
                        );
                    } else if (state === TRAY_STATE.SUBMITTED_BANK) {
                        chipSub.textContent = trayCopyFallback(
                            "checkout.waitingBankTransfer",
                            "Chờ chuyển khoản",
                            "Waiting for bank transfer"
                        );
                    } else if (state === TRAY_STATE.SUBMITTED_CARD) {
                        chipSub.textContent = trayCopyFallback(
                            "checkout.waitingCardPayment",
                            "Chờ thanh toán bằng thẻ tại quầy",
                            "Waiting for card payment at counter"
                        );
                    } else if (
                        state === TRAY_STATE.SUBMITTED_CASH ||
                        state === TRAY_STATE.SUBMITTED_COUNTER ||
                        state === TRAY_STATE.SUBMITTED_PENDING
                    ) {
                        chipSub.textContent = trayCopyFallback(
                            "checkout.waitingCashPayment",
                            "Chờ thanh toán tiền mặt tại quầy",
                            "Waiting for cash payment at counter"
                        );
                    } else {
                        chipSub.textContent = trayCopyFallback(
                            "menuTray.chipSubmittedBody",
                            "Nhân viên sẽ đến kiểm tra lại đơn và hỗ trợ thanh toán.",
                            "Staff will come to confirm your order and help with payment."
                        );
                    }
                } else {
                    chipSub.textContent =
                        n === 0
                            ? isSommelierFlowActive()
                                ? trayCopyFallback("menuTray.chipSubSomm", "Chọn một ly để bắt đầu.", "Choose a drink to begin.")
                                : trayCopyFallback("menuTray.chipSubEmpty", "Chọn một ly để bắt đầu.", "Choose a drink to begin.")
                            : trayCopyFallback("menuTray.chipSubFilledShort", "Chạm để mở", "Tap to open");
                }
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
                if (submitted) {
                    chipStack.innerHTML = '<span class="order-tray-chip__seal" aria-hidden="true">A</span>';
                    chipStack.classList.remove("annap-tray-slot-receive");
                } else {
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
            updateTraySheetForState(state);
            updateCheckoutUi();
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
            GuestInteractionContract.removeGuestOrderSession();
            traySubmittedStatus = null;
            resetCheckoutStep();
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

        function itemNoteKey(line) {
            return cartLineKey(line);
        }

        function noteDraftValue(raw) {
            const s = raw != null ? String(raw) : "";
            return s.length > 200 ? s.slice(0, 200) : s;
        }

        function persistLineCustomerNoteDraft(line, rawNote) {
            const key = itemNoteKey(line);
            const draft = noteDraftValue(rawNote);
            const stored = cartItems.get(key);
            if (stored) stored.customerNote = draft;
            if (typeof GuestInteractionContract.setLineCustomerNote === "function") {
                GuestInteractionContract.setLineCustomerNote(line.id, draft, line.guestLabel, { silent: true });
            }
            return draft;
        }

        function commitLineCustomerNote(line, options) {
            options = options || {};
            const key = itemNoteKey(line);
            const stored = cartItems.get(key);
            const raw = stored && stored.customerNote != null ? String(stored.customerNote) : "";
            const trimmed = raw.trim() ? raw.trim().slice(0, 200) : "";
            if (stored) stored.customerNote = trimmed;
            if (typeof GuestInteractionContract.setLineCustomerNote === "function") {
                GuestInteractionContract.setLineCustomerNote(line.id, trimmed || null, line.guestLabel, {
                    silent: options.silent === true
                });
            }
            if (!trimmed && !expandedItemNoteKeys.has(key)) {
                refreshNotePillForKey(key);
            } else if (!expandedItemNoteKeys.has(key)) {
                refreshNotePillForKey(key);
            }
            return trimmed;
        }

        function isNoteEditorFocused() {
            const active = document.activeElement;
            return !!(active && active.classList && active.classList.contains("tray-line-note-input"));
        }

        function syncOpenNoteTextareasFromDom() {
            document.querySelectorAll(".tray-line-note-input").forEach(function (ta) {
                const card = ta.closest ? ta.closest("[data-tray-line-key]") : null;
                if (!card) return;
                const noteKey = card.getAttribute("data-tray-line-key");
                if (!noteKey) return;
                const line = cartItems.get(noteKey);
                if (!line) return;
                persistLineCustomerNoteDraft(line, ta.value);
            });
        }

        function flushAllNoteDraftsForSubmit() {
            syncOpenNoteTextareasFromDom();
            cartItems.forEach(function (line) {
                commitLineCustomerNote(line, { silent: true });
            });
            if (typeof GuestInteractionContract !== "undefined" && typeof GuestInteractionContract.getCartLines === "function") {
                linesToCartMap(GuestInteractionContract.getCartLines());
            }
        }

        function refreshNotePillForKey(noteKey) {
            const card = document.querySelector('[data-tray-line-key="' + String(noteKey).replace(/"/g, '\\"') + '"]');
            if (!card) return;
            const line = cartItems.get(noteKey);
            if (!line) return;
            const letter = card.querySelector(".tray-correspondence-card__letter");
            if (!letter) return;
            const noteExpanded = expandedItemNoteKeys.has(noteKey);
            let pill = letter.querySelector(".tray-line-customer-note--pill");
            const noteText =
                line.customerNote && String(line.customerNote).trim() ? String(line.customerNote).trim() : "";
            if (noteText && !noteExpanded) {
                if (!pill) {
                    pill = document.createElement("p");
                    pill.className = "tray-line-customer-note tray-line-customer-note--pill";
                    const toggle = letter.querySelector(".tray-line-note-toggle");
                    if (toggle && toggle.nextSibling) letter.insertBefore(pill, toggle);
                    else letter.appendChild(pill);
                }
                pill.textContent =
                    (tOrder("menuTray.itemNotePreview") || trayCopyFallback("menuTray.itemNotePreview", "Ghi chú:", "Note:")) +
                    " " +
                    noteText;
            } else if (pill) {
                pill.remove();
            }
        }

        function updateNoteCharCount(wrap, length) {
            if (!wrap) return;
            let counter = wrap.querySelector(".tray-line-note-count");
            if (length <= 0) {
                if (counter) counter.remove();
                return;
            }
            if (!counter) {
                counter = document.createElement("span");
                counter.className = "tray-line-note-count";
                counter.setAttribute("aria-live", "polite");
                wrap.appendChild(counter);
            }
            counter.textContent = String(length) + "/200";
        }

        function stopNoteEditorEvent(ev) {
            if (ev) ev.stopPropagation();
        }

        function renderCart() {
            if (isNoteEditorFocused()) return;
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
                const trayState = resolveTrayState();
                const sess = readSubmittedSession();
                if (isSubmittedTrayState(trayState) && sess) {
                    renderSubmittedTraySheet(sess, trayState);
                    return;
                }
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
                const noteKey = itemNoteKey(line);
                card.setAttribute("data-tray-line-key", noteKey);
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
                const noteText = line.customerNote && String(line.customerNote).trim() ? String(line.customerNote).trim() : "";
                const noteExpanded = expandedItemNoteKeys.has(noteKey);
                const notePreviewLabel =
                    tOrder("menuTray.itemNotePreview") || trayCopyFallback("menuTray.itemNotePreview", "Ghi chú:", "Note:");
                let noteBlock = "";
                if (noteText && !noteExpanded) {
                    noteBlock = `<p class="tray-line-customer-note tray-line-customer-note--pill">${escapeHtml(
                        notePreviewLabel + " " + noteText
                    )}</p>`;
                }
                letter.innerHTML = `<p class="tray-correspondence-card__name">${escapeHtml(line.name)}</p>
                    <p class="tray-correspondence-card__note">${escapeHtml(descriptorForLine(line))}</p>
                    ${noteBlock}`;

                const noteToggle = document.createElement("button");
                noteToggle.type = "button";
                noteToggle.className = "tray-line-note-toggle guest-hit";
                noteToggle.textContent =
                    tOrder("menuTray.itemNoteAction") ||
                    trayCopyFallback("menuTray.itemNoteAction", "Ghi chú", "Note");
                noteToggle.addEventListener("click", function (ev) {
                    stopNoteEditorEvent(ev);
                    if (noteExpanded) {
                        commitLineCustomerNote(line, { silent: true });
                        expandedItemNoteKeys.delete(noteKey);
                    } else {
                        expandedItemNoteKeys.add(noteKey);
                    }
                    renderCart();
                });

                const noteWrap = document.createElement("div");
                noteWrap.className = "tray-line-note-wrap";
                if (noteExpanded) {
                    const ta = document.createElement("textarea");
                    ta.className = "tray-line-note-input";
                    ta.rows = 2;
                    ta.maxLength = 200;
                    ta.setAttribute("inputmode", "text");
                    ta.setAttribute("autocomplete", "off");
                    ta.setAttribute("autocorrect", "on");
                    ta.placeholder =
                        tOrder("menuTray.itemNotePlaceholder") ||
                        trayCopyFallback("menuTray.itemNotePlaceholder", "Ví dụ: ít đá, ít đường...", "e.g. less ice, less sugar...");
                    ta.value = line.customerNote != null ? String(line.customerNote) : noteText;
                    ta.addEventListener("input", function (ev) {
                        stopNoteEditorEvent(ev);
                        const draft = persistLineCustomerNoteDraft(line, ta.value);
                        if (ta.value !== draft) ta.value = draft;
                        updateNoteCharCount(noteWrap, draft.length);
                    });
                    ta.addEventListener("blur", function () {
                        commitLineCustomerNote(line, { silent: true });
                        refreshNotePillForKey(noteKey);
                    });
                    ["focus", "click", "touchstart", "pointerdown", "mousedown"].forEach(function (evtName) {
                        ta.addEventListener(evtName, stopNoteEditorEvent);
                    });
                    noteWrap.appendChild(ta);
                    if (ta.value.length > 0) updateNoteCharCount(noteWrap, ta.value.length);
                }
                letter.appendChild(noteToggle);
                if (noteExpanded) letter.appendChild(noteWrap);

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
            if (checkoutStep !== "payment") {
                handlePrimaryTrayAction();
                return;
            }
            if (!selectedPaymentMethod) {
                if (orderResult) orderResult.textContent = tOrder("checkout.choosePayment") || "Choose payment method.";
                return;
            }
            if (menuOrderSubmitInFlight) return;
            menuOrderSubmitInFlight = true;
            const prepLabel = getPaymentSubmitLabel(selectedPaymentMethod);
            const submitPaymentMethod = selectedPaymentMethod;
            __annapTrayPaymentDevLog("submit start", { paymentMethod: submitPaymentMethod });
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
                if (btn) {
                    btn.textContent = tOrder("order.submitting") || "Sending to the 106/1 bar…";
                    btn.classList.remove("order-tray-submit--success", "order-tray-submit--pulse");
                    btn.classList.add("order-tray-submit--preparing");
                }

                flushAllNoteDraftsForSubmit();

                const items = Array.from(cartItems.values()).map((l) => {
                    const g = (l.guestLabel && String(l.guestLabel).trim()) || "";
                    const item = {
                        menuItemId: l.id,
                        quantity: l.qty,
                        notes: g ? `Guest: ${g}` : null
                    };
                    const cn = l.customerNote && String(l.customerNote).trim() ? String(l.customerNote).trim() : "";
                    if (cn) item.customerNote = cn;
                    return item;
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
                        body: JSON.stringify({
                            venueTableId: VENUE_TABLE_ID,
                            items,
                            idempotencyKey: menuOrderIdempotencyKey,
                            paymentMethod: submitPaymentMethod
                        })
                    });
                    __annapTrayPaymentDevLog("POST /api/orders status", res.status);
                } catch (_net) {
                    if (window.GuestOrderQueue)
                        window.GuestOrderQueue.enqueue(VENUE_TABLE_ID, items, menuOrderIdempotencyKey, submitPaymentMethod);
                    if (orderResult)
                        orderResult.textContent =
                            tOrder("order.queuedSoft") ||
                            "The line softened for a moment — we will place this tray when the room is steady again.";
                    restoreSubmitButtonState(btn, prepLabel);
                    refreshTableIdentityUi();
                    return;
                }

                const payload = await res.json().catch(function () {
                    return null;
                });
                if (!res.ok) {
                    const er = String((payload && payload.error) || "").toLowerCase();
                    let msg =
                        tOrder("order.submitFailedRetry") ||
                        trayCopyFallback(
                            "order.submitFailedRetry",
                            "Không gửi được đơn. Vui lòng thử lại hoặc gọi nhân viên.",
                            "We could not send your order. Please try again or call staff."
                        );
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
                    } else if (er === "database_migration_required") {
                        msg =
                            tOrder("order.serverMaintenance") ||
                            "Hệ thống đang cập nhật — vui lòng gọi nhân viên để gửi đơn.";
                    } else if (res.status === 429) {
                        if (window.GuestOrderQueue)
                            window.GuestOrderQueue.enqueue(VENUE_TABLE_ID, items, menuOrderIdempotencyKey, submitPaymentMethod);
                        msg =
                            tOrder("order.queuedSoft") ||
                            "The line softened for a moment — we will place this tray when the room is steady again.";
                    } else if (res.status >= 500) {
                        msg =
                            tOrder("order.submitFailedStaff") ||
                            "Không gửi được đơn, vui lòng gọi nhân viên.";
                    }
                    __annapTrayPaymentDevLog("submit failed", { status: res.status, error: er || null });
                    if (orderResult) orderResult.textContent = msg;
                    restoreSubmitButtonState(btn, prepLabel);
                    refreshTableIdentityUi();
                    return;
                }

                if (!payload || !payload.id || !payload.guestSessionToken) {
                    __annapTrayPaymentDevLog("submit invalid payload", {
                        hasPayload: !!payload,
                        hasOrderId: !!(payload && payload.id),
                        hasToken: !!(payload && payload.guestSessionToken)
                    });
                    if (orderResult)
                        orderResult.textContent =
                            tOrder("order.submitFailedRetry") ||
                            trayCopyFallback(
                                "order.submitFailedRetry",
                                "Không gửi được đơn. Vui lòng thử lại hoặc gọi nhân viên.",
                                "We could not send your order. Please try again or call staff."
                            );
                    restoreSubmitButtonState(btn, prepLabel);
                    refreshTableIdentityUi();
                    return;
                }

                menuOrderIdempotencyKey = null;
                applySubmitSuccessUi(payload, submitPaymentMethod, btn, prepLabel);
            } finally {
                menuOrderSubmitInFlight = false;
                if (btn && btn.classList.contains("order-tray-submit--preparing")) {
                    restoreSubmitButtonState(btn, prepLabel);
                }
            }
        }

        function annapBindCheckoutControls() {
            if (window._annapCheckoutControlsBound) return;
            window._annapCheckoutControlsBound = true;
            document.querySelectorAll(".order-tray-payment-option").forEach(function (btn) {
                btn.addEventListener("click", function () {
                    const method = btn.getAttribute("data-payment-method");
                    if (!method) return;
                    if (method === PAYMENT_METHOD.BANK && bankTransferConfigured === false) return;
                    selectedPaymentMethod = method;
                    updatePaymentOptionUi();
                    scrollCheckoutCtaIntoView();
                });
            });
            const backBtn = document.getElementById("order-tray-checkout-back");
            if (backBtn) {
                backBtn.addEventListener("click", function () {
                    if (checkoutStep === "payment") setCheckoutStep("review");
                    else resetCheckoutStep();
                    updateCheckoutUi();
                    updateTraySummary();
                });
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
            __annapTrayPaymentDevLog("Annap order tray payment flow loaded", { version: TRAY_PAYMENT_FLOW_VERSION });
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
                refreshSubmittedTrayStatus()
                    .then(function () {
                        updateTraySummary();
                        renderCart();
                        if (shouldPollSubmittedTrayStatus()) startTrayStatusPolling();
                    })
                    .catch(function () {
                        /* ignore */
                    });
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
                annapBindCheckoutControls();
                refreshBankTransferAvailability();
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
                    btn.textContent = tOrder("checkout.reviewOrder") || "Review order";
            } catch (eI18n) {
                __annapMenuBootErr("[ANNAP MENU BOOT] luxury:i18n-changed handler failed", eI18n);
            }
        });

        document.addEventListener("visibilitychange", function () {
            if (document.visibilityState !== "visible" || !readSubmittedSession()) return;
            refreshSubmittedTrayStatus()
                .then(function () {
                    updateTraySummary();
                    renderCart();
                    if (shouldPollSubmittedTrayStatus()) startTrayStatusPolling();
                })
                .catch(function () {
                    /* ignore */
                });
        });

        document.addEventListener("annap:guest-interaction", function (ev) {
            var d = ev && ev.detail;
            if (!d || (d.type !== "cartUpdated" && d.type !== "activeGuestChanged")) return;
            if (isNoteEditorFocused()) return;
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

window.__annapMenuSubmitOrder = handlePrimaryTrayAction;
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
