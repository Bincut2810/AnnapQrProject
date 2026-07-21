/**
 * Live guest preview for experience atelier — reads current form state, no server roundtrip.
 */
(function (global) {
    "use strict";

    function prefersReducedMotion() {
        return global.matchMedia && global.matchMedia("(prefers-reduced-motion: reduce)").matches;
    }

    function parseSeed() {
        var el = document.getElementById("exp-preview-seed");
        if (!el) return {};
        try {
            return JSON.parse(el.textContent || "{}");
        } catch {
            return {};
        }
    }

    function menuMap(seed) {
        var map = {};
        (seed.menu || []).forEach(function (m) {
            map[String(m.id).toLowerCase()] = m;
        });
        return map;
    }

    function fadeHtml(root, html) {
        var reduce = prefersReducedMotion();
        root.style.opacity = reduce ? "1" : "0";
        root.innerHTML = html;
        if (reduce) return;
        requestAnimationFrame(function () {
            root.style.transition = "opacity 0.28s ease";
            root.style.opacity = "1";
        });
    }

    function bindDrawer() {
        var fab = document.getElementById("exp-preview-fab");
        var sheet = document.getElementById("exp-preview-sheet");
        var closeBtn = document.getElementById("exp-preview-close");
        if (!fab || !sheet) return;
        function isWide() {
            return global.matchMedia && global.matchMedia("(min-width: 1100px)").matches;
        }
        function open() {
            sheet.classList.add("exp-preview__sheet--open");
            fab.setAttribute("aria-expanded", "true");
        }
        function shut() {
            sheet.classList.remove("exp-preview__sheet--open");
            fab.setAttribute("aria-expanded", "false");
        }
        fab.addEventListener("click", function () {
            if (isWide()) return;
            if (sheet.classList.contains("exp-preview__sheet--open")) shut();
            else open();
        });
        if (closeBtn) closeBtn.addEventListener("click", shut);
        if (isWide()) sheet.classList.add("exp-preview__sheet--open");
        global.addEventListener("resize", function () {
            if (isWide()) sheet.classList.add("exp-preview__sheet--open");
        });
    }

    function bindParallax(phone) {
        if (!phone || prefersReducedMotion()) return;
        var maxDeg = 1.5;
        global.addEventListener(
            "mousemove",
            function (e) {
                var r = phone.getBoundingClientRect();
                var cx = r.left + r.width / 2;
                var cy = r.top + r.height / 2;
                var rx = ((e.clientX - cx) / Math.max(1, r.width / 2)) * maxDeg;
                var ry = ((-e.clientY + cy) / Math.max(1, r.height / 2)) * maxDeg;
                rx = Math.max(-maxDeg, Math.min(maxDeg, rx));
                ry = Math.max(-maxDeg, Math.min(maxDeg, ry));
                phone.style.transform = "perspective(880px) rotateY(" + rx + "deg) rotateX(" + ry + "deg)";
            },
            { passive: true }
        );
    }

    function esc(s) {
        if (!s) return "";
        return String(s)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/"/g, "&quot;");
    }

    function readSignatures(seed, root) {
        var list = document.getElementById("sig-sort-list");
        var byId = menuMap(seed);
        if (!list) {
            fadeHtml(root, '<p class="exp-lp-hint">Open the signature rail editor to shape the group path.</p>');
            return;
        }
        var cards = [].slice.call(list.querySelectorAll("[data-sig-card]"));
        var parts = cards.map(function (card, i) {
            var sel = card.querySelector('select[name="SlotForms[' + i + '].MenuItemId"]');
            var mid = sel && sel.value;
            var activeEl = card.querySelector('select[name="SlotForms[' + i + '].IsActive"]');
            var active = activeEl && activeEl.value === "true";
            var kInp = card.querySelector('input[name="SlotForms[' + i + '].EditorialKicker"]');
            var bodyTa = card.querySelector('textarea[name="SlotForms[' + i + '].EditorialBody"]');
            var kicker = kInp ? kInp.value : "";
            var body = bodyTa ? bodyTa.value : "";
            var m = mid && byId[String(mid).toLowerCase()];
            var title = m ? m.name : "—";
            var sub = kicker || (m && m.subtitle) || "";
            var taste = body || (m && m.tastingNotes) || "";
            var img = m && m.imageUrl ? m.imageUrl : "";
            var price = m && typeof m.price !== "undefined" ? window.AnnapMoney.format(m.price) : "—";
            var fade = active ? "" : " exp-lp-card--dim";
            return (
                '<div class="exp-lp-card exp-lp-card--rail' +
                fade +
                '">' +
                '<div class="exp-lp-card__img"><img src="' +
                esc(img) +
                '" alt="" loading="lazy"/></div>' +
                '<div class="exp-lp-card__body">' +
                '<p class="exp-lp-card__title">' +
                esc(title) +
                "</p>" +
                '<p class="exp-lp-card__sub">' +
                esc(sub) +
                "</p>" +
                '<p class="exp-lp-card__taste">' +
                esc(taste) +
                "</p>" +
                '<p class="exp-lp-card__price">' +
                esc(price) +
                "</p>" +
                '<div class="exp-lp-card__actions">' +
                '<span class="exp-lp-btn exp-lp-btn--ghost">Detail</span>' +
                '<span class="exp-lp-btn exp-lp-btn--solid">Send to table</span>' +
                "</div></div></div>"
            );
        });
        fadeHtml(
            root,
            '<div class="exp-lp exp-lp--signatures">' +
                '<p class="exp-lp-kicker">Group path</p>' +
                '<div class="exp-lp-rail">' +
                parts.join("") +
                "</div></div>"
        );
    }

    function affMapFromSeed(seed) {
        var map = {};
        (seed.questions || []).forEach(function (q) {
            (q.options || []).forEach(function (o) {
                map[String(o.id).toLowerCase()] = o.affinities || [];
            });
        });
        return map;
    }

    function hash01(str) {
        var h = 0;
        for (var i = 0; i < str.length; i++) h = (h * 31 + str.charCodeAt(i)) >>> 0;
        return (h % 997) / 997;
    }

    function readGuided(seed, root, state) {
        var list = document.getElementById("q-sort-list");
        if (!list) {
            fadeHtml(root, '<p class="exp-lp-hint">Open the guided sommelier editor to shape questions.</p>');
            return;
        }
        var qCards = [].slice.call(list.querySelectorAll("[data-q-card]"));
        var liveQs = [];
        qCards.forEach(function (card, qi) {
            var en = card.querySelector('select[name="QuestionsForm[' + qi + '].IsEnabled"]');
            if (en && en.value !== "true") return;
            var prompt = (card.querySelector('input[name="QuestionsForm[' + qi + '].Prompt"]') || {}).value || "";
            var optCards = [].slice.call(card.querySelectorAll("[data-opt-card]"));
            var opts = [];
            optCards.forEach(function (oc, oi) {
                var oe = oc.querySelector('select[name="QuestionsForm[' + qi + "].Options[" + oi + '].IsEnabled"]');
                if (oe && oe.value !== "true") return;
                var idInp = oc.querySelector('input[name="QuestionsForm[' + qi + "].Options[" + oi + '].Id"]');
                var label = (oc.querySelector('input[name="QuestionsForm[' + qi + "].Options[" + oi + '].Label"]') || {}).value || "Choice";
                var sub = (oc.querySelector('input[name="QuestionsForm[' + qi + "].Options[" + oi + '].Subline"]') || {}).value || "";
                var wmInp = oc.querySelector('input[name="QuestionsForm[' + qi + "].Options[" + oi + '].WeightMultiplier"]');
                var wm = wmInp ? parseFloat(wmInp.value) || 1 : 1;
                opts.push({ id: idInp ? idInp.value : "", label: label, sub: sub, wm: wm });
            });
            if (opts.length) liveQs.push({ prompt: prompt, options: opts });
        });

        if (!liveQs.length) {
            fadeHtml(root, '<p class="exp-lp-hint">Enable at least one question with answers to walk the tasting.</p>');
            return;
        }

        if (state.step >= liveQs.length) {
            var affByOpt = affMapFromSeed(seed);
            var menus = seed.menu || [];
            var picks = state.picks || [];
            var scored = menus.map(function (m) {
                var s = 42;
                picks.forEach(function (pid) {
                    var affs = affByOpt[String(pid).toLowerCase()] || [];
                    affs.forEach(function (a) {
                        if (String(a.menuItemId).toLowerCase() === String(m.id).toLowerCase()) s += (a.weight || 0) * 22;
                    });
                });
                var optBlend =
                    picks.length === 0
                        ? 1
                        : picks.reduce(function (acc, pid) {
                              var o = null;
                              (seed.questions || []).some(function (q) {
                                  return (q.options || []).some(function (x) {
                                      if (String(x.id).toLowerCase() === String(pid).toLowerCase()) {
                                          o = x;
                                          return true;
                                      }
                                      return false;
                                  });
                              });
                              var w = o && o.weightMultiplier ? parseFloat(o.weightMultiplier) : 1;
                              return acc + (isNaN(w) || w <= 0 ? 1 : Math.min(4, Math.max(0.25, w)));
                          }, 0) / picks.length;
                s *= 0.85 + 0.15 * optBlend;
                s += hash01(m.id + picks.join(",")) * 8;
                return { m: m, s: s };
            });
            scored.sort(function (a, b) {
                return b.s - a.s;
            });
            var top = scored.slice(0, 5);
            var ambient =
                picks.length === 0
                    ? "We listened for a quiet line through the menu."
                    : "Your answers sketch a mood; here is how the list answers back.";
            var cards = top.map(function (row, idx) {
                var pct = 88 - idx * 4;
                var line = (row.m.tastingNotes || row.m.moodProfile || "").slice(0, 120);
                return (
                    '<div class="exp-lp-rec">' +
                    '<div class="exp-lp-rec__top"><span>' +
                    esc(row.m.name) +
                    '</span><span class="exp-lp-rec__pct">' +
                    pct +
                    "%</span></div>" +
                    '<p class="exp-lp-rec__line">' +
                    esc(line) +
                    "</p>" +
                    '<p class="exp-lp-rec__soft">' +
                    esc(ambient) +
                    "</p></div>"
                );
            });
        fadeHtml(
            root,
            '<div class="exp-lp exp-lp--guided">' +
                '<p class="exp-lp-kicker">Recommendations</p>' +
                cards.join("") +
                '<button type="button" class="exp-lp-btn exp-lp-btn--ghost exp-lp-wide" data-exp-guided-reset>Begin again</button></div>'
        );
        return;
    }

        var q = liveQs[state.step];
        var btns = q.options
            .map(function (o, j) {
                return (
                    '<button type="button" class="exp-lp-choice" data-exp-opt-idx="' +
                    j +
                    '">' +
                    '<span class="exp-lp-choice__l">' +
                    esc(o.label) +
                    "</span>" +
                    '<span class="exp-lp-choice__s">' +
                    esc(o.sub) +
                    "</span></button>"
                );
            })
            .join("");
        fadeHtml(
            root,
            '<div class="exp-lp exp-lp--guided">' +
                '<p class="exp-lp-kicker">Guided · step ' +
                (state.step + 1) +
                "</p>" +
                '<p class="exp-lp-q">' +
                esc(q.prompt) +
                "</p>" +
                '<div class="exp-lp-choices">' +
                btns +
                "</div></div>"
        );
    }

    function val(form, name) {
        if (!form) return "";
        var el = form.querySelector('[name="' + name + '"]');
        return el ? el.value : "";
    }

    function chk(form, name) {
        if (!form) return false;
        var el = form.querySelector('[name="' + name + '"]');
        return !!(el && el.checked);
    }

    function readDiscovery(root) {
        var host = document.querySelector('[data-exp-preview="discovery"]');
        var form = host && host.querySelector("form");
        if (!form) {
            fadeHtml(root, '<p class="exp-lp-hint">Discovery controls appear when you open the ritual editor.</p>');
            return;
        }
        var tone = parseInt(val(form, "SettingsForm.AdventureTone"), 10) || 3;
        var courier = val(form, "SettingsForm.CourierMoodCopy");
        var reroll = chk(form, "SettingsForm.AllowRerolls");
        var seasonalOnly = chk(form, "SettingsForm.SeasonalOnlyPool");
        var rows = [].slice.call(form.querySelectorAll("[data-exp-disc-row]"));
        var pool = [];
        rows.forEach(function (row) {
            var idInp = row.querySelector('input[name$=".MenuItemId"]');
            if (!idInp || !idInp.name) return;
            var m = /MenuForms\[(\d+)\]/.exec(idInp.name);
            if (!m) return;
            var i = m[1];
            var w = row.querySelector('input[name="MenuForms[' + i + '].DiscoveryWeight"]');
            var story = row.querySelector('textarea[name="MenuForms[' + i + '].DiscoveryStory"]');
            var hid = row.querySelector('input[name="MenuForms[' + i + '].IsHiddenDiscovery"]');
            var eli = row.querySelector('input[name="MenuForms[' + i + '].IsDiscoveryEligible"]');
            var nm = (row.querySelector("p.text-sm") || {}).textContent || "Cup";
            var weight = w ? parseFloat(w.value) || 0 : 0;
            var eligible = eli ? eli.checked : true;
            var hidden = hid ? hid.checked : false;
            if (eligible && !hidden && weight > 0) pool.push({ name: nm.trim(), story: story ? story.value : "" });
        });
        var pick = pool.length ? pool[Math.floor(hash01(courier + tone + pool.length) * pool.length)] : null;
        var stage =
            tone <= 2
                ? "A hushed stage — soft light on linen."
                : tone >= 4
                  ? "The room leans forward — a little theatre in the draw."
                  : "Balanced air — confident but unhurried.";
        fadeHtml(
            root,
            '<div class="exp-lp exp-lp--discovery">' +
                '<p class="exp-lp-kicker">Discovery</p>' +
                '<p class="exp-lp-stage">' +
                esc(stage) +
                "</p>" +
                '<p class="exp-lp-courier">' +
                esc(courier || "The house chooses a cup that matches the evening you described.") +
                "</p>" +
                '<div class="exp-lp-reveal">' +
                (pick
                    ? '<p class="exp-lp-reveal__n">' +
                      esc(pick.name) +
                      "</p>" +
                      '<p class="exp-lp-reveal__s">' +
                      esc(pick.story || "A quiet line from the house menu — yours to unfold.") +
                      "</p>"
                    : "<p class=\"exp-lp-hint\">No cups are ready for this mix yet — open the selection or raise a cup’s presence.</p>") +
                "</div>" +
                '<p class="exp-lp-meta">' +
                esc(seasonalOnly ? "Seasonal-only selection · " : "") +
                (reroll ? "Rerolls allowed" : "Single reveal") +
                " · tone " +
                tone +
                "/5</p></div>"
        );
    }

    function findHomepageForm() {
        var form = document.getElementById("exp-homepage-form");
        if (form) return form;
        form = document.getElementById("exp-homepage-form-hub");
        if (form) return form;
        var panel = document.querySelector("[data-homepage-composition]");
        return panel ? panel.querySelector("[data-homepage-preview-form]") : null;
    }

    function readHomepage(root) {
        var form = findHomepageForm();
        if (!form) {
            fadeHtml(root, '<p class="exp-lp-hint">Homepage composition preview.</p>');
            return;
        }
        var modes = [
            { id: "group", label: "Đi nhóm", hint: "Table ritual" },
            { id: "sommelier", label: "Một mình", hint: "Concierge" },
            { id: "discovery", label: "Bất ngờ", hint: "Sealed path" }
        ];
        var visible = [];
        modes.forEach(function (m) {
            var inp = form.querySelector('[data-homepage-flow="' + m.id + '"]');
            if (inp && inp.checked) visible.push(m);
        });
        var count = visible.length;
        var stackClass = "exp-lp-home__stack";
        if (count >= 1 && count <= 3) stackClass += " exp-lp-home__stack--" + count;
        var cards = visible
            .map(function (m, i) {
                var ix = i + 1 < 10 ? "0" + (i + 1) : String(i + 1);
                return (
                    '<div class="exp-lp-home__card"><span style="opacity:0.55">' +
                    ix +
                    "</span><br><strong>" +
                    esc(m.label) +
                    "</strong><br>" +
                    esc(m.hint) +
                    "</div>"
                );
            })
            .join("");
        var empty =
            count === 0
                ? '<p class="exp-lp-home__empty">Experience temporarily being curated.</p>'
                : "";
        var hero =
            count === 1
                ? '<p class="exp-lp-home__kicker">Today&apos;s recommended journey</p>'
                : '<p class="exp-lp-home__kicker">106/1 · arrival</p>';
        fadeHtml(
            root,
            '<div class="exp-lp exp-lp-home">' +
                hero +
                '<div class="' +
                stackClass +
                '">' +
                cards +
                "</div>" +
                empty +
                "</div>"
        );
    }

    function readHub(seed, root) {
        var sig = (seed.signatures || [])
            .map(function (s) {
                var fade = s.isActive ? "" : " exp-lp-card--dim";
                return (
                    '<div class="exp-lp-card exp-lp-card--rail' +
                    fade +
                    '">' +
                    '<div class="exp-lp-card__img"><img src="' +
                    esc(s.imageUrl) +
                    '" alt=""/></div>' +
                    '<div class="exp-lp-card__body"><p class="exp-lp-card__title">' +
                    esc(s.name) +
                    "</p><p class=\"exp-lp-card__sub\">" +
                    esc(s.subtitle || "") +
                    "</p></div></div>"
                );
            })
            .join("");
        var g = (seed.guided || [])
            .map(function (q) {
                var opts = (q.options || []).map(function (o) {
                    return "<li>" + esc(o.label) + "</li>";
                });
                return (
                    '<div class="exp-lp-hub__q"><p class="exp-lp-hub__qp">' +
                    esc(q.prompt) +
                    "</p><ul class=\"exp-lp-hub__ul\">" +
                    opts.join("") +
                    "</ul></div>"
                );
            })
            .join("");
        var d = seed.discovery || {};
        fadeHtml(
            root,
            '<div class="exp-lp exp-lp--hub">' +
                '<div class="exp-lp-hub-tabs">' +
                '<span class="exp-lp-hub-tab exp-lp-hub-tab--on" data-tab="g">Group</span>' +
                '<span class="exp-lp-hub-tab" data-tab="s">Guided</span>' +
                '<span class="exp-lp-hub-tab" data-tab="d">Discovery</span>' +
                "</div>" +
                '<div class="exp-lp-hub-panel" data-panel="g"><p class="exp-lp-kicker">Signature rail</p><div class="exp-lp-rail">' +
                sig +
                "</div></div>" +
                '<div class="exp-lp-hub-panel" data-panel="s" hidden><p class="exp-lp-kicker">Sommelier path</p>' +
                g +
                "</div>" +
                '<div class="exp-lp-hub-panel" data-panel="d" hidden><p class="exp-lp-kicker">Ritual tone</p>' +
                "<p class=\"exp-lp-courier\">" +
                esc(d.courier || "Courier copy follows your discovery settings.") +
                "</p><p class=\"exp-lp-meta\">Adventure " +
                esc(String(d.adventureTone || "—")) +
                " · rerolls " +
                (d.allowRerolls ? "on" : "off") +
                "</p></div></div>"
        );
        [].slice.call(root.querySelectorAll(".exp-lp-hub-tab")).forEach(function (tab) {
            tab.addEventListener("click", function () {
                var id = tab.getAttribute("data-tab");
                [].slice.call(root.querySelectorAll(".exp-lp-hub-tab")).forEach(function (t) {
                    return t.classList.toggle("exp-lp-hub-tab--on", t.getAttribute("data-tab") === id);
                });
                [].slice.call(root.querySelectorAll(".exp-lp-hub-panel")).forEach(function (p) {
                    p.hidden = p.getAttribute("data-panel") !== id;
                });
            });
        });
    }

    function init() {
        var host =
            document.querySelector("[data-exp-preview-host]") ||
            document.querySelector("[data-exp-preview]");
        var root = document.getElementById("exp-live-preview");
        if (!host || !root) return;
        var mode = host.getAttribute("data-exp-preview") || "hub";
        var seed = parseSeed();
        bindDrawer();
        bindParallax(document.querySelector("[data-exp-phone]"));

        var state = { step: 0, picks: [] };

        if (mode === "guided") {
            host.addEventListener("click", function (e) {
                var btn = e.target.closest("[data-exp-opt-idx]");
                if (!btn || !host.contains(btn)) return;
                e.preventDefault();
                var list = document.getElementById("q-sort-list");
                if (!list) return;
                var qCards = [].slice.call(list.querySelectorAll("[data-q-card]"));
                var liveQs = [];
                qCards.forEach(function (card, qi) {
                    var en = card.querySelector('select[name="QuestionsForm[' + qi + '].IsEnabled"]');
                    if (en && en.value !== "true") return;
                    var prompt = (card.querySelector('input[name="QuestionsForm[' + qi + '].Prompt"]') || {}).value || "";
                    var optCards = [].slice.call(card.querySelectorAll("[data-opt-card]"));
                    var opts = [];
                    optCards.forEach(function (oc, oi) {
                        var oe = oc.querySelector('select[name="QuestionsForm[' + qi + "].Options[" + oi + '].IsEnabled"]');
                        if (oe && oe.value !== "true") return;
                        var idInp = oc.querySelector('input[name="QuestionsForm[' + qi + "].Options[" + oi + '].Id"]');
                        var label = (oc.querySelector('input[name="QuestionsForm[' + qi + "].Options[" + oi + '].Label"]') || {}).value || "Choice";
                        var sub = (oc.querySelector('input[name="QuestionsForm[' + qi + "].Options[" + oi + '].Subline"]') || {}).value || "";
                        var wmInp = oc.querySelector('input[name="QuestionsForm[' + qi + "].Options[" + oi + '].WeightMultiplier"]');
                        var wm = wmInp ? parseFloat(wmInp.value) || 1 : 1;
                        opts.push({ id: idInp ? idInp.value : "", label: label, sub: sub, wm: wm });
                    });
                    if (opts.length) liveQs.push({ prompt: prompt, options: opts });
                });
                if (state.step >= liveQs.length) return;
                var q = liveQs[state.step];
                var idx = parseInt(btn.getAttribute("data-exp-opt-idx"), 10);
                var opt = q.options[idx];
                if (!opt || !opt.id) return;
                state.picks = state.picks || [];
                state.picks.push(opt.id);
                state.step++;
                readGuided(seed, root, state);
            });
            host.addEventListener("click", function (e) {
                if (!e.target.closest("[data-exp-guided-reset]") || !root.contains(e.target.closest("[data-exp-guided-reset]"))) return;
                state.step = 0;
                state.picks = [];
                readGuided(seed, root, state);
            });
        }

        function tick() {
            if (mode === "signatures") readSignatures(seed, root);
            else if (mode === "guided") readGuided(seed, root, state);
            else if (mode === "discovery") readDiscovery(root);
            else if (mode === "homepage") readHomepage(root);
            else readHub(seed, root);
        }

        tick();
        host.addEventListener("input", tick);
        host.addEventListener("change", tick);
        var list = document.getElementById("sig-sort-list");
        if (list)
            new MutationObserver(tick).observe(list, {
                childList: true,
                subtree: true
            });
        var qList = document.getElementById("q-sort-list");
        if (qList)
            new MutationObserver(tick).observe(qList, {
                childList: true,
                subtree: true
            });

        global.addEventListener("annap:homepage-composition-changed", function () {
            if (mode === "homepage") readHomepage(root);
        });

        [].slice.call(document.querySelectorAll("[data-homepage-composition] form")).forEach(function (hpForm) {
            hpForm.addEventListener("change", function () {
                if (mode === "homepage" && root) readHomepage(root);
            });
        });
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})(window);
