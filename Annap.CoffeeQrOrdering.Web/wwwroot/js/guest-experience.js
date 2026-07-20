/**
 * Seated guest arrival flows (group / guided sommelier / discovery).
 * Avoid optional chaining in boot paths for older WebKit.
 */
(function (window, document) {
    "use strict";

    function geById(id) {
        return document.getElementById(id);
    }

    function geParseJsonScript(id) {
        var node = geById(id);
        if (!node || !node.textContent) return null;
        try {
            return JSON.parse(node.textContent);
        } catch (e) {
            return null;
        }
    }

    function geEsc(s) {
        return String(s || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function geShow(el, on) {
        if (!el) return;
        if (on) {
            el.classList.remove("hidden");
            el.removeAttribute("hidden");
        } else {
            el.classList.add("hidden");
            el.setAttribute("hidden", "hidden");
        }
    }

    function geFmtMoney(n) {
        if (typeof window.GuestInteractionContract !== "undefined" && window.GuestInteractionContract.formatMoney) {
            return window.GuestInteractionContract.formatMoney(n);
        }
        try {
            var isVi =
                (window.LuxuryI18n && window.LuxuryI18n.getLang && window.LuxuryI18n.getLang() === "vi") ||
                (document.documentElement.lang || "").toLowerCase().indexOf("vi") === 0;
            var amount = Math.round(Number(n) || 0);
            if (isVi) {
                return new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 0 }).format(amount) + "đ";
            }
            return new Intl.NumberFormat("en-US", { style: "currency", currency: "USD", maximumFractionDigits: 0 }).format(amount);
        } catch (e2) {
            return String(n);
        }
    }

    function geVtQuery(root) {
        var vt = root && root.getAttribute("data-vt");
        if (vt && vt.length) return "?vt=" + encodeURIComponent(vt);
        return "";
    }

    function geInit() {
        var root = geById("guest-experience-root");
        if (!root) {
            try {
                if (typeof console !== "undefined" && console.warn) {
                    console.warn("[annap-ge] geInit aborted: #guest-experience-root not in DOM");
                }
            } catch (_r0) {}
            return;
        }

        var landing = geById("ge-landing");
        function geSetLandingVisible(on) {
            if (!landing) return;
            geShow(landing, on);
            if (on) {
                landing.classList.remove("ge-landing--away");
                landing.removeAttribute("aria-hidden");
            } else {
                landing.classList.add("ge-landing--away");
                landing.setAttribute("aria-hidden", "true");
            }
        }
        var panelGroup = geById("ge-panel-group");
        var panelSomm = geById("ge-panel-sommelier");
        var panelDisc = geById("ge-panel-discovery");
        var catalog = geParseJsonScript("ge-guided-catalog-json");
        var sigList = geParseJsonScript("ge-signature-json");
        if (!catalog || !catalog.questions) catalog = { questions: [] };
        if (!sigList) sigList = [];

        var sommStepHost = geById("ge-sommelier-step");
        var sommResults = geById("ge-sommelier-results");
        var discAmbient = geById("ge-discovery-ambient");
        var discLine = geById("ge-discovery-line");
        var discCooldownEl = geById("ge-discovery-cooldown");
        var discDuckBlocked = geById("ge-duck-blocked");
        var discCard = geById("ge-discovery-card");
        var discStage = geById("ge-discovery-stage");
        var discVignette = discStage && discStage.querySelector ? discStage.querySelector(".ge-discovery-vignette") : null;
        var discLetterDesk = geById("ge-letter-desk");

        var answers = [];
        var stepIdx = 0;
        var sommBranchKey = "";
        var sommSpecialtyPath = false;
        var sommCalibrationBranch = "";
        var SOMM_ENTRY_SPECIALTY_ID = "q0_specialty";
        var SOMM_COFFEE_OPTION_ID = "q0_specialty";
        var discNonce = 1;
        var discLastMs = 0;
        var discTimers = [];
        var discAbort = null;
        var discRerollReadyAt = 0;
        var discRerollWatchTimer = null;
        var discRerollCount = 0;
        /** Max secondary reveals from API (default 2). */
        var discHouseMaxRerolls = 2;
        /** Last houseRitual object from API (admin lines, caps). */
        var discLastHouseRitual = null;
        /** Letter Room copy merged from boot JSON + last API `letterRoom`. */
        var discLastLetterRoom = null;
        /** 0–2: chosen sealed envelope (rerolls use a fresh index from nonce). */
        var discChosenEnvelopeIx = null;
        /** One atmospheric signal from the evening gesture (earns the reveal). */
        var discTasteSignals = [];
        /** Incremented on each ritual reset so stale fetch / AbortError callbacks never hijack UI. */
        var discRitualTicket = 0;

        var discLetterRoomBoot = geParseJsonScript("ge-letter-room-desk-json");
        if (!discLetterRoomBoot || typeof discLetterRoomBoot !== "object") discLetterRoomBoot = {};

        function geGuestLangVi() {
            return (
                (window.LuxuryI18n &&
                    window.LuxuryI18n.getLang &&
                    window.LuxuryI18n.getLang() === "vi") ||
                (document.documentElement.lang || "")
                    .toLowerCase()
                    .indexOf("vi") === 0
            );
        }

        function geFlowT(path, viFallback, enFallback) {
            if (window.LuxuryI18n && typeof window.LuxuryI18n.t === "function") {
                var s = window.LuxuryI18n.t(path);
                if (s && String(s).trim()) return String(s).trim();
            }
            return geGuestLangVi()
                ? viFallback
                : enFallback || viFallback;
        }

        function geHouseT(path, viFallback, enFallback) {
            return geFlowT(path, viFallback, enFallback);
        }

        function geFlowTf(path, vars, viFallback, enFallback) {
            if (window.LuxuryI18n && typeof window.LuxuryI18n.tf === "function") {
                var tf = window.LuxuryI18n.tf(path, vars);
                if (tf && String(tf).trim()) return String(tf).trim();
            }
            var tpl = geGuestLangVi()
                ? viFallback
                : enFallback || viFallback;
            if (vars) {
                for (var k in vars) {
                    if (Object.prototype.hasOwnProperty.call(vars, k)) {
                        tpl = tpl.split("{" + k + "}").join(String(vars[k]));
                    }
                }
            }
            return tpl;
        }

        function geSetSommelierFlowActive(on) {
            try {
                if (document.body) {
                    document.body.classList.toggle("ge-sommelier-flow", !!on);
                }
                if (typeof window.__annapRefreshTraySummary === "function") {
                    window.__annapRefreshTraySummary();
                }
            } catch (_sfa) {
                /* ignore */
            }
        }

        /** Ritual copy — Vietnamese-first hospitality voice. */
        function geRitualT(path, viFallback) {
            if (window.LuxuryI18n && typeof window.LuxuryI18n.t === "function") {
                var s = window.LuxuryI18n.t(path);
                if (s && String(s).trim()) return String(s).trim();
            }
            return viFallback || "";
        }

        var sommSettleTimer = null;
        var sommEntryTimer = null;
        var sommNoteAnimTimer = null;
        var SOMM_ENTRY_HOLD_MS = 2200;
        var SOMM_SETTLE_MS = [1300, 1500, 1700, 1900];
        var SOMM_TASTING_SETTLE_MS = 680;
        var SOMM_RECOGNITION_MS = 900;
        var SOMM_NOTE_ROW_MS = 480;
        var RITUAL_SETTLE_MS = 1500;
        var sommRecognitionReadyAt = 0;

        function geHostTableLabel() {
            var raw = root.getAttribute("data-table-label") || "";
            return String(raw).trim();
        }

        function geHostHourBucket() {
            var h = new Date().getHours();
            if (h >= 5 && h < 12) return "morning";
            if (h >= 12 && h < 17) return "afternoon";
            if (h >= 17 && h < 22) return "evening";
            return "night";
        }

        function geHostGreetingLine() {
            var table = geHostTableLabel();
            var bucket = geHostHourBucket();
            var greet = geRitualT(
                "ge.host.greet." + bucket,
                bucket === "morning"
                    ? "chào buổi sáng"
                    : bucket === "afternoon"
                      ? "chào buổi chiều"
                      : "chào buổi tối"
            );
            if (table) {
                return geRitualT("ge.host.tableGreet", "{table} — {greet}.")
                    .replace("{table}", table)
                    .replace("{greet}", greet);
            }
            return greet.charAt(0).toUpperCase() + greet.slice(1) + ".";
        }

        function geHostRoomLine() {
            var bucket = geHostHourBucket();
            return geRitualT(
                "ge.host.room." + bucket,
                geRitualT("ge.host.roomDefault", "Phòng đang chậm lại tối nay.")
            );
        }

        function gePaintLandingHostNotice() {
            var notice = geById("ge-table-notice");
            if (!notice) return;
            notice.textContent = "";
            notice.hidden = true;
            notice.setAttribute("aria-hidden", "true");
        }

        var GE_EVENING_OPTIONS = [
            {
                signal: "slow_evening",
                labelKey: "ge.host.evening.quieter",
                fallback: "Yên hơn tối nay"
            },
            {
                signal: "bright_lift",
                labelKey: "ge.host.evening.brighter",
                fallback: "Sáng hơn một chút"
            },
            {
                signal: "creamy_calm",
                labelKey: "ge.host.evening.soft",
                fallback: "Mềm và không vội"
            },
            {
                signal: "curious_edge",
                labelKey: "ge.host.evening.curious",
                fallback: "Tò mò, nhưng điềm"
            }
        ];

        function geDiscRerollStorageKey() {
            var vt = root.getAttribute("data-vt") || "";
            return "annap_ge_disc_reroll_" + (vt || "na");
        }

        function geDiscResetRerollCount() {
            discRerollCount = 0;
            try {
                window.sessionStorage.removeItem(geDiscRerollStorageKey());
            } catch (eRr) {
                /* ignore */
            }
        }

        function geDiscBumpRerollCount() {
            discRerollCount++;
            try {
                window.sessionStorage.setItem(geDiscRerollStorageKey(), String(discRerollCount));
            } catch (eRb) {
                /* ignore */
            }
        }

        function geDiscLoadRerollCount() {
            try {
                var raw = window.sessionStorage.getItem(geDiscRerollStorageKey());
                var n = parseInt(raw, 10);
                discRerollCount = isFinite(n) && n >= 0 ? n : 0;
            } catch (eRl) {
                discRerollCount = 0;
            }
        }

        function geReducedMotion() {
            try {
                return (
                    window.matchMedia &&
                    window.matchMedia("(prefers-reduced-motion: reduce)").matches
                );
            } catch (eRm) {
                return false;
            }
        }

        function geDiscPushTimer(t) {
            discTimers.push(t);
        }

        function geDiscClearRitual() {
            discRitualTicket++;
            for (var i = 0; i < discTimers.length; i++) {
                window.clearTimeout(discTimers[i]);
            }
            discTimers = [];
            if (discAbort) {
                try {
                    discAbort.abort();
                } catch (eAb) {
                    /* ignore */
                }
                discAbort = null;
            }
            if (discRerollWatchTimer) {
                window.clearTimeout(discRerollWatchTimer);
                discRerollWatchTimer = null;
            }
            if (discStage) {
                discStage.classList.remove("ge-discovery-stage--ritual-active");
                discStage.classList.remove("ge-discovery-stage--reveal");
                discStage.classList.remove("ge-discovery-stage--duck");
                discStage.removeAttribute("data-gdr-scene");
                discStage.removeAttribute("data-gdr-leg");
                discStage.removeAttribute("data-ge-env-ix");
            }
            if (discDuckBlocked) geShow(discDuckBlocked, false);
            if (discAmbient) discAmbient.classList.remove("is-on");
            if (discVignette) discVignette.classList.remove("is-on");
            if (discLine) {
                discLine.textContent = "";
                discLine.className = "ge-discovery-line";
            }
            if (discCooldownEl) {
                discCooldownEl.textContent = "";
                discCooldownEl.classList.add("hidden");
                discCooldownEl.setAttribute("hidden", "hidden");
            }
            if (discCard) {
                discCard.innerHTML = "";
                discCard.classList.remove("ge-discovery-card--reroll-warm");
                discCard.classList.remove("ge-discovery-card--reroll-deep");
                discCard.classList.remove("ge-discovery-card--commit");
            }
            discLastHouseRitual = null;
            discLastLetterRoom = null;
            if (discLetterDesk) geShow(discLetterDesk, false);
        }

        function geShowCooldownMsg(msg) {
            if (!discCooldownEl) return;
            discCooldownEl.textContent = msg || "";
            discCooldownEl.classList.remove("hidden");
            discCooldownEl.removeAttribute("hidden");
        }

        function geHideCooldownMsg() {
            if (!discCooldownEl) return;
            discCooldownEl.textContent = "";
            discCooldownEl.classList.add("hidden");
            discCooldownEl.setAttribute("hidden", "hidden");
        }

        function geDiscArmRerollCooldown() {
            discRerollReadyAt = Date.now() + 2000;
            var btn = discCard && discCard.querySelector ? discCard.querySelector("[data-ge-reroll]") : null;
            if (btn) {
                btn.setAttribute("aria-disabled", "true");
                btn.classList.add("ge-reveal-reroll--cooling");
            }
            if (discRerollWatchTimer) window.clearTimeout(discRerollWatchTimer);
            var wait = Math.max(0, discRerollReadyAt - Date.now());
            discRerollWatchTimer = window.setTimeout(function () {
                discRerollWatchTimer = null;
                var b2 = discCard && discCard.querySelector ? discCard.querySelector("[data-ge-reroll]") : null;
                if (b2) {
                    b2.setAttribute("aria-disabled", "false");
                    b2.classList.remove("ge-reveal-reroll--cooling");
                }
                geHideCooldownMsg();
            }, wait + 80);
        }

        try {
            var sn = window.sessionStorage.getItem("annap_ge_disc_nonce");
            if (sn && !isNaN(parseInt(sn, 10))) discNonce = parseInt(sn, 10);
            var sl = window.sessionStorage.getItem("annap_ge_disc_last");
            if (sl && !isNaN(parseInt(sl, 10))) discLastMs = parseInt(sl, 10);
            geDiscLoadRerollCount();
        } catch (e0) {
            /* ignore */
        }

        var groupCfg = geParseJsonScript("ge-group-settings-json") || {};
        var homepageCfg = geParseJsonScript("ge-homepage-settings-json") || {};
        var geHomepageEmpty = geById("ge-homepage-curated-empty");

        function homepageFlag(key, fallback) {
            if (!homepageCfg || typeof homepageCfg !== "object") return fallback !== false;
            var v = homepageCfg[key];
            if (v === false || v === "false" || v === 0) return false;
            if (v === true || v === "true" || v === 1) return true;
            return fallback !== false;
        }

        function geFlowEnabled(name) {
            if (name === "group") return homepageFlag("isGroupEnabled", true);
            if (name === "sommelier") return homepageFlag("isSoloEnabled", true);
            if (name === "discovery") return homepageFlag("isSommelierEnabled", true);
            return false;
        }

        function applyHomepageModes() {
            var count =
                (geFlowEnabled("group") ? 1 : 0) +
                (geFlowEnabled("sommelier") ? 1 : 0) +
                (geFlowEnabled("discovery") ? 1 : 0);
            if (geHomepageEmpty) geShow(geHomepageEmpty, count === 0);
        }

        function arrivalAlreadyDismissed() {
            try {
                if (sessionStorage.getItem("annap_arrival_done") === "1") return true;
            } catch (_) {}
            var scene = document.getElementById("annap-arrival");
            if (!scene) return true;
            return !!(scene.hidden || scene.classList.contains("is-done"));
        }

        var enteredAfterArrival = false;
        function enterAfterArrival() {
            if (enteredAfterArrival) return;
            enteredAfterArrival = true;
            openFlow("sommelier");
        }

        var geGroupSetup = geById("ge-group-setup");
        var geGroupMain = geById("ge-group-main");
        var geGroupSummary = geById("ge-group-summary");
        var geGroupPrompt = geById("ge-group-prompt");
        var geGroupLead = geById("ge-group-lead");
        var geGroupCountGrid = geById("ge-group-count-grid");
        var geGroupCountContinue = geById("ge-group-count-continue");
        var geGuestRail = geById("ge-guest-rail");
        var geGuestTabsIntro = geById("ge-guest-tabs-intro");
        var geGuestDoneHint = geById("ge-guest-done-hint");
        var geGroupFinishReview = geById("ge-group-finish-review");
        var geGroupSummaryKicker = geById("ge-group-summary-kicker");
        var geGroupSummaryTitle = geById("ge-group-summary-title");
        var geGroupSummaryLead = geById("ge-group-summary-lead");
        var geGroupSummaryLines = geById("ge-group-summary-lines");
        var geGroupSummaryTotal = geById("ge-group-summary-total");
        var geGroupSummaryClosing = geById("ge-group-summary-closing");
        var geGroupSummaryBack = geById("ge-group-summary-back");
        var geGroupSummaryRestart = geById("ge-group-summary-restart");

        var geGroupCountPick = null;

        function geGroupNotifyGuestUi() {
            try {
                document.dispatchEvent(new CustomEvent("annap:guest-interaction", { detail: { type: "activeGuestChanged" } }));
            } catch (_n) {
                /* ignore */
            }
        }

        function geGroupRenderCountChips() {
            geGroupCountPick = null;
            if (!geGroupCountGrid) return;
            geGroupCountGrid.innerHTML = "";
            var mn = parseInt(groupCfg.minGuests, 10);
            var mx = parseInt(groupCfg.maxGuests, 10);
            if (!isFinite(mn) || mn < 1) mn = 1;
            if (!isFinite(mx) || mx < mn) mx = Math.max(mn, 10);
            mx = Math.min(mx, 10);
            mn = Math.min(mn, 10);
            for (var n = 1; n <= 10; n++) {
                var btn = document.createElement("button");
                btn.type = "button";
                btn.className = "ge-count-chip guest-hit";
                btn.setAttribute("data-ge-count-chip", String(n));
                btn.textContent = String(n);
                var allowed = n >= mn && n <= mx;
                btn.disabled = !allowed;
                if (!allowed) btn.classList.add("ge-count-chip--disabled");
                geGroupCountGrid.appendChild(btn);
            }
            geGroupSyncContinueBtn();
        }

        function geGroupSyncContinueBtn() {
            if (!geGroupCountContinue) return;
            var ok = geGroupCountPick != null && isFinite(geGroupCountPick) && geGroupCountPick >= 1 && geGroupCountPick <= 10;
            geGroupCountContinue.disabled = !ok;
            geGroupCountContinue.setAttribute("aria-disabled", ok ? "false" : "true");
            if (ok) geGroupCountContinue.classList.remove("ge-btn-solid--disabled");
            else geGroupCountContinue.classList.add("ge-btn-solid--disabled");
        }

        function geGuestCartHasLine(ix) {
            if (!window.GuestInteractionContract || !window.GuestInteractionContract.getCartLines) return false;
            var lab = geGuestLabelForIndex(ix);
            var lines = window.GuestInteractionContract.getCartLines();
            for (var i = 0; i < lines.length; i++) {
                var L = lines[i];
                var gl = String((L && L.guestLabel) || "").trim();
                if (gl === lab && (Number(L.qty) || 0) > 0) return true;
            }
            return false;
        }

        function geGroupAllGuestsHaveDrinks() {
            var st = geGroupReadState();
            if (!st) return false;
            for (var i = 0; i < st.count; i++) {
                if (!geGuestCartHasLine(i)) return false;
            }
            return true;
        }

        function geGroupCanReviewSummary() {
            var st = geGroupReadState();
            if (!st || st.count < 1) return false;
            if (geGroupAllGuestsHaveDrinks()) return true;
            for (var j = 0; j < st.done.length; j++) {
                if (!st.done[j]) return false;
            }
            return true;
        }

        function geGroupVt() {
            return root && root.getAttribute ? String(root.getAttribute("data-vt") || "").trim() : "";
        }

        function geGroupStorageKey() {
            var vt = geGroupVt();
            return vt ? "annap_ge_group_" + vt : "";
        }

        function geGuestLabelForIndex(i) {
            var tmpl = geHouseT("ge.group.guestLabel", "Khách {n}", "Guest {n}");
            return tmpl.replace("{n}", String(i + 1));
        }

        function geSyncGroupGuestGlobals() {
            try {
                var stG = geGroupReadState();
                window.__annapGroupGuestCount = stG && stG.count ? stG.count : 0;
            } catch (_sg) {
                try {
                    window.__annapGroupGuestCount = 0;
                } catch (_sg2) {
                    /* ignore */
                }
            }
        }

        function geGroupReadState() {
            var k = geGroupStorageKey();
            if (!k) return null;
            try {
                var raw = window.sessionStorage.getItem(k);
                if (!raw) return null;
                var o = JSON.parse(raw);
                if (!o || typeof o !== "object") return null;
                var c = parseInt(o.count, 10);
                if (!isFinite(c) || c < 1) return null;
                var done = o.done;
                if (!Array.isArray(done) || done.length !== c) {
                    done = [];
                    for (var j = 0; j < c; j++) done.push(false);
                } else {
                    done = done.slice();
                }
                var fp = o.firstPassAuto;
                if (!Array.isArray(fp) || fp.length !== c) {
                    fp = [];
                    for (var f = 0; f < c; f++) fp.push(false);
                } else {
                    fp = fp.slice();
                }
                var a = parseInt(o.active, 10);
                if (!isFinite(a) || a < 0 || a >= c) a = 0;
                return { count: c, active: a, done: done, firstPassAuto: fp };
            } catch (eGs) {
                return null;
            }
        }

        function geGroupWriteState(st) {
            var k = geGroupStorageKey();
            if (!k || !st) return;
            try {
                window.sessionStorage.setItem(k, JSON.stringify(st));
            } catch (eGw) {
                /* ignore */
            }
        }

        function geGroupCoalesce(a, b) {
            if (a != null && String(a).trim()) return String(a).trim();
            return b != null ? String(b).trim() : "";
        }

        function geGroupFillCopy() {
            if (geGroupPrompt)
                geGroupPrompt.textContent = geGroupCoalesce(
                    groupCfg.guestCountPrompt,
                    geHouseT("ge.group.countPromptFallback", "Bàn có mấy khách?", "How many guests?")
                );
            if (geGroupLead) geGroupLead.textContent = geGroupCoalesce(groupCfg.guestCountLead, "");
            if (geGuestTabsIntro) geGuestTabsIntro.textContent = geGroupCoalesce(groupCfg.guestTabsIntro, "");
            if (geGuestDoneHint) geGuestDoneHint.textContent = geGroupCoalesce(groupCfg.guestDoneHint, "");
            if (geGroupSummaryKicker)
                geGroupSummaryKicker.textContent = geGroupCoalesce(groupCfg.arrivalKicker, geHouseT("ge.group.summaryKickerFallback", "Bàn của bạn", "Your table"));
            if (geGroupSummaryTitle)
                geGroupSummaryTitle.textContent = geGroupCoalesce(groupCfg.summaryHeadline, geHouseT("ge.group.summaryTitleFallback", "Tổng lựa chọn bàn", "Table summary"));
            if (geGroupSummaryLead) geGroupSummaryLead.textContent = geGroupCoalesce(groupCfg.summaryLead, "");
            if (geGroupSummaryClosing)
                geGroupSummaryClosing.textContent = geGroupCoalesce(groupCfg.hospitalityClosing, "");
        }

        function geGroupUpdateFinishButton() {
            var st = geGroupReadState();
            if (!geGroupFinishReview || !st) return;
            if (geGroupCanReviewSummary()) {
                geGroupFinishReview.classList.remove("hidden");
                geGroupFinishReview.removeAttribute("hidden");
            } else {
                geGroupFinishReview.classList.add("hidden");
                geGroupFinishReview.setAttribute("hidden", "hidden");
            }
        }

        function geGroupShowSummary() {
            if (!geGroupSummary || !geGroupSummaryLines) return;
            try {
                window.__annapGroupBrowseActive = false;
                window.__annapGroupGuestCount = 0;
            } catch (eBr) {
                /* ignore */
            }
            geGroupFillCopy();
            var st = geGroupReadState();
            var html = [];
            var total = 0;
            if (st && window.GuestInteractionContract && window.GuestInteractionContract.getCartLines) {
                var lines = window.GuestInteractionContract.getCartLines();
                var byGuest = {};
                for (var i = 0; i < lines.length; i++) {
                    var L = lines[i];
                    var g = String((L && L.guestLabel) || "").trim();
                    if (!g) g = geHouseT("ge.group.tableLabel", "Bàn", "Table");
                    if (!byGuest[g]) byGuest[g] = [];
                    byGuest[g].push(L);
                    var q = parseInt(L && L.qty, 10) || 0;
                    var up = Number(L && L.unitPrice) || 0;
                    total += q * up;
                }
                var orderedKeys = [];
                for (var gi = 0; gi < st.count; gi++) orderedKeys.push(geGuestLabelForIndex(gi));
                var extras = [];
                for (var ek in byGuest) {
                    if (Object.prototype.hasOwnProperty.call(byGuest, ek) && orderedKeys.indexOf(ek) < 0) extras.push(ek);
                }
                extras.sort();
                var keys2 = orderedKeys.concat(extras);
                for (var k = 0; k < keys2.length; k++) {
                    var key = keys2[k];
                    var arr = byGuest[key];
                    html.push('<div class="ge-sum-guest"><p class="ge-sum-guest-label">' + geEsc(key) + "</p>");
                    if (!arr || !arr.length) {
                        html.push('<p class="ge-sum-empty">' + geEsc(geHouseT("ge.group.noGuest", "Khách này chưa có ly nào.", "No cups for this guest yet.")) + "</p></div>");
                        continue;
                    }
                    html.push("<ul>");
                    for (var j = 0; j < arr.length; j++) {
                        var it = arr[j];
                        var nm = geEsc((it && it.name) || "");
                        var q2 = parseInt(it && it.qty, 10) || 0;
                        html.push(
                            '<li class="ge-sum-line"><span class="ge-sum-qty">\u00d7' +
                                geEsc(String(q2)) +
                                '</span><span class="ge-sum-name">' +
                                nm +
                                "</span></li>"
                        );
                    }
                    html.push("</ul></div>");
                }
            }
            geGroupSummaryLines.innerHTML = html.join("");
            if (geGroupSummaryTotal) {
                var fmt =
                    window.GuestInteractionContract && window.GuestInteractionContract.formatMoney
                        ? window.GuestInteractionContract.formatMoney(total)
                        : String(total);
                geGroupSummaryTotal.textContent = geHouseT("ge.group.tableTotal", "Tổng bàn · ", "Table total · ") + fmt;
            }
            geShow(geGroupMain, false);
            geShow(geGroupSetup, false);
            geShow(geGroupSummary, true);
            geGroupNotifyGuestUi();
        }

        function geGroupOnOpen() {
            geGroupFillCopy();
            var vt = geGroupVt();
            if (!vt) {
                geShow(geGroupSetup, false);
                geShow(geGroupMain, true);
                geShow(geGroupSummary, false);
                try {
                    window.__annapActiveGroupGuestLabel = "";
                    window.__annapGroupBrowseActive = false;
                    window.__annapGroupGuestCount = 0;
                } catch (e1) {
                    /* ignore */
                }
                return;
            }
            var st = geGroupReadState();
            if (!st) {
                geGroupRenderCountChips();
                geShow(geGroupSetup, true);
                geShow(geGroupMain, false);
                geShow(geGroupSummary, false);
                try {
                    window.__annapActiveGroupGuestLabel = "";
                    window.__annapGroupBrowseActive = false;
                    window.__annapGroupGuestCount = 0;
                } catch (e2) {
                    /* ignore */
                }
            } else {
                geShow(geGroupSetup, false);
                geShow(geGroupMain, true);
                geShow(geGroupSummary, false);
                try {
                    window.__annapActiveGroupGuestLabel = geGuestLabelForIndex(st.active);
                    window.__annapGroupBrowseActive = true;
                } catch (e3) {
                    /* ignore */
                }
                geSyncGroupGuestGlobals();
                geGroupRenderRail();
                geGroupUpdateFinishButton();
            }
        }

        function geGroupSetActiveIndex(ix) {
            var st = geGroupReadState();
            if (!st) return;
            var prev = st.active;
            st.active = ix;
            geGroupWriteState(st);
            window.__annapActiveGroupGuestLabel = geGuestLabelForIndex(ix);
            try {
                window.__annapGroupBrowseActive = true;
            } catch (_e2b) {
                /* ignore */
            }
            geGroupRenderRail();
            geGroupUpdateFinishButton();
            geGroupNotifyGuestUi();
            var hint = geById("ge-passing-hint");
            if (hint && st.count > 1 && ix !== prev) {
                var passTmpl = geHouseT("ge.group.passingMenu", "Truyền thực đơn cho {name}…", "Passing the menu to {name}…");
                hint.textContent = passTmpl.replace("{name}", geGuestLabelForIndex(ix));
                window.setTimeout(function () {
                    if (hint && geGroupReadState() && geGroupReadState().active === ix) hint.textContent = "";
                }, 2600);
            }
        }

        function geGroupToggleDone(ix) {
            var st = geGroupReadState();
            if (!st || ix < 0 || ix >= st.count) return;
            st.done[ix] = !st.done[ix];
            geGroupWriteState(st);
            geGroupRenderRail();
            geGroupUpdateFinishButton();
            geGroupNotifyGuestUi();
        }

        function geGroupRenderRail() {
            if (!geGuestRail) return;
            var st = geGroupReadState();
            geGuestRail.innerHTML = "";
            if (!st) return;
            geSyncGroupGuestGlobals();
            for (var i = 0; i < st.count; i++) {
                var lab = geGuestLabelForIndex(i);
                var hasLine = geGuestCartHasLine(i);
                var isActive = i === st.active;
                var row = document.createElement("div");
                row.className = "ge-guest-row";
                var pill = document.createElement("button");
                pill.type = "button";
                pill.className = "ge-guest-pill guest-hit";
                pill.setAttribute("role", "tab");
                pill.setAttribute("aria-selected", isActive ? "true" : "false");
                pill.setAttribute("data-ge-guest-ix", String(i));
                if (isActive) pill.classList.add("is-active");
                if (hasLine) pill.classList.add("is-complete");
                if (isActive && !hasLine) pill.classList.add("is-awaiting-cup");
                if (!hasLine && !isActive) pill.classList.add("is-pending");
                pill.setAttribute(
                    "aria-label",
                    lab +
                        (hasLine
                            ? geHouseT("ge.group.cupOnTray", ", đã có ly", ", cup on tray")
                            : isActive
                            ? geHouseT("ge.group.yourTurn", ", đến lượt chọn", ", your turn to choose")
                            : geHouseT("ge.group.notServed", ", chưa chọn", ", not yet served"))
                );
                var inner = document.createElement("span");
                inner.className = "ge-guest-pill-inner";
                var labEl = document.createElement("span");
                labEl.className = "ge-guest-pill-label";
                labEl.textContent = lab;
                inner.appendChild(labEl);
                if (hasLine) {
                    var chk = document.createElement("span");
                    chk.className = "ge-guest-pill-check";
                    chk.setAttribute("aria-hidden", "true");
                    chk.textContent = "\u2713";
                    inner.appendChild(chk);
                }
                pill.appendChild(inner);
                var doneBtn = document.createElement("button");
                doneBtn.type = "button";
                doneBtn.className = "ge-guest-done guest-hit";
                doneBtn.setAttribute("data-ge-guest-done", String(i));
                doneBtn.textContent = st.done[i] ? geHouseT("ge.group.undoBtn", "Hoàn tác", "Undo") : geHouseT("ge.group.doneBtn", "Xong", "Done");
                row.appendChild(pill);
                row.appendChild(doneBtn);
                geGuestRail.appendChild(row);
            }
        }

        function centerArrivalModes() {
            try {
                var stack =
                    landing && landing.querySelector
                        ? landing.querySelector("#ge-ritual-invitation")
                        : null;
                if (!stack) {
                    window.scrollTo(0, 0);
                    return;
                }
                var vv = window.visualViewport;
                var vh = (vv && vv.height) || window.innerHeight || document.documentElement.clientHeight || 0;
                var currentY = window.scrollY || document.documentElement.scrollTop || 0;
                var rect = stack.getBoundingClientRect();
                var breathingRoom = Math.max(46, Math.min(96, vh * 0.14));
                var targetY = currentY + rect.top - Math.max(breathingRoom, (vh - rect.height) * 0.42);
                if (!isFinite(targetY) || targetY < 0) targetY = 0;
                window.scrollTo(0, Math.round(targetY));
            } catch (_cm) {
                try { window.scrollTo(0, 0); } catch (_st) { /* ignore */ }
            }
        }

        function enterModePending() {
            try {
                var html = document.documentElement;
                html.classList.add("annap-mode-pending");
                if (landing) landing.classList.add("ge-landing--mode-focus");
                centerArrivalModes();
                window.requestAnimationFrame(function () {
                    centerArrivalModes();
                });
                window.setTimeout(centerArrivalModes, 180);
            } catch (_mp) { /* ignore */ }
        }

        function exitModePending() {
            try {
                if (window.__annapModeFocusTimer) {
                    clearTimeout(window.__annapModeFocusTimer);
                    window.__annapModeFocusTimer = null;
                }
                document.documentElement.classList.remove("annap-mode-pending", "annap-mode-focus");
                if (landing) landing.classList.remove("ge-landing--mode-focus");
            } catch (_xp) { /* ignore */ }
        }

        function geFocusFlowPanel(panel) {
            try {
                if (root) root.classList.add("ge-root--flow-active");
                window.scrollTo(0, 0);
                if (document.documentElement) document.documentElement.scrollTop = 0;
                if (document.body) document.body.scrollTop = 0;
            } catch (_fs) {
                /* ignore */
            }
            if (!panel) return;
            var anchor =
                panel.querySelector(".ge-sommelier-q") ||
                panel.querySelector(".ge-sommelier-step") ||
                panel.querySelector(".ge-panel-title") ||
                panel.querySelector(".ge-letter-desk") ||
                panel;
            function snap() {
                try {
                    window.scrollTo(0, 0);
                    if (document.documentElement) document.documentElement.scrollTop = 0;
                    if (document.body) document.body.scrollTop = 0;
                    var reduce = geReducedMotion();
                    if (anchor.scrollIntoView) {
                        try {
                            anchor.scrollIntoView({
                                block: "start",
                                inline: "nearest",
                                behavior: reduce ? "auto" : "instant"
                            });
                        } catch (_si) {
                            anchor.scrollIntoView(true);
                        }
                    }
                    window.scrollTo(0, 0);
                } catch (_sn) {
                    /* ignore */
                }
            }
            snap();
            window.requestAnimationFrame(snap);
            window.setTimeout(snap, 80);
        }

        function geClearFlowPanelFocus() {
            try {
                if (root) root.classList.remove("ge-root--flow-active");
            } catch (_cf) {
                /* ignore */
            }
        }

        function hideFlow() {
            try {
                window.__annapActiveGroupGuestLabel = "";
                window.__annapGroupBrowseActive = false;
                window.__annapGroupGuestCount = 0;
            } catch (eH) {
                /* ignore */
            }
            try {
                var vk = geGroupVt();
                if (vk) window.sessionStorage.removeItem("annap_ge_group_" + vk);
            } catch (eHS) {
                /* ignore */
            }
            // Restore canonical atmosphere — the landing has no committed mode.
            try {
                if (window.AnnapAtmosphereManager && typeof window.AnnapAtmosphereManager.clearSession === "function") {
                    window.AnnapAtmosphereManager.clearSession();
                }
            } catch (_atm) {
                /* ignore */
            }
            geDiscResetRerollCount();
            geDiscClearRitual();
            geClearFlowPanelFocus();
            sommClearEntryTimer();
            sommClearNoteCardAnim();
            sommSetWaitingQuietTray(false);
            geSetSommelierFlowActive(false);
            geSetLandingVisible(false);
            geShow(panelGroup, false);
            geShow(panelSomm, false);
            geShow(panelDisc, false);
            if (discLetterDesk) geShow(discLetterDesk, false);
            try {
                if (root) root.classList.remove("ge-root--flow-active");
            } catch (_cf2) { /* ignore */ }
            // No welcome landing — leave flows via menu.
            try {
                var vtLeave = root.getAttribute("data-vt");
                if (vtLeave) {
                    window.location.href = "/Menu/Index?vt=" + encodeURIComponent(vtLeave);
                }
            } catch (_nav) { /* ignore */ }
        }

        // bfcache restore: Arrival stays dismissed; reopen Sommelier (no welcome landing).
        window.addEventListener("pageshow", function (ev) {
            if (!ev.persisted) return;
            try {
                if (window.__annapModeFocusTimer) {
                    clearTimeout(window.__annapModeFocusTimer);
                    window.__annapModeFocusTimer = null;
                }
                exitModePending();
            } catch (_mfps) { /* ignore */ }
            try {
                if (window.AnnapAtmosphereManager && typeof window.AnnapAtmosphereManager.clearSession === "function") {
                    window.AnnapAtmosphereManager.clearSession();
                }
            } catch (_atm) { /* ignore */ }
            try {
                var scene = document.getElementById("annap-arrival");
                if (scene) {
                    scene.classList.add("is-done");
                    scene.hidden = true;
                    scene.setAttribute("aria-hidden", "true");
                }
                document.documentElement.classList.remove("annap-arrival-active");
            } catch (_sc) { /* ignore */ }
            enterAfterArrival();
        });

        function openFlow(name) {
            exitModePending();
            geSetLandingVisible(false);
            try {
                if (root) root.classList.add("ge-root--flow-active");
            } catch (_rf) {
                /* ignore */
            }
            try {
                window.scrollTo(0, 0);
                if (document.documentElement) document.documentElement.scrollTop = 0;
                if (document.body) document.body.scrollTop = 0;
            } catch (_rs) {
                /* ignore */
            }
            try {
                if (window.__ANNAP_DEBUG === true && typeof console !== "undefined" && console.log) {
                    console.log("[annap-ge] pathway selected", name);
                }
            } catch (_pf) {}
            try {
                if (window.AnnapAtmosphereManager && typeof window.AnnapAtmosphereManager.mapGuestFlow === "function") {
                    var amode = window.AnnapAtmosphereManager.mapGuestFlow(name);
                    if (amode && typeof window.AnnapAtmosphereManager.commit === "function") {
                        window.AnnapAtmosphereManager.commit(amode);
                    }
                }
            } catch (_atm) {
                /* ignore */
            }
            if (name !== "group") {
                try {
                    window.__annapGroupBrowseActive = false;
                    window.__annapGroupGuestCount = 0;
                } catch (_ob) {
                    /* ignore */
                }
            }
            geShow(panelGroup, name === "group");
            geShow(panelSomm, name === "sommelier");
            geShow(panelDisc, name === "discovery");
            geSetSommelierFlowActive(name === "sommelier");
            if (name === "group") {
                geGroupOnOpen();
            }
            if (name === "sommelier") {
                answers = [];
                stepIdx = 0;
                sommBranchKey = "";
                sommSpecialtyPath = false;
                sommCalibrationBranch = "";
                if (sommSettleTimer) {
                    window.clearTimeout(sommSettleTimer);
                    sommSettleTimer = null;
                }
                sommClearEntryTimer();
                if (panelSomm) {
                    panelSomm.classList.remove(
                        "ge-panel--sommelier-revealing",
                        "ge-panel--ritual-settling",
                        "ge-panel--somm-calibration",
                        "ge-panel--somm-recognizing",
                        "ge-panel--somm-waiting"
                    );
                    geRitualApplyDepth(panelSomm, 0);
                }
                if (sommResults) sommResults.classList.remove("ge-sommelier-results--cup-moment");
                geShow(sommResults, false);
                sommBeginEntry();
                geFocusFlowPanel(panelSomm);
            }
            if (name === "discovery") {
                discChosenEnvelopeIx = null;
                discTasteSignals = [];
                geDiscClearRitual();
                geHideCooldownMsg();
                geShow(discCard, false);
                geLetterDeskResetVisual();
                geLetterDeskPaint();
                if (panelDisc) {
                    panelDisc.classList.remove("ge-panel--ritual-settling");
                    geRitualApplyDepth(panelDisc, 0);
                }
                var gw0 = geById("ge-evening-gesture");
                if (gw0) gw0.classList.remove("ge-evening-gesture--settling");
                if (discLetterDesk) geShow(discLetterDesk, true);
                geFocusFlowPanel(panelDisc);
            }
            if (name === "group") {
                geFocusFlowPanel(panelGroup);
            }
        }

        geSetLandingVisible(false);
        geShow(panelGroup, false);
        geShow(panelSomm, false);
        geShow(panelDisc, false);
        if (discLetterDesk) geShow(discLetterDesk, false);

        /* Arrival → Sommelier only. No intermediate welcome landing. */
        window.addEventListener("annap-arrival-complete", function () {
            try {
                enterAfterArrival();
            } catch (_ae) { /* ignore */ }
        });

        applyHomepageModes();
        gePaintLandingHostNotice();

        if (arrivalAlreadyDismissed()) {
            enterAfterArrival();
        }

        root.addEventListener("click", function (ev) {
            var t = ev.target;
            if (!t || !t.closest) return;
            var back = t.closest("[data-ge-back]");
            if (back) {
                ev.preventDefault();
                hideFlow();
                return;
            }
            var flow = t.closest("[data-ge-flow]");
            if (flow) {
                ev.preventDefault();
                if (flow.hidden || flow.classList.contains("ge-choice--off")) return;
                var f = flow.getAttribute("data-ge-flow");
                if (f) openFlow(f);
                return;
            }
            var addId = t.closest("[data-ge-add]");
            if (addId) {
                ev.preventDefault();
                var id = addId.getAttribute("data-ge-add");
                if (id && typeof window.quickAdd === "function")
                    window.quickAdd(
                        id,
                        typeof window.__annapActiveGroupGuestLabel === "string"
                            ? window.__annapActiveGroupGuestLabel
                            : "",
                        addId
                    );
                return;
            }
            var openDetail = t.closest("[data-ge-open-detail]");
            if (openDetail) {
                ev.preventDefault();
                var did = openDetail.getAttribute("data-ge-open-detail");
                if (!did) return;
                if (typeof window.openDrinkDetail === "function" && document.getElementById("drink-detail-modal")) {
                    var letterMode =
                        openDetail.getAttribute("data-detail-mode") === "origin-letter";
                    window.openDrinkDetail(
                        did,
                        letterMode ? { mode: "origin-letter" } : undefined
                    );
                } else {
                    var nav = window.GuestInteractionContract && window.GuestInteractionContract.navigation;
                    window.location.href = nav ? nav.drinkDetailHref(did) : ("/menu/drink/" + encodeURIComponent(did));
                }
                return;
            }
        });

        if (geGroupCountContinue) {
            geGroupCountContinue.addEventListener("click", function (ev) {
                ev.preventDefault();
                var n = geGroupCountPick;
                if (n == null || !isFinite(n) || n < 1) return;
                try {
                    if (
                        window.GuestInteractionContract &&
                        typeof window.GuestInteractionContract.clearGroupGuestLabeledLines === "function"
                    ) {
                        window.GuestInteractionContract.clearGroupGuestLabeledLines();
                    }
                } catch (eClr) {
                    /* ignore */
                }
                var done = [];
                var fp = [];
                for (var i = 0; i < n; i++) {
                    done.push(false);
                    fp.push(false);
                }
                geGroupWriteState({ count: n, active: 0, done: done, firstPassAuto: fp });
                geShow(geGroupSetup, false);
                geShow(geGroupMain, true);
                try {
                    window.__annapActiveGroupGuestLabel = geGuestLabelForIndex(0);
                    window.__annapGroupBrowseActive = true;
                    window.__annapGroupGuestCount = n;
                } catch (eGc) {
                    /* ignore */
                }
                geGroupRenderRail();
                geGroupUpdateFinishButton();
                geGroupNotifyGuestUi();
            });
        }
        if (geGroupSetup) {
            geGroupSetup.addEventListener("click", function (ev) {
                var b = ev.target && ev.target.closest ? ev.target.closest("[data-ge-count-chip]") : null;
                if (!b || b.disabled) return;
                ev.preventDefault();
                var n = parseInt(b.getAttribute("data-ge-count-chip"), 10);
                if (!isFinite(n)) return;
                geGroupCountPick = n;
                var chips = geGroupCountGrid ? geGroupCountGrid.querySelectorAll("[data-ge-count-chip]") : [];
                for (var i = 0; i < chips.length; i++) {
                    chips[i].classList.toggle("is-selected", chips[i] === b);
                }
                geGroupSyncContinueBtn();
            });
        }
        if (geGroupSummaryRestart) {
            geGroupSummaryRestart.addEventListener("click", function (ev) {
                ev.preventDefault();
                var k = geGroupStorageKey();
                try {
                    if (k) window.sessionStorage.removeItem(k);
                } catch (eR) {
                    /* ignore */
                }
                geGroupCountPick = null;
                if (window.GuestInteractionContract && window.GuestInteractionContract.clearCart) {
                    window.GuestInteractionContract.clearCart();
                }
                try {
                    window.__annapActiveGroupGuestLabel = "";
                    window.__annapGroupBrowseActive = false;
                    window.__annapGroupGuestCount = 0;
                } catch (_r) {
                    /* ignore */
                }
                geShow(geGroupSummary, false);
                geGroupRenderCountChips();
                geShow(geGroupSetup, true);
                geShow(geGroupMain, false);
            });
        }
        if (geGuestRail) {
            geGuestRail.addEventListener("click", function (ev) {
                var t2 = ev.target;
                if (!t2 || !t2.closest) return;
                var pill = t2.closest("[data-ge-guest-ix]");
                if (pill) {
                    ev.preventDefault();
                    var ix = parseInt(pill.getAttribute("data-ge-guest-ix"), 10);
                    if (isFinite(ix)) geGroupSetActiveIndex(ix);
                    return;
                }
                var dn = t2.closest("[data-ge-guest-done]");
                if (dn) {
                    ev.preventDefault();
                    var j = parseInt(dn.getAttribute("data-ge-guest-done"), 10);
                    if (isFinite(j)) geGroupToggleDone(j);
                }
            });
        }
        if (geGroupFinishReview) {
            geGroupFinishReview.addEventListener("click", function (ev) {
                ev.preventDefault();
                geGroupShowSummary();
            });
        }
        if (geGroupSummaryBack) {
            geGroupSummaryBack.addEventListener("click", function (ev) {
                ev.preventDefault();
                geShow(geGroupSummary, false);
                geShow(geGroupMain, true);
                try {
                    window.__annapGroupBrowseActive = true;
                    var stBk = geGroupReadState();
                    if (stBk) window.__annapActiveGroupGuestLabel = geGuestLabelForIndex(stBk.active);
                } catch (_bk) {
                    /* ignore */
                }
                geSyncGroupGuestGlobals();
                geGroupNotifyGuestUi();
            });
        }

        document.addEventListener("annap:guest-interaction", function (ev) {
            var d = ev && ev.detail;
            if (!d || (d.type !== "cartUpdated" && d.type !== "activeGuestChanged")) return;
            if (!panelGroup || panelGroup.classList.contains("hidden")) return;
            geGroupRenderRail();
            geGroupUpdateFinishButton();
        });

        /* —— Group: add-to-tray wired via delegation above —— */

        /* —— Cup moment (single hosted pour; shared by guided + discovery) —— */
        var GE_CUP_NAME_DELAY = 1000;
        var GE_CUP_PRICE_DELAY = 1800;
        var GE_CUP_SERVE_DELAY = 2800;
        var GE_CUP_TRAY_DELAY = 3800;
        var GE_CUP_ALTS_DELAY = 5600;

        function geCupMomentHostLine(item) {
            if (!item) return "";
            return item.hostNote || item.emotionalExplanation || item.tastingNotes || "";
        }

        function geCupMomentTastingLine(item) {
            return geCupMomentHostLine(item);
        }

        function geCupMomentNotesLine(item) {
            if (!item) return "";
            var primary = geCupMomentTastingLine(item);
            var tn = item.tastingNotes || "";
            if (tn && tn !== primary) return tn;
            return "";
        }

        function geCupMomentHeroId(item) {
            if (!item) return "";
            return item.menuItemId || item.menuitemId || item.id || item.Id || "";
        }

        function geBuildCupMomentHtml(config) {
            config = config || {};
            var hero = config.hero || {};
            var isSpecialty = config.mode === "specialty";
            var alts = isSpecialty ? [] : config.alts || [];
            var addAttr = config.addAttr || "data-ge-add";
            var mid = geCupMomentHeroId(hero);
            var img = hero.imageUrl || "";
            var name = hero.name || "";
            var origin = hero.origin || "";
            var displayName =
                isSpecialty && origin
                    ? origin + " · " + name
                    : name;
            var price =
                hero.price != null && hero.price !== ""
                    ? (function () {
                          try {
                              return geFmtMoney(hero.price);
                          } catch (_pe) {
                              return "";
                          }
                      })()
                    : "";
            var whisperLine = "";
            if (isSpecialty) {
                whisperLine =
                    (hero.shortStory && String(hero.shortStory).trim()) ||
                    (hero.tastingNotes && String(hero.tastingNotes).trim()) ||
                    "";
            }
            var taste = geCupMomentTastingLine(hero);
            var notes = geCupMomentNotesLine(hero);
            var defaultReason = geHouseT(
                "ge.host.serveLine",
                "Annap chọn cho bạn.",
                "Annap chooses for you."
            );
            var reasonText =
                config.reasonText != null && String(config.reasonText).trim()
                    ? String(config.reasonText).trim()
                    : config.serveLine || defaultReason;
            var tasteLine = "";
            if (isSpecialty) {
                tasteLine = whisperLine;
            } else if (notes && notes !== reasonText) {
                tasteLine = notes;
            } else if (taste && taste !== reasonText) {
                tasteLine = taste;
            }
            var ctaLabel =
                config.ctaLabel ||
                (isSpecialty
                    ? geFlowT("ge.host.prepareForTable", "Pha cho bàn tôi", "Prepare for my table")
                    : geFlowT("ge.host.trayInvite", "Thêm vào khay", "Add to tray"));
            var detailLabel =
                config.detailLabel ||
                geFlowT(
                    "ge.host.readOriginLetter",
                    "Đọc lá thư nguồn gốc",
                    "Read the origin letter"
                );
            var altSectionTitle =
                config.altSectionTitle ||
                geFlowT(
                    "ge.cupMoment.altSectionTitle",
                    "Nếu bạn muốn đổi hướng",
                    "If you want another direction"
                );
            var altAddLabel =
                config.altAddLabel ||
                geFlowT("ge.host.altPour", "Chọn ly này", "Choose this cup");
            var isReveal = isSpecialty || !!config.showRecBadge;
            var parts = [];
            parts.push(
                '<section class="ge-rec-result' +
                    (isReveal ? " ge-rec-result--reveal" : "") +
                    (isSpecialty ? " ge-rec-result--specialty" : "") +
                    '">'
            );
            if (!isSpecialty) {
                parts.push(
                    '<p class="ge-rec-result__kicker ge-cup-moment__kicker ge-cup-moment__kicker--held">' +
                        geEsc(
                            geFlowT(
                                "ge.cupMoment.namingKicker",
                                "Annap chọn cho bạn",
                                "Annap chooses for you"
                            )
                        ) +
                        "</p>"
                );
            }
            if (isSpecialty) {
                parts.push(
                    '<p class="ge-rec-result__kicker ge-cup-moment__kicker ge-cup-moment__kicker--held">' +
                        geEsc(
                            geFlowT(
                                "ge.cupMoment.namingKicker",
                                "Gợi ý hôm nay",
                                "Today's recommendation"
                            )
                        ) +
                        "</p>"
                );
            }
            parts.push(
                '<article class="ge-rec-card ge-cup-moment' +
                    (isReveal ? " ge-rec-card--reveal ge-rec-card--held" : "") +
                    (isSpecialty ? " ge-cup-moment--specialty" : "") +
                    '">'
            );
            if (!isSpecialty) {
                parts.push(
                    '<div class="ge-rec-card__media ge-rec-card__poster ge-cup-moment__visual">'
                );
                parts.push(
                    '<img src="' +
                        geEsc(img || "/images/menu-fallback.svg") +
                        '" alt="" loading="lazy" decoding="async" referrerpolicy="no-referrer" onerror="this.onerror=null;this.src=\'/images/menu-fallback.svg\'" />'
                );
                parts.push("</div>");
            }
            parts.push('<div class="ge-rec-card__body ge-cup-moment__body">');
            parts.push(
                '<h2 class="ge-rec-card__name ge-cup-moment__name ge-cup-moment__name--held">' +
                    geEsc(displayName) +
                    "</h2>"
            );
            if (reasonText && !isSpecialty) {
                parts.push(
                    '<p class="ge-rec-card__reason ge-rec-card__reason--held">' +
                        geEsc(reasonText) +
                        "</p>"
                );
            }
            if (tasteLine) {
                parts.push(
                    '<p class="ge-rec-card__taste ge-cup-moment__taste ge-cup-moment__notes--held">' +
                        geEsc(tasteLine) +
                        "</p>"
                );
            }
            if (isSpecialty) {
                parts.push(
                    '<div class="ge-rec-card__media ge-rec-card__poster ge-cup-moment__visual ge-cup-moment__visual--held">'
                );
                parts.push(
                    '<img src="' +
                        geEsc(img || "/images/menu-fallback.svg") +
                        '" alt="" loading="lazy" decoding="async" referrerpolicy="no-referrer" onerror="this.onerror=null;this.src=\'/images/menu-fallback.svg\'" />'
                );
                parts.push("</div>");
            }
            if (price) {
                parts.push('<div class="ge-rec-card__price-row">');
                parts.push(
                    '<p class="ge-rec-card__price ge-cup-moment__price ge-cup-moment__price--held">' +
                        geEsc(price) +
                        "</p>"
                );
                parts.push("</div>");
            }
            parts.push(
                '<div class="ge-rec-card__cta ge-cup-moment__cta-wrap ge-cup-moment__cta-wrap--held">'
            );
            parts.push(
                '<button type="button" class="ge-btn-solid ge-btn-solid--tray guest-hit ge-cup-moment__cta" ' +
                    addAttr +
                    '="' +
                    geEsc(String(mid)) +
                    '" data-item-name="' +
                    geEsc(name) +
                    '">' +
                    geEsc(ctaLabel) +
                    "</button>"
            );
            parts.push("</div>");
            if (mid) {
                parts.push(
                    '<button type="button" class="ge-rec-card__detail ge-cup-moment__detail ge-cup-moment__detail--held guest-hit" data-ge-open-detail="' +
                        geEsc(String(mid)) +
                        '"' +
                        (isSpecialty ? ' data-detail-mode="origin-letter"' : "") +
                        ">" +
                        geEsc(detailLabel) +
                        "</button>"
                );
            }
            parts.push("</div></article>");

            if (alts.length > 0) {
                parts.push(
                    '<section class="ge-rec-alts ge-cup-alts ge-cup-alts--held' +
                        (isReveal ? " ge-rec-alts--shelf" : "") +
                        '">'
                );
                parts.push(
                    '<h3 class="ge-rec-alts__title">' +
                        geEsc(altSectionTitle) +
                        "</h3>"
                );
                parts.push('<ul class="ge-rec-alts__list">');
                for (var ai = 0; ai < alts.length; ai++) {
                    var alt = alts[ai] || {};
                    var altMid = geCupMomentHeroId(alt);
                    var altName = alt.name || "";
                    var altImg = alt.imageUrl || "/images/menu-fallback.svg";
                    var altPrice =
                        alt.price != null && alt.price !== ""
                            ? (function () {
                                  try {
                                      return geFmtMoney(alt.price);
                                  } catch (_ap) {
                                      return "";
                                  }
                              })()
                            : "";
                    parts.push('<li class="ge-rec-alt-row ge-cup-alt-row">');
                    parts.push(
                        '<div class="ge-rec-alt-row__thumb ge-cup-alt-row__thumb"><img src="' +
                            geEsc(altImg) +
                            '" alt="" loading="lazy" decoding="async" onerror="this.onerror=null;this.src=\'/images/menu-fallback.svg\'" /></div>'
                    );
                    parts.push('<div class="ge-rec-alt-row__meta ge-cup-alt-row__meta">');
                    parts.push(
                        '<p class="ge-rec-alt-row__name ge-cup-alt-row__name">' +
                            geEsc(altName) +
                            "</p>"
                    );
                    if (altPrice) {
                        parts.push(
                            '<p class="ge-rec-alt-row__price ge-cup-alt-row__price">' +
                                geEsc(altPrice) +
                                "</p>"
                        );
                    }
                    parts.push("</div>");
                    parts.push(
                        '<button type="button" class="ge-rec-alt-row__add ge-cup-alt-row__add guest-hit" ' +
                            addAttr +
                            '="' +
                            geEsc(String(altMid)) +
                            '" data-item-name="' +
                            geEsc(altName) +
                            '">' +
                            geEsc(altAddLabel) +
                            "</button>"
                    );
                    parts.push("</li>");
                }
                parts.push("</ul></section>");
            }
            parts.push("</section>");
            if (config.footerHtml) parts.push(config.footerHtml);
            return parts.join("");
        }

        function geArmCupMomentDelays(rootEl) {
            if (!rootEl) return;
            var rm = geReducedMotion();
            var cardEl = rootEl.querySelector(".ge-rec-card--held");
            var kickerEl = rootEl.querySelector(".ge-cup-moment__kicker--held");
            var reasonEl =
                rootEl.querySelector(".ge-rec-card__reason--held") ||
                rootEl.querySelector(".ge-rec-reason--held");
            var nameEl = rootEl.querySelector(".ge-cup-moment__name--held");
            var visualEl = rootEl.querySelector(".ge-cup-moment__visual--held");
            var priceEl = rootEl.querySelector(".ge-cup-moment__price--held");
            var notesEl = rootEl.querySelector(".ge-cup-moment__notes--held");
            var cta = rootEl.querySelector(".ge-cup-moment__cta-wrap--held");
            var detailBtn = rootEl.querySelector(".ge-cup-moment__detail--held");
            var alts = rootEl.querySelector(".ge-cup-alts--held");
            var isSpecialty = !!rootEl.querySelector(".ge-cup-moment--specialty");
            function arm(el, ms) {
                if (!el) return;
                window.setTimeout(function () {
                    el.classList.add("is-ready");
                }, rm ? 0 : ms);
            }
            arm(cardEl, 0);
            if (isSpecialty) {
                arm(kickerEl, rm ? 0 : 200);
                arm(nameEl, rm ? 0 : 700);
                arm(notesEl, rm ? 0 : 1200);
                arm(visualEl, rm ? 0 : 1700);
                arm(priceEl, rm ? 0 : 2000);
                arm(cta, rm ? 200 : 2200);
                arm(detailBtn, rm ? 300 : 2600);
                return;
            }
            var badgeEl = rootEl.querySelector(".ge-rec-card__badge--held");
            arm(badgeEl, rm ? 0 : 80);
            arm(reasonEl, 0);
            arm(nameEl, GE_CUP_NAME_DELAY);
            arm(priceEl, GE_CUP_PRICE_DELAY);
            arm(notesEl, GE_CUP_PRICE_DELAY);
            arm(cta, rm ? 200 : GE_CUP_TRAY_DELAY);
            arm(alts, rm ? 400 : GE_CUP_ALTS_DELAY);
        }

        function geRitualApplyDepth(panel, depth) {
            if (!panel) return;
            var d = Math.max(0, Math.min(4, depth));
            for (var i = 0; i <= 4; i++) {
                panel.classList.remove("ge-panel--ritual-depth-" + i);
            }
            panel.classList.add("ge-panel--ritual-depth-" + d);
        }

        function sommFindQuestion(questionId) {
            var qs = catalog.questions || [];
            var qi;
            for (qi = 0; qi < qs.length; qi++) {
                if (qs[qi] && qs[qi].questionId === questionId) return qs[qi];
            }
            return null;
        }

        function sommActiveQuestions() {
            var entryId = (catalog && catalog.entryQuestionId) || "q0";
            var entry = sommFindQuestion(entryId);
            if (!sommBranchKey) {
                return entry ? [entry] : [];
            }
            var branchIds =
                (catalog && catalog.branches && catalog.branches[sommBranchKey]) || [];
            var list = entry ? [entry] : [];
            var bi;
            for (bi = 0; bi < branchIds.length; bi++) {
                var q = sommFindQuestion(branchIds[bi]);
                if (q) list.push(q);
            }
            return list;
        }

        function sommResolveBranchFromOption(optionId) {
            var opt = sommFindCatalogOption(optionId);
            if (opt && opt.branchKey) return String(opt.branchKey).trim();
            var id = String(optionId || "").toLowerCase();
            if (id === "q0_specialty") return "specialty";
            if (id === "q0_coffee") return "coffee";
            if (id === "q0_tea") return "tea";
            if (id === "q0_matcha") return "matcha";
            if (id === "q0_fruit") return "fruit";
            if (id === "q0_signature") return "signature";
            return "";
        }

        function sommAnswerLabels() {
            var labels = [];
            var qs = catalog.questions || [];
            var idToLabel = {};
            var qi;
            var oi;
            for (qi = 0; qi < qs.length; qi++) {
                var opts = (qs[qi] && qs[qi].options) || [];
                for (oi = 0; oi < opts.length; oi++) {
                    var opt = opts[oi];
                    if (opt && opt.optionId) {
                        idToLabel[opt.optionId] = opt.label || "";
                    }
                }
            }
            for (qi = 0; qi < answers.length; qi++) {
                var lab = idToLabel[answers[qi]];
                if (lab && String(lab).trim()) {
                    labels.push(String(lab).trim());
                }
            }
            return labels;
        }

        function sommIsSpecialtyCalibrationQuestionId(questionId) {
            var qid = String(questionId || "").toLowerCase();
            return qid.indexOf("q_sp_") === 0;
        }

        function sommFindCatalogOption(optionId) {
            if (!optionId) return null;
            var qs = catalog.questions || [];
            var qi;
            var oi;
            for (qi = 0; qi < qs.length; qi++) {
                var opts = (qs[qi] && qs[qi].options) || [];
                for (oi = 0; oi < opts.length; oi++) {
                    if (opts[oi] && opts[oi].optionId === optionId) return opts[oi];
                }
            }
            return null;
        }

        function sommReflectionI18nFallback(optionId) {
            if (String(optionId || "").indexOf("q_sp_") === 0) return "";
            if (String(optionId || "").indexOf("q0_") === 0) return "";
            var path = "ge.sommelier.tasting.reflection." + optionId;
            return geFlowT(path, "", "");
        }

        function sommResolveOptionReflection(optionId, catalogReflection) {
            var cms =
                catalogReflection != null && String(catalogReflection).trim()
                    ? String(catalogReflection).trim()
                    : "";
            if (cms) return cms;
            var i18n = sommReflectionI18nFallback(optionId);
            if (i18n) return i18n;
            return "";
        }

        function sommResolveAckReflection(optionId) {
            var path = "ge.ritual.ack." + optionId;
            var ack = geFlowT(path, "", "");
            if (ack) return ack;
            return geFlowT(
                "ge.host.eveningAck",
                "Hiểu rồi… cho mình một chút.",
                "Understood — give me a moment."
            );
        }

        function sommLatestNoteSoftLine() {
            if (!answers.length) return "";
            var lastOid = answers[answers.length - 1];
            if (String(lastOid || "").indexOf("q_sp_") === 0) return "";
            var qs = sommActiveQuestions();
            var lastQi = answers.length - 1;
            var q = qs[lastQi];
            if (q && sommIsSpecialtyCalibrationQuestionId(q.questionId)) {
                return "";
            }
            return sommResolveAckReflection(lastOid);
        }

        function sommNoteFieldLabel(questionId, index) {
            var qid = String(questionId || "").toLowerCase();
            if (qid === "q0") {
                return geFlowT(
                    "ge.sommelier.tasting.note.base",
                    "Hướng",
                    "Direction"
                );
            }
            if (qid.indexOf("q_sp_tried") === 0) {
                return geFlowT(
                    "ge.sommelier.tasting.note.mood",
                    "Trải nghiệm",
                    "Experience"
                );
            }
            if (qid.indexOf("q_sp_profile") === 0) {
                return geFlowT(
                    "ge.sommelier.tasting.note.flavor",
                    "Hồ sơ",
                    "Profile"
                );
            }
            if (qid.indexOf("q_sp_adventure") === 0) {
                return geFlowT(
                    "ge.sommelier.tasting.note.experience",
                    "Mạo hiểm",
                    "Adventure"
                );
            }
            if (qid.indexOf("q_cf_style") === 0 || qid.indexOf("q_ma_style") === 0) {
                return geFlowT(
                    "ge.sommelier.tasting.note.base",
                    "Kiểu ly",
                    "Cup style"
                );
            }
            if (qid.indexOf("q_cf_sweet") === 0 || qid.indexOf("q_ma_sweet") === 0) {
                return geFlowT(
                    "ge.sommelier.tasting.note.sweet",
                    "Độ ngọt",
                    "Sweetness"
                );
            }
            if (qid.indexOf("q_cf_temp") === 0 || qid.indexOf("q_ma_temp") === 0 || qid.indexOf("q_fr_cold") === 0) {
                return geFlowT(
                    "ge.sommelier.tasting.note.caffeine",
                    "Nhiệt độ",
                    "Temperature"
                );
            }
            if (qid.indexOf("q_te_feel") === 0 || qid.indexOf("q_fr_profile") === 0) {
                return geFlowT(
                    "ge.sommelier.tasting.note.flavor",
                    "Hướng vị",
                    "Flavor"
                );
            }
            if (qid.indexOf("q_te_moment") === 0) {
                return geFlowT(
                    "ge.sommelier.tasting.note.mood",
                    "Thời điểm",
                    "Moment"
                );
            }
            if (qid.indexOf("q_sg_intent") === 0) {
                return geFlowT(
                    "ge.sommelier.tasting.note.mood",
                    "Trải nghiệm",
                    "Experience"
                );
            }
            var fallbacksVi = ["Hướng", "Chi tiết", "Sở thích", "Kết"];
            var fallbacksEn = ["Direction", "Detail", "Preference", "Finish"];
            var fb = geGuestLangVi() ? fallbacksVi : fallbacksEn;
            return fb[index] || fb[0] || "";
        }

        function sommAnswerNoteRows() {
            var rows = [];
            var qs = sommActiveQuestions();
            var qi;
            for (qi = 0; qi < answers.length && qi < qs.length; qi++) {
                var q = qs[qi];
                if (sommIsSpecialtyCalibrationQuestionId(q && q.questionId)) continue;
                var oid = answers[qi];
                var value = "";
                var opts = (q && q.options) || [];
                var oi;
                for (oi = 0; oi < opts.length; oi++) {
                    if (opts[oi] && opts[oi].optionId === oid) {
                        value = opts[oi].label || "";
                        break;
                    }
                }
                if (!value || !String(value).trim()) continue;
                rows.push({
                    label: sommNoteFieldLabel(q && q.questionId, qi),
                    value: String(value).trim()
                });
            }
            return rows.slice(0, 4);
        }

        function sommSetWaitingQuietTray(on) {
            try {
                if (document.body) {
                    document.body.classList.toggle("somm-waiting-quiet-tray", !!on);
                }
            } catch (_wqt) {
                /* ignore */
            }
        }

        function sommClearNoteCardAnim() {
            if (sommNoteAnimTimer) {
                window.clearTimeout(sommNoteAnimTimer);
                sommNoteAnimTimer = null;
            }
        }

        function sommArmNoteCard(rootEl) {
            sommClearNoteCardAnim();
            if (!rootEl) return;
            var rows = rootEl.querySelectorAll(".ge-somm-note__row");
            var softLine = rootEl.querySelector(".ge-somm-note__line--soft");
            var ink = rootEl.querySelector(".ge-somm-note__ink span");
            var rm = geReducedMotion();
            var ri;

            rootEl.classList.add("is-writing");
            for (ri = 0; ri < rows.length; ri++) {
                rows[ri].classList.remove("is-visible", "is-active");
            }
            if (softLine) softLine.classList.remove("is-visible");
            if (ink) ink.style.width = "0%";

            function finishInk() {
                if (ink) ink.style.width = "100%";
            }

            function revealSoftLine() {
                if (softLine) softLine.classList.add("is-visible");
                finishInk();
            }

            if (!rows.length) {
                revealSoftLine();
                return;
            }

            if (rm) {
                for (ri = 0; ri < rows.length; ri++) {
                    rows[ri].classList.add("is-visible");
                }
                rows[rows.length - 1].classList.add("is-active");
                revealSoftLine();
                return;
            }

            var step = 0;
            function revealNext() {
                if (step > 0) {
                    rows[step - 1].classList.remove("is-active");
                }
                if (step >= rows.length) {
                    if (softLine) {
                        softLine.classList.add("is-visible");
                        sommNoteAnimTimer = window.setTimeout(finishInk, SOMM_NOTE_ROW_MS);
                    } else {
                        finishInk();
                    }
                    return;
                }
                rows[step].classList.add("is-visible", "is-active");
                if (ink && rows.length > 0) {
                    ink.style.width =
                        String(
                            Math.round(((step + 1) / rows.length) * 88)
                        ) + "%";
                }
                step += 1;
                sommNoteAnimTimer = window.setTimeout(revealNext, SOMM_NOTE_ROW_MS);
            }

            sommNoteAnimTimer = window.setTimeout(revealNext, 120);
        }

        function sommBuildNoteCardHtml() {
            var hideTitle = sommSpecialtyPath;
            var noteTitle = geFlowT(
                "ge.sommelier.tasting.note.title",
                "Đang viết lá thư vị giác của bạn",
                "Writing your tasting letter"
            );
            var writingLine = geFlowT(
                "ge.sommelier.tasting.note.writing",
                "Annap đang chọn một món hợp với ghi chú này.",
                "Annap is choosing a cup to match this note."
            );
            var noteRows = sommAnswerNoteRows();
            var softLine = sommLatestNoteSoftLine();
            var parts = [];
            parts.push(
                '<div class="ge-somm-note" role="status" aria-live="polite">'
            );
            if (!hideTitle) {
                parts.push(
                    '<p class="ge-somm-note__title">' + geEsc(noteTitle) + "</p>"
                );
            }
            parts.push('<div class="ge-somm-note__card">');
            if (noteRows.length > 0) {
                parts.push('<ul class="ge-somm-note__rows">');
                for (var ni = 0; ni < noteRows.length; ni++) {
                    var row = noteRows[ni];
                    parts.push(
                        '<li class="ge-somm-note__row" style="--note-i:' +
                            ni +
                            '">'
                    );
                    parts.push(
                        '<span class="ge-somm-note__field">' +
                            geEsc(row.label) +
                            "</span>"
                    );
                    parts.push(
                        '<span class="ge-somm-note__rule" aria-hidden="true"></span>'
                    );
                    parts.push(
                        '<span class="ge-somm-note__value">' +
                            geEsc(row.value) +
                            "</span>"
                    );
                    parts.push(
                        '<span class="ge-somm-note__cursor" aria-hidden="true"></span>'
                    );
                    parts.push("</li>");
                }
                parts.push("</ul>");
            }
            if (softLine) {
                parts.push(
                    '<p class="ge-somm-note__line ge-somm-note__line--soft">' +
                        geEsc(softLine) +
                        "</p>"
                );
            } else if (!noteRows.length) {
                parts.push(
                    '<p class="ge-somm-note__line">' + geEsc(writingLine) + "</p>"
                );
            }
            parts.push(
                '<div class="ge-somm-note__ink" aria-hidden="true"><span></span></div>'
            );
            parts.push("</div></div>");
            return parts.join("");
        }

        function sommMountNoteCard() {
            if (!sommStepHost) return;
            sommSetWaitingQuietTray(true);
            sommStepHost.innerHTML = sommBuildNoteCardHtml();
            sommArmNoteCard(sommStepHost.querySelector(".ge-somm-note"));
        }

        function sommBuildErrorHtml(message) {
            var errMsg =
                message ||
                geHouseT(
                    "ge.sommelier.tasting.error",
                    "Quán chưa gọi tên được lúc này.",
                    "The house could not name an origin just now."
                );
            var retryLabel = geFlowT(
                "ge.sommelier.tasting.retry",
                "Thử lại",
                "Try again"
            );
            var menuLabel = geHouseT("ge.arrival.secondaryMenu", "Xem thực đơn", "Browse menu");
            var menuHref = "/Menu/Index" + geVtQuery(root);
            var parts = [];
            parts.push(
                '<div class="ge-somm-tasting ge-somm-tasting--error" role="alert">'
            );
            parts.push('<div class="ge-somm-tasting__card">');
            parts.push(
                '<p class="ge-somm-tasting__error-msg">' + geEsc(errMsg) + "</p>"
            );
            parts.push('<div class="ge-somm-tasting__actions">');
            parts.push(
                '<button type="button" class="ge-btn-solid guest-hit ge-somm-tasting__retry">' +
                    geEsc(retryLabel) +
                    "</button>"
            );
            parts.push(
                '<a class="ge-btn-ghost guest-hit ge-somm-tasting__menu" href="' +
                    geEsc(menuHref) +
                    '">' +
                    geEsc(menuLabel) +
                    "</a>"
            );
            parts.push("</div></div></div>");
            return parts.join("");
        }

        function sommMountErrorHtml(message) {
            if (!sommStepHost) return;
            sommStepHost.innerHTML = sommBuildErrorHtml(message);
            var retryBtn = sommStepHost.querySelector(".ge-somm-tasting__retry");
            if (retryBtn) {
                retryBtn.addEventListener("click", function (ev) {
                    ev.preventDefault();
                    sommRetryRecommendation();
                });
            }
        }

        function sommRetryRecommendation() {
            if (!sommStepHost || !answers.length) return;
            geShow(sommResults, false);
            if (sommSpecialtyPath) {
                sommRecognitionReadyAt = Date.now() + sommRecognitionWaitMs();
                if (panelSomm) {
                    panelSomm.classList.add("ge-panel--sommelier-revealing");
                    panelSomm.classList.add("ge-panel--somm-waiting");
                    panelSomm.classList.add("ge-panel--somm-recognizing");
                }
                sommMountRecognitionTurn();
            } else {
                if (panelSomm) {
                    panelSomm.classList.add("ge-panel--sommelier-revealing");
                    panelSomm.classList.add("ge-panel--somm-waiting");
                }
                if (!sommStepHost.querySelector(".ge-somm-note")) {
                    sommMountNoteCard();
                }
            }
            sommFetchResults();
        }

        function sommFocusFirstChoice(scope) {
            if (!scope || !scope.querySelector) return;
            var el = scope.querySelector(
                ".ge-calibration-card:not([disabled]), .ge-host-choice[data-ge-opt]:not([disabled]), .ge-opt[data-ge-opt]:not([disabled])"
            );
            if (!el || typeof el.focus !== "function") return;
            window.requestAnimationFrame(function () {
                try {
                    el.focus({ preventScroll: true });
                } catch (_sf) {
                    try {
                        el.focus();
                    } catch (_sf2) {
                        /* ignore */
                    }
                }
            });
        }

        function sommShowAcknowledgment(optionId, chosenBtn, completedStep) {
            if (!sommStepHost) return;
            var qs = sommActiveQuestions();
            var completedQ = qs[completedStep];
            var skipNote =
                sommSpecialtyPath &&
                completedQ &&
                sommIsSpecialtyCalibrationQuestionId(completedQ.questionId);
            var turn = sommStepHost.querySelector(
                ".ge-host-turn, .ge-calibration-turn"
            );
            if (turn && chosenBtn) {
                chosenBtn.classList.add("is-chosen");
                turn.classList.add("ge-host-turn--settling");
                if (turn.classList.contains("ge-calibration-turn")) {
                    turn.classList.add("ge-calibration-turn--settling");
                }
                var choices = turn.querySelectorAll(
                    ".ge-host-choice, .ge-calibration-card"
                );
                for (var ci = 0; ci < choices.length; ci++) {
                    if (choices[ci] !== chosenBtn) {
                        choices[ci].classList.add("is-muted");
                        choices[ci].disabled = true;
                    }
                }
            }
            var mountDelay = geReducedMotion() ? 0 : skipNote ? 0 : 150;
            window.setTimeout(function () {
                if (!skipNote) {
                    sommMountNoteCard();
                }
                if (panelSomm) {
                    panelSomm.classList.add("ge-panel--ritual-settling");
                    if (!skipNote) {
                        panelSomm.classList.add("ge-panel--somm-waiting");
                    }
                    geRitualApplyDepth(panelSomm, completedStep + 1);
                }
            }, mountDelay);
        }

        function sommBuildRecognitionHtml() {
            var line = geFlowT(
                "ge.sommelier.tasting.recognition.beat1",
                "Một chút.",
                "One moment."
            );
            return (
                '<div class="ge-recognition-turn" role="status" aria-live="polite">' +
                '<p class="ge-recognition-turn__line is-active">' +
                geEsc(line) +
                "</p>" +
                "</div>"
            );
        }

        function sommArmRecognitionBeats(rootEl) {
            if (!rootEl) return;
            var line = rootEl.querySelector(".ge-recognition-turn__line");
            if (line) line.classList.add("is-active");
        }

        function sommMountRecognitionTurn() {
            if (!sommStepHost) return;
            sommSetWaitingQuietTray(true);
            sommStepHost.innerHTML = sommBuildRecognitionHtml();
            sommArmRecognitionBeats(
                sommStepHost.querySelector(".ge-recognition-turn")
            );
        }

        function sommRecognitionWaitMs() {
            return geReducedMotion() ? 0 : SOMM_RECOGNITION_MS;
        }

        function sommScheduleAfterRecognition(fn) {
            if (!sommRecognitionReadyAt) {
                fn();
                return;
            }
            var wait = Math.max(0, sommRecognitionReadyAt - Date.now());
            if (wait <= 0) {
                fn();
                return;
            }
            window.setTimeout(fn, wait);
        }

        /* —— Sommelier (hosted conversation ritual) —— */
        function sommBeginReveal() {
            if (!sommStepHost || !sommResults) return;
            sommSetCalibrationPanel(false);
            if (panelSomm) {
                panelSomm.classList.add("ge-panel--sommelier-revealing");
                panelSomm.classList.add("ge-panel--somm-waiting");
                geRitualApplyDepth(panelSomm, 4);
            }
            if (sommSpecialtyPath) {
                sommRecognitionReadyAt = Date.now() + sommRecognitionWaitMs();
                if (panelSomm) {
                    panelSomm.classList.add("ge-panel--somm-recognizing");
                }
                sommMountRecognitionTurn();
                sommFetchResults();
                return;
            }
            sommRecognitionReadyAt = 0;
            sommMountNoteCard();
            window.setTimeout(sommFetchResults, geReducedMotion() ? 0 : 2200);
        }

        function sommClearEntryTimer() {
            if (sommEntryTimer) {
                window.clearTimeout(sommEntryTimer);
                sommEntryTimer = null;
            }
            if (panelSomm) panelSomm.classList.remove("ge-panel--somm-entry");
        }

        function sommShowHostThreshold() {
            if (!sommStepHost) return;
            sommClearEntryTimer();
            sommClearNoteCardAnim();
            if (sommSettleTimer) {
                window.clearTimeout(sommSettleTimer);
                sommSettleTimer = null;
            }
            sommSetWaitingQuietTray(true);
            if (panelSomm) {
                panelSomm.classList.add("ge-panel--somm-entry");
                panelSomm.classList.remove(
                    "ge-panel--ritual-settling",
                    "ge-panel--somm-waiting",
                    "ge-panel--sommelier-revealing"
                );
                geRitualApplyDepth(panelSomm, 0);
            }
            var greet = geHostGreetingLine();
            var invite = geFlowT(
                "ge.sommelier.entry.invite",
                "Chúng tôi sẽ giữ tối nay không vội.",
                "We'll keep tonight unhurried."
            );
            sommStepHost.innerHTML =
                '<div class="ge-host-turn ge-host-turn--threshold" role="status" aria-live="polite">' +
                '<p class="ge-host-prompt ge-host-threshold__greet">' +
                geEsc(greet) +
                "</p>" +
                '<p class="ge-host-whisper ge-host-threshold__invite">' +
                geEsc(invite) +
                "</p>" +
                "</div>";
            var hold = geReducedMotion() ? 450 : SOMM_ENTRY_HOLD_MS;
            sommEntryTimer = window.setTimeout(function () {
                sommEntryTimer = null;
                if (panelSomm) panelSomm.classList.remove("ge-panel--somm-entry");
                sommSetWaitingQuietTray(false);
                sommRender();
            }, hold);
        }

        function sommBeginEntry() {
            sommShowHostThreshold();
        }

        function sommSetCalibrationPanel(on) {
            if (!panelSomm) return;
            panelSomm.classList.toggle("ge-panel--somm-calibration", !!on);
        }

        function sommFlavorBranchKey(optionId) {
            var oid = String(optionId || "");
            if (
                oid === "q_sp_profile_floral" ||
                oid === "q_sp_profile_fruit" ||
                oid === "q_sc_flavor_floral" ||
                oid === "q_sc_flavor_fruit"
            ) {
                return "rwanda";
            }
            if (
                oid === "q_sp_profile_chocolate" ||
                oid === "q_sp_profile_surprise" ||
                oid === "q_sc_flavor_wine" ||
                oid === "q_sc_flavor_blueberry"
            ) {
                return "natural";
            }
            return "";
        }

        function sommExperienceInBranch(optionId, branch) {
            return true;
        }

        function sommCalibrationCardCopy(optionId) {
            var base = "ge.sommelier.tasting.calibration.cards." + optionId;
            var fallbacks = {
                q_sp_profile_floral: [
                    "Hoa nhài & cam quýt",
                    ["Hoa nhài", "Cam quýt"],
                    "Floral",
                    ["Jasmine", "Citrus"]
                ],
                q_sp_profile_fruit: [
                    "Trái mọng",
                    ["Đào", "Sốt chanh"],
                    "Stone Fruit",
                    ["Peach", "Lemon curd"]
                ],
                q_sp_profile_chocolate: [
                    "Cacao & trái đen",
                    ["Cacao", "Trái đen"],
                    "Cocoa & Dark Fruit",
                    ["Cocoa", "Dark fruit"]
                ],
                q_sp_profile_surprise: [
                    "Quả mọng & mật ong",
                    ["Việt quất", "Mật ong"],
                    "Berry & Honey",
                    ["Blueberry", "Honey"]
                ],
                q_sp_adventure_safe: [
                    "Thanh như trà",
                    [],
                    "Tea-like",
                    []
                ],
                q_sp_adventure_balanced: [
                    "Tròn đều",
                    [],
                    "Round",
                    []
                ],
                q_sp_adventure_experimental: [
                    "Mở dần",
                    [],
                    "Layered",
                    []
                ],
                q_sc_flavor_floral: [
                    "Hoa nhài & cam quýt",
                    ["Hoa nhài", "Cam quýt"],
                    "Floral",
                    ["Jasmine", "Citrus"]
                ],
                q_sc_flavor_fruit: [
                    "Trái mọng",
                    ["Đào", "Sốt chanh"],
                    "Stone Fruit",
                    ["Peach", "Lemon curd"]
                ],
                q_sc_flavor_wine: [
                    "Cacao & trái đen",
                    ["Cacao", "Trái đen"],
                    "Cocoa & Dark Fruit",
                    ["Cocoa", "Dark fruit"]
                ],
                q_sc_flavor_blueberry: [
                    "Quả mọng & mật ong",
                    ["Việt quất", "Mật ong"],
                    "Berry & Honey",
                    ["Blueberry", "Honey"]
                ],
                q_sc_experience_soft: [
                    "Thanh như trà",
                    [],
                    "Tea-like",
                    []
                ],
                q_sc_experience_balanced: [
                    "Tròn đều",
                    [],
                    "Round",
                    []
                ],
                q_sc_experience_complex: [
                    "Đậm đặc",
                    [],
                    "Syrupy",
                    []
                ],
                q_sc_experience_surprising: [
                    "Mọng nước",
                    [],
                    "Juicy",
                    []
                ]
            };
            var fb = fallbacks[optionId];
            var title = geFlowT(
                base + ".title",
                fb ? fb[0] : "",
                fb ? fb[2] : ""
            );
            var notes = [];
            var ni;
            var viNotes = fb && fb[1] ? fb[1] : [];
            var enNotes = fb && fb[3] ? fb[3] : [];
            var noteCount = Math.max(viNotes.length, enNotes.length);
            for (ni = 1; ni <= noteCount; ni++) {
                var note = geFlowT(
                    base + ".note" + ni,
                    viNotes[ni - 1] || "",
                    enNotes[ni - 1] || ""
                );
                if (note && String(note).trim()) {
                    notes.push(String(note).trim());
                }
            }
            return { title: title, notes: notes };
        }

        function sommRenderCalibration(q, depthIdx) {
            if (!sommStepHost || !q) return;
            var isFlavor =
                q.questionId === "q_sp_profile" || q.questionId === "q_sc_flavor";
            var question = geFlowT(
                isFlavor
                    ? "ge.sommelier.tasting.calibration.flavor.question"
                    : "ge.sommelier.tasting.calibration.feel.question",
                isFlavor
                    ? "Hồ sơ nào nghe thú vị nhất với bạn?"
                    : "Bạn muốn mạo hiểm đến mức nào?",
                isFlavor
                    ? "Which profile sounds most interesting?"
                    : "How adventurous are you?"
            );
            var parts = [];
            parts.push(
                '<div class="ge-calibration-turn ge-host-turn ge-host-turn--calibration ge-host-turn--depth-' +
                    depthIdx +
                    '" role="group" aria-labelledby="ge-calibration-heading">'
            );
            if (question) {
                parts.push(
                    '<p class="ge-calibration-turn__host" id="ge-calibration-heading">' +
                        geEsc(question) +
                        "</p>"
                );
            }
            parts.push(
                '<div class="ge-calibration-grid' +
                    (isFlavor ? "" : " ge-calibration-grid--pair") +
                    '">'
            );
            var opts = q.options || [];
            var j;
            for (j = 0; j < opts.length; j++) {
                var o = opts[j];
                var oid = o && o.optionId ? o.optionId : "";
                var copy = sommCalibrationCardCopy(oid);
                var ariaParts = [copy.title || (o && o.label) || ""].concat(copy.notes);
                var ariaLabel = ariaParts.filter(Boolean).join(". ");
                parts.push(
                    '<button type="button" class="ge-calibration-card ge-host-choice guest-hit" data-ge-opt="' +
                        geEsc(oid) +
                        '" aria-label="' +
                        geEsc(ariaLabel) +
                        '">'
                );
                if (copy.title) {
                    parts.push(
                        '<span class="ge-calibration-card__title">' +
                            geEsc(copy.title) +
                            "</span>"
                    );
                } else if (o && o.label) {
                    parts.push(
                        '<span class="ge-calibration-card__title">' +
                            geEsc(o.label) +
                            "</span>"
                    );
                }
                var nk;
                for (nk = 0; nk < copy.notes.length; nk++) {
                    parts.push(
                        '<span class="ge-calibration-card__note">' +
                            geEsc(copy.notes[nk]) +
                            "</span>"
                    );
                }
                parts.push("</button>");
            }
            parts.push("</div></div>");
            sommStepHost.innerHTML = parts.join("");
            sommFocusFirstChoice(sommStepHost);
        }

        function sommRender() {
            if (!sommStepHost) return;
            sommClearNoteCardAnim();
            sommSetWaitingQuietTray(false);
            if (panelSomm) {
                panelSomm.classList.remove("ge-panel--ritual-settling");
                panelSomm.classList.remove("ge-panel--somm-waiting");
            }
            var qs = sommActiveQuestions();
            if (stepIdx >= qs.length) {
                sommSetCalibrationPanel(false);
                sommBeginReveal();
                return;
            }
            geRitualApplyDepth(panelSomm, stepIdx);
            var q = qs[stepIdx];
            if (
                sommSpecialtyPath &&
                q &&
                (q.questionId === "q_sp_profile" ||
                    q.questionId === "q_sp_adventure" ||
                    q.questionId === "q_sc_flavor" ||
                    q.questionId === "q_sc_experience")
            ) {
                sommSetCalibrationPanel(true);
                sommRenderCalibration(q, stepIdx);
                geFocusFlowPanel(panelSomm);
                return;
            }
            sommSetCalibrationPanel(false);
            var stepNum = stepIdx + 1;
            var totalQ = qs.length;
            var html = [];
            html.push(
                '<div class="ge-host-turn ge-host-turn--dialog ge-host-turn--depth-' +
                    stepIdx +
                    '">'
            );
            html.push('<p class="ge-somm-progress">');
            html.push(
                '<span class="ge-somm-progress__step">' +
                    geEsc(
                        geFlowTf(
                            "ge.sommelier.question.step",
                            { n: stepNum, total: totalQ },
                            "Bước " + stepNum + " / " + totalQ,
                            "Step " + stepNum + " / " + totalQ
                        )
                    ) +
                    "</span>"
            );
            html.push("</p>");
            html.push('<p class="ge-host-prompt">' + geEsc(q && q.prompt) + "</p>");
            html.push('<div class="ge-host-choices">');
            var opts = (q && q.options) || [];
            for (var j = 0; j < opts.length; j++) {
                var o = opts[j];
                var oid = o && o.optionId ? o.optionId : "";
                var olab = o && o.label ? o.label : "";
                html.push(
                    '<button type="button" class="ge-opt ge-host-choice guest-hit" data-ge-opt="' +
                        geEsc(oid) +
                        '">' +
                        geEsc(olab) +
                        "</button>"
                );
            }
            html.push("</div></div>");
            sommStepHost.innerHTML = html.join("");
            geFocusFlowPanel(panelSomm);
            sommFocusFirstChoice(sommStepHost);
        }

        if (sommStepHost) {
            sommStepHost.addEventListener("click", function (ev) {
                var b = ev.target && ev.target.closest ? ev.target.closest("[data-ge-opt]") : null;
                if (!b || b.disabled) return;
                ev.preventDefault();
                var oid = b.getAttribute("data-ge-opt");
                if (!oid) return;
                var completedStep = stepIdx;
                if (oid) answers.push(oid);
                var branchFromOpt = sommResolveBranchFromOption(oid);
                if (branchFromOpt) {
                    sommBranchKey = branchFromOpt;
                    sommSpecialtyPath = branchFromOpt === "specialty";
                }
                if (oid === SOMM_ENTRY_SPECIALTY_ID || oid === SOMM_COFFEE_OPTION_ID) {
                    sommSpecialtyPath = true;
                    sommBranchKey = sommBranchKey || "specialty";
                }
                var flavorBranch = sommFlavorBranchKey(oid);
                if (flavorBranch) {
                    sommCalibrationBranch = flavorBranch;
                }
                stepIdx++;
                sommShowAcknowledgment(oid, b, completedStep);
                if (sommSettleTimer) window.clearTimeout(sommSettleTimer);
                var completedQ = sommActiveQuestions()[completedStep];
                var tastingAck =
                    sommSpecialtyPath &&
                    completedQ &&
                    sommIsSpecialtyCalibrationQuestionId(completedQ.questionId);
                var delay = geReducedMotion()
                    ? tastingAck
                        ? 420
                        : 380
                    : tastingAck
                      ? SOMM_TASTING_SETTLE_MS
                      : SOMM_SETTLE_MS[completedStep] || RITUAL_SETTLE_MS;
                sommSettleTimer = window.setTimeout(function () {
                    sommSettleTimer = null;
                    if (stepIdx >= sommActiveQuestions().length) {
                        sommBeginReveal();
                        return;
                    }
                    sommRender();
                }, delay);
            });
        }

        function sommFetchResults() {
            if (!sommStepHost || !sommResults) return;
            var specialtyReveal = sommSpecialtyPath;
            if (!navigator.onLine) {
                sommClearNoteCardAnim();
                sommSetWaitingQuietTray(false);
                if (panelSomm) {
                    panelSomm.classList.remove("ge-panel--sommelier-revealing");
                    panelSomm.classList.remove("ge-panel--somm-waiting");
                    panelSomm.classList.remove("ge-panel--somm-recognizing");
                }
                sommMountErrorHtml(
                    geHouseT(
                        "ge.sommelier.tasting.offline",
                        "Quán chưa gọi tên được lúc này.",
                        "The house could not name an origin just now."
                    )
                );
                return;
            }
            if (!specialtyReveal) {
                if (!sommStepHost.querySelector(".ge-somm-note")) {
                    sommMountNoteCard();
                } else {
                    sommArmNoteCard(sommStepHost.querySelector(".ge-somm-note"));
                }
            }
            var ac = new AbortController();
            var to = window.setTimeout(function () {
                ac.abort();
            }, 20000);
            fetch(typeof window.__annapApiUrl === "function" ? window.__annapApiUrl("/api/guest/guided-sommelier/recommend") : "/api/guest/guided-sommelier/recommend", {
                method: "POST",
                headers: { "Content-Type": "application/json", Accept: "application/json" },
                body: JSON.stringify({ optionIds: answers }),
                signal: ac.signal,
                cache: "no-store"
            })
                .then(function (r) {
                    return r.json().then(function (j) {
                        return { ok: r.ok, j: j };
                    });
                })
                .then(function (pack) {
                    window.clearTimeout(to);
                    if (!pack.ok) {
                        sommClearNoteCardAnim();
                        sommSetWaitingQuietTray(false);
                        if (panelSomm) {
                            panelSomm.classList.remove("ge-panel--sommelier-revealing");
                            panelSomm.classList.remove("ge-panel--somm-waiting");
                            panelSomm.classList.remove("ge-panel--somm-recognizing");
                        }
                        sommMountErrorHtml();
                        return;
                    }
                    sommScheduleAfterRecognition(function () {
                        sommClearNoteCardAnim();
                        sommSetWaitingQuietTray(false);
                        sommStepHost.innerHTML = "";
                        if (panelSomm) {
                            panelSomm.classList.remove("ge-panel--somm-waiting");
                            panelSomm.classList.remove("ge-panel--somm-recognizing");
                        }
                        var reflection = pack.j && pack.j.personalityReflection ? pack.j.personalityReflection : "";
                        var res = (pack.j && pack.j.results) || [];
                        var isSpecialtyResult =
                            sommSpecialtyPath ||
                            !!(pack.j && pack.j.isSpecialtyCoffee);
                        var parts = [];
                        if (res.length > 0) {
                            var lead = res[0];
                            var leadExplain =
                                lead.emotionalExplanation ||
                                lead.EmotionalExplanation ||
                                "";
                            var reasonText = isSpecialtyResult
                                ? ""
                                : leadExplain ||
                                  reflection ||
                                  geHouseT(
                                      "ge.host.serveLine",
                                      "Annap chọn cho bạn.",
                                      "Annap chooses for you."
                                  );
                            parts.push(
                                geBuildCupMomentHtml({
                                    hero: res[0],
                                    alts: isSpecialtyResult ? [] : res.slice(1, 3),
                                    addAttr: "data-ge-add",
                                    reasonText: reasonText,
                                    mode: isSpecialtyResult ? "specialty" : "default",
                                    showRecBadge: false,
                                    ctaLabel: isSpecialtyResult
                                        ? geFlowT(
                                              "ge.host.prepareForTable",
                                              "Pha cho bàn tôi",
                                              "Prepare for my table"
                                          )
                                        : geFlowT(
                                              "ge.host.trayInvite",
                                              "Thêm vào khay",
                                              "Add to tray"
                                          )
                                })
                            );
                        } else {
                            parts.push(
                                '<p class="ge-muted">' +
                                    geEsc(
                                        geHouseT(
                                            "ge.cupMoment.empty",
                                            "Quán chưa thể gọi tên một ly lúc này.",
                                            "The house could not name a cup just now."
                                        )
                                    ) +
                                    "</p>"
                            );
                        }
                        sommResults.innerHTML = parts.join("");
                        sommResults.classList.add("ge-sommelier-results--cup-moment");
                        geArmCupMomentDelays(sommResults);
                        var rb = document.createElement("button");
                        rb.type = "button";
                        rb.className = "ge-restart ge-restart--sommelier guest-hit";
                        rb.textContent = geFlowT("ge.sommelier.beginAgain", "Viết lại gu", "Rewrite my taste");
                        rb.addEventListener("click", function () {
                            answers = [];
                            stepIdx = 0;
                            sommBranchKey = "";
                            sommSpecialtyPath = false;
                            sommCalibrationBranch = "";
                            sommRecognitionReadyAt = 0;
                            if (panelSomm) panelSomm.classList.remove("ge-panel--sommelier-revealing");
                            if (sommResults) sommResults.classList.remove("ge-sommelier-results--cup-moment");
                            geShow(sommResults, false);
                            sommBeginEntry();
                        });
                        sommResults.appendChild(rb);
                        geShow(sommResults, true);
                        if (panelSomm) panelSomm.classList.remove("ge-panel--sommelier-revealing");
                    });
                })
                .catch(function () {
                    window.clearTimeout(to);
                    sommClearNoteCardAnim();
                    sommSetWaitingQuietTray(false);
                    if (panelSomm) {
                        panelSomm.classList.remove("ge-panel--sommelier-revealing");
                        panelSomm.classList.remove("ge-panel--somm-waiting");
                        panelSomm.classList.remove("ge-panel--somm-recognizing");
                    }
                    sommMountErrorHtml();
                });
        }

        /* —— Discovery — Letter Room (three envelopes, no taste quiz) —— */
        function geLetterRoomMerge() {
            var b = discLetterRoomBoot || {};
            var a = discLastLetterRoom || {};
            function p(k) {
                var v = a[k];
                if (v != null && String(v).trim()) return String(v).trim();
                v = b[k];
                return v != null && String(v).trim() ? String(v).trim() : "";
            }
            var envA = Array.isArray(a.envelopes) ? a.envelopes : [];
            var envB = Array.isArray(b.envelopes) ? b.envelopes : [];
            var envelopes = [];
            for (var e = 0; e < 3; e++) {
                var ea = envA[e] || {};
                var eb = envB[e] || {};
                var lab = ea.label && String(ea.label).trim() ? String(ea.label).trim() : String(eb.label || "");
                var hin = ea.hint && String(ea.hint).trim() ? String(ea.hint).trim() : String(eb.hint || "");
                var tex = ea.texture && String(ea.texture).trim() ? String(ea.texture).trim().toLowerCase() : String(eb.texture || "kraft").toLowerCase();
                if (tex !== "kraft" && tex !== "cream" && tex !== "ink") tex = "kraft";
                envelopes.push({ label: lab, hint: hin, texture: tex });
            }
            return {
                title: p("title"),
                subtitle: p("subtitle"),
                deskHint: p("deskHint"),
                envelopes: envelopes,
                ctaPrimary:
                    p("ctaPrimary") ||
                    geHouseT("letterRoom.ctaPrimary", "Thêm vào khay", "Add to tray"),
                rerollCta:
                    p("rerollCta") ||
                    geHouseT("letterRoom.rerollCta", "Nhờ vịt giao thêm một thư", "Another delivery"),
                earnedKicker:
                    p("earnedKicker") ||
                    geHouseT("letterRoom.earnedKicker", "Quán gọi tên ly này cho bạn.", "The house names this cup for you."),
                paperTheme: p("paperTheme") || "desk"
            };
        }

        function geEveningGestureRender() {
            var host = geById("ge-evening-gesture-choices");
            var wrap = geById("ge-evening-gesture");
            if (!host) return;
            if (wrap) wrap.classList.remove("ge-evening-gesture--settling");
            var hintReset = geById("ge-letter-desk-hint");
            if (hintReset) hintReset.className = "ge-letter-desk__hint";
            var html = [];
            for (var gi = 0; gi < GE_EVENING_OPTIONS.length; gi++) {
                var opt = GE_EVENING_OPTIONS[gi];
                html.push(
                    '<button type="button" class="ge-opt ge-evening-choice guest-hit" data-ge-evening-signal="' +
                        geEsc(opt.signal) +
                        '">' +
                        geEsc(geRitualT(opt.labelKey, opt.fallback)) +
                        "</button>"
                );
            }
            host.innerHTML = html.join("");
            if (wrap) geShow(wrap, true);
        }

        function geLetterDeskPaint() {
            if (!discLetterDesk) return;
            var lr = geLetterRoomMerge();
            var kicker = geById("ge-letter-desk-kicker");
            var title = geById("ge-letter-desk-title");
            var sub = geById("ge-letter-desk-subtitle");
            var hint = geById("ge-letter-desk-hint");
            if (kicker) {
                kicker.textContent =
                    (lr.title && String(lr.title).trim()) || geHostGreetingLine();
            }
            if (title) {
                title.textContent =
                    (lr.subtitle && String(lr.subtitle).trim()) || geHostRoomLine();
            }
            if (sub) {
                sub.textContent = geHouseT(
                    "ge.host.deskSub",
                    "106/1 Nguyễn Thị Minh Khai — đọc phòng trước khi rót.",
                    "106/1 Nguyễn Thị Minh Khai — we'll read the room before we pour."
                );
            }
            if (hint) {
                hint.textContent =
                    (lr.deskHint && String(lr.deskHint).trim()) ||
                    geRitualT(
                        "ge.host.eveningPrompt",
                        "Tối nay nên cảm thấy thế nào?"
                    );
            }
            geEveningGestureRender();
            discLetterDesk.setAttribute("data-ge-paper", lr.paperTheme || "desk");
        }

        function gePaintDuckBlocked() {
            if (!discDuckBlocked) return;
            var titleEl = discDuckBlocked.querySelector("#ge-duck-blocked-title");
            var bodyEl = discDuckBlocked.querySelector(".ge-duck-blocked__body");
            if (titleEl) {
                titleEl.textContent = geHouseT(
                    "ge.host.rerollBlockedTitle",
                    "Tôi đã cân nhắc đủ cho tối nay.",
                    "I've considered enough for tonight."
                );
            }
            if (bodyEl) {
                bodyEl.textContent = geHouseT(
                    "ge.host.rerollBlockedBody",
                    "Tôi sẽ giữ ly cuối tôi đã gọi tên cho bàn của bạn.",
                    "I'll stand by the last cup I named for your table."
                );
            }
            var owner = discDuckBlocked.querySelector(".ge-duck-blocked__owner");
            if (owner) {
                owner.textContent = geHouseT("ge.host.rerollBlockedOwner", "Buổi tối giữ nhịp của nó.", "The evening keeps its line.");
            }
            var hintEl = discDuckBlocked.querySelector("#ge-duck-blocked-hint");
            if (hintEl) {
                hintEl.textContent = geHouseT(
                    "ge.duckPost.blockedDismissHint",
                    "Vuốt xuống một lần trên thẻ, chạm vùng tối bên ngoài, hoặc bấm Escape.",
                    "Swipe down on this card once, tap the dimmed edge, or press Escape."
                );
            }
        }

        function geLetterDeskResetVisual() {
            /* evening gesture only — envelope UI removed */
        }

        function geDiscoveryFetchPayload() {
            var vtStr = root.getAttribute("data-vt") || "";
            var vtGuid = null;
            if (vtStr && /^[0-9a-fA-F-]{36}$/.test(vtStr)) vtGuid = vtStr;
            discAbort = typeof AbortController !== "undefined" ? new AbortController() : null;
            var signal = discAbort && discAbort.signal ? discAbort.signal : undefined;
            var url =
                typeof window.__annapApiUrl === "function"
                    ? window.__annapApiUrl("/api/guest/discovery/reveal")
                    : "/api/guest/discovery/reveal";
            var bodyObj = {
                venueTableId: vtGuid,
                rollNonce: discNonce,
                tasteSignals: discTasteSignals.slice(),
                chosenEnvelopeIndex:
                    discChosenEnvelopeIx != null && isFinite(discChosenEnvelopeIx) ? discChosenEnvelopeIx : 0
            };
            var fetchCore = fetch(url, {
                method: "POST",
                headers: { "Content-Type": "application/json", Accept: "application/json" },
                body: JSON.stringify(bodyObj),
                signal: signal,
                cache: "no-store"
            }).then(function (r) {
                return r.text().then(function (raw) {
                    var j = {};
                    try {
                        j = raw && String(raw).trim() ? JSON.parse(raw) : {};
                    } catch (eParse) {
                        j = { error: "We could not read the house reply." };
                    }
                    return { ok: r.ok, j: j };
                });
            });
            var timeoutMs = 20000;
            return new Promise(function (resolve, reject) {
                var settled = false;
                var tid = window.setTimeout(function () {
                    if (settled) return;
                    settled = true;
                    if (discAbort) {
                        try {
                            discAbort.abort();
                        } catch (_ab) {}
                    }
                    resolve({ ok: false, j: { error: "The house is taking too long to answer — try again." } });
                }, timeoutMs);
                geDiscPushTimer(tid);
                fetchCore.then(
                    function (pack) {
                        if (settled) return;
                        settled = true;
                        window.clearTimeout(tid);
                        resolve(pack);
                    },
                    function (err) {
                        if (settled) return;
                        settled = true;
                        window.clearTimeout(tid);
                        reject(err);
                    }
                );
            });
        }

        function geDiscoveryRenderMulti(recs) {
            if (!discCard) return;
            discCard.classList.toggle("ge-discovery-card--reroll-warm", discRerollCount >= 1);
            discCard.classList.toggle("ge-discovery-card--reroll-deep", discRerollCount >= 2);
            discCard.classList.toggle("ge-discovery-card--commit", discHouseMaxRerolls > 0 && discRerollCount >= discHouseMaxRerolls);

            var shellClass = "ge-reveal-card-shell";
            var html = [];
            html.push('<div class="' + shellClass + '">');
            html.push('<div class="ge-reveal-card-scroll">');

            if (!recs || !recs.length) {
                html.push('<p class="ge-muted">' + geEsc("Nothing surfaced this round.") + "</p>");
                html.push("</div></div>");
                discCard.innerHTML = html.join("");
                geShow(discCard, true);
                return;
            }

            var lrM = geLetterRoomMerge();
            html.push(
                geBuildCupMomentHtml({
                    hero: recs[0],
                    alts: recs.slice(1, 2),
                    addAttr: "data-ge-disc-add",
                    reasonText: geHouseT(
                        "ge.host.serveLine",
                        "Annap chọn cho bạn.",
                        "Annap chooses for you."
                    ),
                    ctaLabel: geFlowT(
                        "ge.host.trayInvite",
                        "Thêm vào khay",
                        "Add to tray"
                    ),
                    detailLabel: geFlowT(
                        "ge.host.readCup",
                        "Chi tiết ly",
                        "Cup details"
                    )
                })
            );

            html.push("</div>");
            html.push('<div class="ge-reveal-footer">');
            html.push('<div class="ge-reveal-actions">');
            var canReroll = discHouseMaxRerolls > 0 && discRerollCount < discHouseMaxRerolls;
            if (canReroll) {
                var rLab = geHouseT("ge.host.reroll", "Bạn muốn tôi xem lại không?", "Would you like me to reconsider?");
                html.push(
                    '<button type="button" class="ge-reveal-btn ge-reveal-btn--quiet guest-hit" data-ge-reroll="1">' +
                        geEsc(rLab) +
                        "</button>"
                );
            } else if (discHouseMaxRerolls > 0 && discRerollCount >= discHouseMaxRerolls) {
                var hrSeal = discLastHouseRitual || {};
                var sealedCopy = "";
                var rl = hrSeal.refusalLines;
                if (Array.isArray(rl) && rl.length > 0) {
                    sealedCopy = String(rl[discRerollCount % rl.length] || "").trim();
                }
                if (!sealedCopy && hrSeal.trustSealedLine && String(hrSeal.trustSealedLine).trim()) {
                    sealedCopy = String(hrSeal.trustSealedLine).trim();
                }
                if (!sealedCopy) {
                    sealedCopy = geHouseT(
                        "houseSelection.rerollSealed",
                        "Quầy giữ tên đã gọi cho bàn bạn tối nay.",
                        "The house stands by this recommendation — too many changes spoil the ritual."
                    );
                }
                html.push('<p class="ge-house-sealed">' + geEsc(sealedCopy) + "</p>");
            }
            html.push("</div></div></div>");
            discCard.innerHTML = html.join("");
            geShow(discCard, true);
            geArmCupMomentDelays(discCard);
            if (discStage) discStage.classList.add("ge-discovery-stage--reveal");
            geDiscArmRerollCooldown();
        }

        function geDiscoveryFail(msg) {
            if (discLine) {
                discLine.textContent =
                    msg ||
                    geHouseT(
                        "ge.host.ritualErr",
                        "Có gì đó gián đoạn — thử lại một lần.",
                        "Something interrupted us — try once more."
                    );
                discLine.className = "ge-discovery-line ge-discovery-line--soft";
            }
            if (discCard) geShow(discCard, false);
            if (discAmbient) discAmbient.classList.remove("is-on");
            if (discVignette) discVignette.classList.remove("is-on");
            if (discStage) {
                discStage.classList.remove("ge-discovery-stage--ritual-active");
                discStage.classList.remove("ge-discovery-stage--reveal");
                discStage.removeAttribute("data-gdr-scene");
                discStage.removeAttribute("data-gdr-leg");
                discStage.removeAttribute("data-ge-env-ix");
            }
            discChosenEnvelopeIx = null;
            if (discLetterDesk && panelDisc && !panelDisc.classList.contains("hidden")) {
                geLetterDeskResetVisual();
                geLetterDeskPaint();
                geShow(discLetterDesk, true);
            }
        }

        function geRunDiscoveryRitual(isReroll) {
            if (!discAmbient || !discLine || !discCard || !discStage) {
                geDiscoveryFail("Discovery could not start — please refresh the page.");
                try {
                    if (window.__ANNAP_DEBUG === true && typeof console !== "undefined" && console.error) {
                        console.error("[DISCOVERY] missing nodes", {
                            ambient: !!discAmbient,
                            line: !!discLine,
                            card: !!discCard,
                            stage: !!discStage
                        });
                    }
                } catch (_log) {}
                return;
            }
            var now = Date.now();
            if (isReroll && discRerollReadyAt && now < discRerollReadyAt) {
                geShowCooldownMsg(
                    geHouseT("houseSelection.rerollWait", "Để mực nghỉ một nhịp rồi mới xem tiếp.", "Give the ink a breath before another pass.")
                );
                return;
            }
            geHideCooldownMsg();
            if (isReroll) {
                discChosenEnvelopeIx = Math.abs(discNonce) % 3;
            }
            if (!isReroll) geDiscResetRerollCount();
            geDiscClearRitual();
            var myTicket = discRitualTicket;
            discLastMs = now;
            try {
                window.sessionStorage.setItem("annap_ge_disc_last", String(discLastMs));
            } catch (e1) {
                /* ignore */
            }

            geShow(discCard, false);
            if (discStage) {
                discStage.classList.add("ge-discovery-stage--ritual-active");
                discStage.classList.remove("ge-discovery-stage--reveal");
                if (discChosenEnvelopeIx != null && isFinite(discChosenEnvelopeIx)) {
                    discStage.setAttribute("data-ge-env-ix", String(discChosenEnvelopeIx));
                }
            }
            discAmbient.classList.add("is-on");
            if (discVignette) discVignette.classList.add("is-on");
            discLine.textContent = geHouseT(
                "ge.host.barPause",
                "Một chút tại quầy bar…",
                "One moment at the bar…"
            );
            discLine.className = "ge-discovery-line ge-discovery-line--enter";

            if (!navigator.onLine) {
                geDiscoveryFail(
                    geHouseT(
                        "ge.sommelier.connectErr",
                        "Kết nối yếu một chút. Bạn có thể thử lại sau.",
                        "The room softened for a moment. You may try again shortly."
                    )
                );
                return;
            }

            var fetchPromise = geDiscoveryFetchPayload();
            var reduced = geReducedMotion();

            function geFinishWithPack(pack) {
                if (myTicket !== discRitualTicket) return;
                if (!pack || typeof pack.ok === "undefined") {
                    geDiscoveryFail(
                        geHouseT(
                            "ge.host.ritualErr",
                            "Có gì đó gián đoạn — thử lại một lần.",
                            "Something interrupted us — try once more."
                        )
                    );
                    return;
                }
                if (!pack.ok) {
                    var er = pack.j && pack.j.error ? pack.j.error : "The list is resting.";
                    geDiscoveryFail(er);
                    return;
                }
                var j = pack.j || {};
                var hr = j.houseRitual || {};
                discLastHouseRitual = hr;
                if (typeof hr.maxRerolls === "number" && isFinite(hr.maxRerolls) && hr.maxRerolls >= 0) {
                    discHouseMaxRerolls = hr.maxRerolls;
                } else {
                    discHouseMaxRerolls = 2;
                }
                var reflection = j.reflection || {};
                var recs = Array.isArray(j.recommendations) ? j.recommendations : [];
                if (!recs.length) {
                    geDiscoveryFail(
                        (j && j.error) ||
                            geHouseT("ge.host.emptyCup", "Chưa đúng — cho tôi thêm một chút.", "Not quite right — give me another moment.")
                    );
                    return;
                }

                var lrApi = j.letterRoom;
                discLastLetterRoom = lrApi && typeof lrApi === "object" ? lrApi : null;

                function geHostRevealLine(ref) {
                    var lead = ref && ref.lead ? String(ref.lead).trim() : "";
                    if (lead) return lead;
                    var para = ref && ref.paragraph ? String(ref.paragraph).trim() : "";
                    if (para) {
                        var cut = para.split(/[.!?]/)[0].trim();
                        if (cut) return cut + ".";
                    }
                    return geHouseT(
                        "ge.host.revealLine",
                        "Tôi nghĩ tôi biết ly nào thuộc về bàn này.",
                        "I think I know what belongs at this table."
                    );
                }

                function showReflectionThenRecs() {
                    if (myTicket !== discRitualTicket) return;
                    var rm = geReducedMotion();
                    var hostLine = geHostRevealLine(reflection);
                    function showHero() {
                        if (myTicket !== discRitualTicket) return;
                        if (discLine) {
                            discLine.textContent = "";
                            discLine.className = "ge-discovery-line";
                        }
                        geDiscoveryRenderMulti(recs);
                    }
                    if (discLine) {
                        discLine.textContent = hostLine;
                        discLine.className =
                            "ge-discovery-line ge-discovery-line--enter ge-discovery-line--lead";
                    }
                    var dBridge = rm ? 400 : 2200;
                    var tBridge = window.setTimeout(showHero, dBridge);
                    geDiscPushTimer(tBridge);
                }

                var dLoad = reduced ? 200 : 900;
                if (discLine) {
                    discLine.textContent = geHouseT(
                        "ge.host.loading",
                        "Một chút — tôi đang ở quầy bar.",
                        "One moment — I'm at the bar."
                    );
                    discLine.className = "ge-discovery-line ge-discovery-line--enter";
                }
                var tLoad = window.setTimeout(function () {
                    if (myTicket !== discRitualTicket) return;
                    showReflectionThenRecs();
                }, dLoad);
                geDiscPushTimer(tLoad);
            }

            function geDiscoveryFailGuarded(msg) {
                if (myTicket !== discRitualTicket) return;
                geDiscoveryFail(msg);
            }

            if (
                window.GuestDiscoveryCourierRitual &&
                typeof window.GuestDiscoveryCourierRitual.play === "function"
            ) {
                window.GuestDiscoveryCourierRitual.play({
                    leg: Math.max(discNonce, discRerollCount + 1),
                    reducedMotion: reduced,
                    letterRoomFast: true,
                    fetchPromise: fetchPromise,
                    stage: discStage,
                    line: discLine,
                    pushTimer: geDiscPushTimer,
                    onPack: geFinishWithPack,
                    onFail: function () {
                        geDiscoveryFailGuarded("");
                    }
                });
                return;
            }

            if (reduced) {
                Promise.all([
                    fetchPromise,
                    new Promise(function (resolve) {
                        var tw = window.setTimeout(resolve, 140);
                        geDiscPushTimer(tw);
                    })
                ])
                    .then(function (arr) {
                        geFinishWithPack(arr[0]);
                    })
                    .catch(function (err) {
                        if (err && err.name === "AbortError") {
                            geDiscoveryFailGuarded("");
                            return;
                        }
                        geDiscoveryFailGuarded("");
                    });
                return;
            }

            Promise.all([
                fetchPromise,
                new Promise(function (resolve) {
                    var tw = window.setTimeout(resolve, 1550);
                    geDiscPushTimer(tw);
                })
            ])
                .then(function (arr) {
                    geFinishWithPack(arr[0]);
                })
                .catch(function (err) {
                    if (err && err.name === "AbortError") {
                        geDiscoveryFailGuarded("");
                        return;
                    }
                    geDiscoveryFailGuarded("");
                });
        }

        if (panelDisc) {
            panelDisc.addEventListener("click", function (ev) {
                var dismissDuck =
                    ev.target && ev.target.closest
                        ? ev.target.closest("[data-ge-duck-blocked-dismiss]")
                        : null;
                if (dismissDuck && discDuckBlocked && discDuckBlocked.contains(dismissDuck)) {
                    ev.preventDefault();
                    geShow(discDuckBlocked, false);
                    return;
                }

                var eveningB =
                    ev.target && ev.target.closest
                        ? ev.target.closest("[data-ge-evening-signal]")
                        : null;
                if (eveningB && discLetterDesk && discLetterDesk.contains(eveningB)) {
                    ev.preventDefault();
                    if (eveningB.disabled) return;
                    var sig = eveningB.getAttribute("data-ge-evening-signal");
                    if (sig) discTasteSignals = [sig];
                    var eveningChoices = discLetterDesk.querySelectorAll(
                        ".ge-evening-choice"
                    );
                    for (var ec = 0; ec < eveningChoices.length; ec++) {
                        eveningChoices[ec].disabled = true;
                        if (eveningChoices[ec] === eveningB) {
                            eveningChoices[ec].classList.add("is-chosen");
                        } else {
                            eveningChoices[ec].classList.add("ge-host-choice--soft");
                        }
                    }
                    var gestureWrap = geById("ge-evening-gesture");
                    if (gestureWrap) gestureWrap.classList.add("ge-evening-gesture--settling");
                    var hintEl = geById("ge-letter-desk-hint");
                    if (hintEl) {
                        hintEl.textContent = geRitualT(
                            "ge.ritual.eveningAck." + sig,
                            geRitualT("ge.host.eveningAck", "Hiểu rồi… cho mình một chút.")
                        );
                        hintEl.className = "ge-letter-desk__hint ge-host-ack";
                    }
                    if (panelDisc) {
                        panelDisc.classList.add("ge-panel--ritual-settling");
                        geRitualApplyDepth(panelDisc, 1);
                    }
                    discChosenEnvelopeIx = Math.abs(discNonce) % 3;
                    window.setTimeout(function () {
                        var gw = geById("ge-evening-gesture");
                        if (gw) geShow(gw, false);
                        if (discLetterDesk) geShow(discLetterDesk, false);
                        if (panelDisc) panelDisc.classList.remove("ge-panel--ritual-settling");
                        try {
                            geRunDiscoveryRitual(false);
                        } catch (eEv) {
                            geDiscoveryFail(
                                geRitualT(
                                    "ge.host.ritualErr",
                                    "Có gì đó gián đoạn — thử lại một lần."
                                )
                            );
                        }
                    }, geReducedMotion() ? 400 : RITUAL_SETTLE_MS);
                    return;
                }

                var duckB =
                    ev.target && ev.target.closest ? ev.target.closest("[data-ge-duck-start]") : null;
                if (duckB && discLetterDesk && discLetterDesk.contains(duckB)) {
                    ev.preventDefault();
                    discChosenEnvelopeIx = Math.abs(discNonce) % 3;
                    try {
                        geRunDiscoveryRitual(false);
                    } catch (eCont) {
                        try {
                            if (typeof console !== "undefined" && console.error) {
                                console.error("[DISCOVERY] duck ritual failed", eCont);
                            }
                        } catch (_d1) {}
                        geDiscoveryFail("Something interrupted the ritual — please try again.");
                    }
                    return;
                }
            });
        }

        (function geWireDuckBlockedGestures() {
            var sheet = geById("ge-duck-blocked-sheet");
            if (!sheet || sheet.getAttribute("data-annap-duck-swipe") === "1") return;
            sheet.setAttribute("data-annap-duck-swipe", "1");
            var y0 = null;
            sheet.addEventListener(
                "touchstart",
                function (e) {
                    if (e.changedTouches && e.changedTouches.length) {
                        y0 = e.changedTouches[0].clientY;
                    }
                },
                { passive: true }
            );
            sheet.addEventListener(
                "touchend",
                function (e) {
                    if (y0 == null) return;
                    var t = e.changedTouches && e.changedTouches[0];
                    var y1 = t ? t.clientY : y0;
                    if (y1 - y0 > 52 && discDuckBlocked && !discDuckBlocked.classList.contains("hidden")) {
                        geShow(discDuckBlocked, false);
                    }
                    y0 = null;
                },
                { passive: true }
            );
            document.addEventListener("keydown", function (e) {
                if (!e || e.key !== "Escape") return;
                if (discDuckBlocked && !discDuckBlocked.classList.contains("hidden")) {
                    geShow(discDuckBlocked, false);
                }
            });
        })();

        if (discStage) {
            discStage.addEventListener("click", function (ev) {
                var addB = ev.target && ev.target.closest ? ev.target.closest("[data-ge-disc-add]") : null;
                if (addB) {
                    ev.preventDefault();
                    var id = addB.getAttribute("data-ge-disc-add");
                    if (id && typeof window.quickAdd === "function") {
                        if (discCard) {
                            discCard.classList.add("ge-discovery-card--to-tray");
                            window.setTimeout(function () {
                                if (discCard) discCard.classList.remove("ge-discovery-card--to-tray");
                            }, 720);
                        }
                        window.quickAdd(id, "", addB);
                        if (discLine) {
                            discLine.textContent = geHouseT(
                                "houseSelection.afterAdd",
                                "Khi ly tới, hỏi thử nên tiếp tục thế nào — một ly chậm hơn hay món bánh từ tủ kính.",
                                "When it lands, ask what would follow nicely — a slower cup or something from the pastry page."
                            );
                            discLine.className = "ge-discovery-line ge-discovery-line--soft";
                            window.setTimeout(function () {
                                if (discLine) discLine.textContent = "";
                            }, 4800);
                        }
                    }
                    geDiscResetRerollCount();
                    return;
                }
                var rr = ev.target && ev.target.closest ? ev.target.closest("[data-ge-reroll]") : null;
                if (rr) {
                    ev.preventDefault();
                    if (rr.getAttribute("aria-disabled") === "true" || rr.classList.contains("ge-reveal-reroll--cooling")) {
                        geShowCooldownMsg(geHouseT("houseSelection.rerollWait", "Để mực nghỉ một nhịp rồi mới xem tiếp.", "Give the ink a breath before another pass."));
                        return;
                    }
                    if (discRerollCount >= discHouseMaxRerolls) {
                        gePaintDuckBlocked();
                        geShow(discDuckBlocked, true);
                        return;
                    }
                    discNonce++;
                    try {
                        window.sessionStorage.setItem("annap_ge_disc_nonce", String(discNonce));
                    } catch (e3) {
                        /* ignore */
                    }
                    geDiscBumpRerollCount();
                    void geRunDiscoveryRitual(true);
                }
            });
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", geInit);
    } else {
        geInit();
    }
})(window, document);
