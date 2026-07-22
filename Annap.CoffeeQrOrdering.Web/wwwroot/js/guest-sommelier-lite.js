/**

 * Slim seated arrival — lightweight AI sommelier (no guest-experience.js ritual stack).

 * Uses server-validated preference maps when compatible; degrades in-sheet when catalog drifts.

 */

(function (global) {

    "use strict";



    var ROOT_ID = "guest-sommelier-lite";

    var OPEN_ID = "guest-arrival-somm-open";

    var BOOT_ID = "guest-sommelier-lite-boot";



    var state = {

        compatible: false,

        preferenceMap: null,

        lastRecommendPack: null,

        lastVt: ""

    };



    function t(key) {
        if (global.LuxuryI18n && typeof global.LuxuryI18n.t === "function") {
            var v = global.LuxuryI18n.t(key);
            if (v) return v;
        }
        return key;
    }

    function guestLang() {
        try {
            if (global.LuxuryI18n && typeof global.LuxuryI18n.getLang === "function") {
                return global.LuxuryI18n.getLang() === "en" ? "en" : "vi";
            }
        } catch (_e) {}
        return (document.documentElement.lang || "vi").toLowerCase().indexOf("en") === 0
            ? "en"
            : "vi";
    }

    /** Resolve catalog/API bilingual fields: string (legacy) or { vi, en }. */
    function localize(field) {
        if (field == null) return "";
        if (typeof field === "string") return String(field).trim();
        if (typeof field !== "object") return String(field).trim();
        var lang = guestLang();
        var primary = lang === "en" ? field.en : field.vi;
        var fallback = lang === "en" ? field.vi : field.en;
        var s = primary != null && String(primary).trim() ? String(primary).trim() : "";
        if (s) return s;
        return fallback != null && String(fallback).trim() ? String(fallback).trim() : "";
    }



    function incompatibleSheetMessage() {

        return t(

            "guest.sommelierLite.incompatibleSheet",

            "I need a bit more data to suggest precisely. You can still open the menu or try again later."

        );

    }



    function esc(s) {

        return String(s || "")

            .replace(/&/g, "&amp;")

            .replace(/</g, "&lt;")

            .replace(/"/g, "&quot;");

    }



    function apiUrl(path) {

        return typeof global.__annapApiUrl === "function" ? global.__annapApiUrl(path) : path;

    }



    function readBootJson() {

        var el = document.getElementById(BOOT_ID);

        if (!el || !el.textContent) return null;

        try {

            return JSON.parse(el.textContent);

        } catch (_e) {

            return null;

        }

    }



    function storeConfig(cfg) {

        if (!cfg) {

            state.compatible = false;

            state.preferenceMap = null;

            return;

        }

        state.compatible = cfg.compatible === true;

        var pm = cfg.preferenceMap;

        if (pm && pm.taste && pm.q3 && pm.caffeine && pm.drinkFamily) {

            state.preferenceMap = pm;

        } else {

            state.preferenceMap = null;

        }

    }



    function loadConfig() {

        var boot = readBootJson();

        if (boot) {

            storeConfig(boot);

            return Promise.resolve();

        }

        if (!navigator.onLine) {

            return Promise.resolve();

        }

        return fetch(apiUrl("/api/guest/sommelier-lite/config"), {

            headers: { Accept: "application/json" },

            cache: "no-store"

        })

            .then(function (r) {

                if (!r.ok) return;

                return r.json().then(function (j) {

                    storeConfig(j);

                });

            })

            .catch(function () {

                /* keep CTA; in-sheet errors on submit */

            });

    }



    function readVt() {
        try {
            return (sessionStorage.getItem("annap_venue_table_id") || "").trim();
        } catch (_e) {
            return "";
        }
    }



    function buildOptionIds(prefs) {

        if (!state.preferenceMap) return null;

        var pm = state.preferenceMap;

        var taste = prefs.taste;

        var milk = prefs.milk;

        var caffeine = prefs.caffeine;

        var temperature = prefs.temperature;

        var q1 = pm.taste && pm.taste[taste];

        var q3 = pm.q3 && pm.q3[taste];

        var q4 = pm.caffeine && pm.caffeine[caffeine];

        var q2 = pm.drinkFamily && pm.drinkFamily[milk + "|" + temperature];

        if (!q1 || !q2 || !q3 || !q4) return null;

        return [q1, q2, q3, q4];

    }



    function formatPrice(price) {
        if (window.AnnapMoney && typeof window.AnnapMoney.format === "function") {
            return window.AnnapMoney.format(price);
        }
        var n = Number(price);
        if (!isFinite(n)) return "";
        return String(price);
    }



    function setStatus(el, message, isError) {

        if (!el) return;

        el.textContent = message || "";

        el.classList.toggle("guest-sommelier-lite__status--error", !!isError);

        el.hidden = !message;

    }



    function updateCapabilityNotice(el) {

        if (!el) return;

        el.hidden = state.compatible;

    }



    function wireChoiceGroup(root, groupName, onChange) {

        root.querySelectorAll('[data-somm-group="' + groupName + '"]').forEach(function (btn) {

            btn.addEventListener("click", function () {

                var val = btn.getAttribute("data-somm-value") || "";

                root.querySelectorAll('[data-somm-group="' + groupName + '"]').forEach(function (b) {

                    b.setAttribute("aria-pressed", b === btn ? "true" : "false");

                });

                if (onChange) onChange(val);

            });

        });

    }



    function readPrefs(root) {

        function picked(group) {

            var btn = root.querySelector('[data-somm-group="' + group + '"][aria-pressed="true"]');

            return btn ? btn.getAttribute("data-somm-value") || "" : "";

        }

        return {

            taste: picked("taste"),

            milk: picked("milk"),

            caffeine: picked("caffeine"),

            temperature: picked("temperature")

        };

    }



    function prefsComplete(prefs) {

        return !!(prefs.taste && prefs.milk && prefs.caffeine && prefs.temperature);

    }



    function renderResults(container, results, reflection, vt, statusEl) {

        if (!container) return;

        if (!results || !results.length) {

            setStatus(

                statusEl,

                t("guest.sommelierLite.empty", "The house could not name a cup just now."),

                false

            );

            container.hidden = true;

            container.innerHTML = "";

            return;

        }

        var parts = [];

        if (reflection) {
            parts.push('<p class="guest-sommelier-lite__card-reason">' + esc(localize(reflection)) + "</p>");
        }

        var canAdd = !!(global.GuestInteractionContract && typeof global.GuestInteractionContract.addItem === "function");

        for (var i = 0; i < Math.min(results.length, 3); i++) {
            var r = results[i];
            var id = r.menuItemId || r.MenuItemId || "";
            var name = r.name || r.Name || "";
            var price = r.price != null ? r.price : r.Price;
            var reason = localize(r.emotionalExplanation || r.EmotionalExplanation || "");
            var detailHref = vt ? "/menu/drink/" + id + "?vt=" + encodeURIComponent(vt) : "/menu/drink/" + id;

            parts.push('<article class="guest-sommelier-lite__card">');

            parts.push('<h3 class="guest-sommelier-lite__card-name">' + esc(name) + "</h3>");

            if (price != null) {

                parts.push('<p class="guest-sommelier-lite__card-meta">' + esc(formatPrice(price)) + "</p>");

            }

            if (reason) {

                parts.push('<p class="guest-sommelier-lite__card-reason">' + esc(reason) + "</p>");

            }

            parts.push('<div class="guest-sommelier-lite__card-actions">');

            if (canAdd && id) {

                parts.push(

                    '<button type="button" class="guest-sommelier-lite__card-btn guest-sommelier-lite__card-btn--primary guest-hit" data-somm-add="' +

                        esc(id) +

                        '" data-somm-name="' +

                        esc(name) +

                        '" data-somm-price="' +

                        esc(String(price)) +

                        '">' +

                        esc(t("guest.sommelierLite.addToTray", "Add to tray")) +

                        "</button>"

                );

            }

            parts.push(

                '<a class="guest-sommelier-lite__card-btn guest-hit" href="' +

                    esc(detailHref) +

                    '">' +

                    esc(t("guest.sommelierLite.viewDrink", "View drink")) +

                    "</a>"

            );

            parts.push("</div></article>");

        }

        container.innerHTML = parts.join("");

        container.hidden = false;

        if (global.InteractionFeedback) {
            global.InteractionFeedback.trigger("whoosh", { silentVisual: true });
        }

        container.querySelectorAll("[data-somm-add]").forEach(function (btn) {

            btn.addEventListener("click", function () {

                var menuId = btn.getAttribute("data-somm-add");

                var itemName = btn.getAttribute("data-somm-name") || "";

                var unitPrice = Number(btn.getAttribute("data-somm-price")) || 0;

                if (global.GuestInteractionContract && typeof global.GuestInteractionContract.addItem === "function") {

                    global.GuestInteractionContract.addItem({

                        menuItemId: menuId,

                        name: itemName,

                        unitPrice: unitPrice,

                        sourceElement: btn

                    });

                    btn.textContent = t("guest.sommelierLite.added");

                    btn.disabled = true;

                } else {

                    setStatus(

                        statusEl,

                        t("guest.sommelierLite.trayUnavailable"),

                        false

                    );

                }

            });

        });

    }



    function initSommelierLite() {

        var openBtn = document.getElementById(OPEN_ID);

        var root = document.getElementById(ROOT_ID);

        if (!openBtn || !root) return;



        var form = root.querySelector("#guest-sommelier-lite-form");

        var submitBtn = root.querySelector("[data-somm-submit]");

        var statusEl = root.querySelector("[data-somm-status]");

        var resultsEl = root.querySelector("[data-somm-results]");

        var closeBtn = root.querySelector("[data-somm-close]");

        var capabilityEl = root.querySelector("[data-somm-capability-notice]");

        var prefs = {};



        function updateSubmit() {

            if (submitBtn) submitBtn.disabled = !prefsComplete(prefs);

        }



        ["taste", "milk", "caffeine", "temperature"].forEach(function (group) {

            wireChoiceGroup(root, group, function (val) {

                prefs[group] = val;

                updateSubmit();

            });

        });



        function openPanel() {

            updateCapabilityNotice(capabilityEl);

            setStatus(statusEl, "", false);

            root.hidden = false;

            document.body.style.overflow = "hidden";

            if (closeBtn) closeBtn.focus();

        }



        function closePanel() {

            root.hidden = true;

            document.body.style.overflow = "";

            openBtn.focus();

        }



        openBtn.addEventListener("click", openPanel);

        if (closeBtn) closeBtn.addEventListener("click", closePanel);

        root.addEventListener("click", function (ev) {

            if (ev.target === root) closePanel();

        });

        document.addEventListener("keydown", function (ev) {

            if (ev.key === "Escape" && !root.hidden) closePanel();

        });



        if (form) {

            form.addEventListener("submit", function (ev) {

                ev.preventDefault();

                prefs = readPrefs(root);

                if (!prefsComplete(prefs)) return;



                var optionIds = buildOptionIds(prefs);

                if (!state.compatible || !optionIds) {

                    setStatus(statusEl, incompatibleSheetMessage(), true);

                    return;

                }



                if (submitBtn) submitBtn.disabled = true;

                setStatus(statusEl, t("guest.sommelierLite.loading", "Finding a cup for you…"), false);

                if (resultsEl) {

                    resultsEl.hidden = true;

                    resultsEl.innerHTML = "";

                }

                if (!navigator.onLine) {

                    setStatus(

                        statusEl,

                        t("guest.sommelierLite.offline", "Cannot reach the bar right now. Open the menu or try again."),

                        true

                    );

                    if (submitBtn) submitBtn.disabled = false;

                    return;

                }

                fetch(apiUrl("/api/guest/guided-sommelier/recommend"), {

                    method: "POST",

                    headers: { "Content-Type": "application/json", Accept: "application/json" },

                    body: JSON.stringify({ optionIds: optionIds }),

                    cache: "no-store"

                })

                    .then(function (r) {

                        return r

                            .json()

                            .catch(function () {

                                return {};

                            })

                            .then(function (j) {

                                return { ok: r.ok, j: j };

                            });

                    })

                    .then(function (pack) {

                        if (!pack.ok) {

                            var err =

                                (pack.j && (pack.j.error || pack.j.message)) ||

                                t("guest.sommelierLite.error", "Could not suggest a drink right now. Open the menu to browse.");

                            setStatus(statusEl, err, true);

                            return;

                        }

                        setStatus(statusEl, "", false);

                        state.lastRecommendPack = pack.j || null;
                        state.lastVt = readVt();

                        renderResults(

                            resultsEl,

                            (pack.j && pack.j.results) || [],

                            pack.j && pack.j.personalityReflection,

                            state.lastVt,

                            statusEl

                        );

                    })

                    .catch(function () {

                        setStatus(

                            statusEl,

                            t("guest.sommelierLite.error", "Could not suggest a drink right now. Open the menu to browse."),

                            true

                        );

                    })

                    .finally(function () {

                        if (submitBtn) submitBtn.disabled = !prefsComplete(readPrefs(root));

                    });

            });

        }



        loadConfig().then(function () {

            updateCapabilityNotice(capabilityEl);

            if (global.LuxuryI18n && typeof global.LuxuryI18n.applyDom === "function") {

                global.LuxuryI18n.applyDom(root);

            }

        });

        global.addEventListener("luxury:i18n-changed", function () {
            if (global.LuxuryI18n && typeof global.LuxuryI18n.applyDom === "function") {
                global.LuxuryI18n.applyDom(root);
            }
            updateCapabilityNotice(capabilityEl);
            if (state.lastRecommendPack && resultsEl && !resultsEl.hidden) {
                renderResults(
                    resultsEl,
                    state.lastRecommendPack.results || [],
                    state.lastRecommendPack.personalityReflection,
                    state.lastVt || readVt(),
                    statusEl
                );
            }
        });

    }



    function start() {

        if (global.LuxuryI18n && global.LuxuryI18n.ready && typeof global.LuxuryI18n.ready.then === "function") {

            global.LuxuryI18n.ready.then(initSommelierLite).catch(initSommelierLite);

        } else {

            initSommelierLite();

        }

    }



    global.annapStartSommelierLite = start;



    if (document.readyState === "loading") {

        document.addEventListener("DOMContentLoaded", start);

    } else {

        start();

    }

})(typeof window !== "undefined" ? window : globalThis);


