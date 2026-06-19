/**
 * DrinkDetailRenderer — menu-data-driven editorial composition (dd-* markup only).
 */
(function (global) {
    "use strict";

    function esc(s) {
        if (s == null) return "";
        return String(s)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function escAttr(s) {
        return esc(s).replace(/'/g, "&#39;");
    }

    function renderAirmailEdge() {
        return '<div class="dd-airmail" aria-hidden="true"></div>';
    }

    function renderPostmark() {
        return (
            '<div class="dd-postmark" aria-hidden="true">' +
            '<span class="dd-postmark__ring"></span>' +
            '<span class="dd-postmark__waves"></span>' +
            "</div>"
        );
    }

    function renderTracking(category) {
        if (!category) return "";
        return (
            '<div class="dd-tracking">' +
            '<span class="dd-tracking__atelier">106/1 Nguyễn Thị Minh Khai</span>' +
            '<span class="dd-tracking__sep"></span>' +
            '<span class="dd-tracking__cat">' + esc(category.toUpperCase()) + "</span>" +
            "</div>"
        );
    }

    function signatureClass(data) {
        var text = ((data.categoryName || "") + " " + (data.name || "")).toLowerCase();
        if (/(matcha mood|cà phê muối|ca phe muoi|salt coffee|espresso|cold brew|cà phê đen|ca phe den|đen đá|den da)/i.test(text)) {
            return " dd-passport--signature";
        }
        return "";
    }

    function sensoryTags(data) {
        var text = ((data.categoryName || "") + " " + (data.name || "")).toLowerCase();
        if (/(espresso|americano|cold brew|cà phê đen|ca phe den|đen đá|den da|robusta|pour-over|pourover|v60)/i.test(text)) {
            return ["Craft coffee", "Focused clarity", "Concentrated"];
        }
        if (/(latte|mocha|bạc xỉu|bac xiu|caramel|cappuccino|cà phê sữa|ca phe sua)/i.test(text)) {
            return ["Milk-forward", "Soft texture", "Approachable"];
        }
        if (/(matcha)/i.test(text)) {
            return ["Stone-ground calm", "Green silk", "Ceremonial"];
        }
        if (/(juice|smoothie|coco|avocado|trái cây|fruit|cam|apple|táo)/i.test(text)) {
            return ["Fresh lift", "Fruit-forward", "Cooling"];
        }
        if (data.isBakery) return ["Pastry pairing", "Table companion", "Soft finish"];
        return ["House selection", "Balanced", "Annap style"];
    }

    function renderAtelierSeal(data) {
        return (
            '<div class="dd-atelier-seal" aria-hidden="true">' +
            '<span class="dd-atelier-seal__mark">A</span>' +
            '<span class="dd-atelier-seal__copy">106/1<br/>ANNAP</span>' +
            "</div>"
        );
    }

    function renderSensoryMap(data) {
        var tags = sensoryTags(data);
        if (!tags.length) return "";
        var html = tags.map(function (t) {
            return '<span class="dd-sensory-chip">' + esc(t) + "</span>";
        }).join("");
        return (
            '<div class="dd-sensory-map" aria-label="Annap sensory profile">' +
            '<div class="dd-sensory-map__chips">' +
            html +
            "</div></div>"
        );
    }

    function renderTitleBlock(data) {
        var accent = data.accentColor || "#e8c76b";
        return (
            '<header class="dd-title-block">' +
            '<div class="dd-title-block__accent" style="background-color:' +
            escAttr(accent) +
            '" aria-hidden="true"></div>' +
            '<div class="dd-title-block__copy">' +
            '<h1 id="dd-detail-title" class="dd-title">' +
            esc(data.name || "") +
            "</h1>" +
            '<p class="dd-title-block__atelier">Annap Taste Passport · 106/1</p>' +
            "</div></header>"
        );
    }

    function renderManifestSection(i18nKey, fallbackLabel, bodyHtml, modClass) {
        if (!bodyHtml) return "";
        return (
            '<div class="dd-manifest ' + (modClass || "") + '">' +
            '<span class="dd-manifest__lbl" data-i18n="' +
            escAttr(i18nKey) +
            '" aria-hidden="true">' +
            esc(fallbackLabel) +
            "</span>" +
            bodyHtml +
            "</div>"
        );
    }

    function renderProductImage(data) {
        if (!data.image) return renderNoPoster(data);
        return (
            '<figure class="dd-product">' +
            '<img class="dd-product__img" src="' +
            escAttr(data.image) +
            '" alt="' +
            escAttr(data.name || "Drink") +
            '" width="480" height="640" decoding="async" loading="eager" fetchpriority="high" referrerpolicy="no-referrer" onerror="this.onerror=null;this.style.display=\'none\';" />' +
            "</figure>"
        );
    }

    function renderNoPoster(data) {
        var accent = data.accentColor || "#e8c76b";
        return (
            '<div class="dd-no-poster" style="--dd-accent:' +
            escAttr(accent) +
            '" aria-hidden="true">' +
            '<div class="dd-no-poster__wash"></div>' +
            '<span class="dd-no-poster__name">' +
            esc(data.name || "") +
            "</span>" +
            '<span class="dd-no-poster__cat">' +
            esc(data.categoryName ? data.categoryName.toUpperCase() : "") +
            "</span>" +
            "</div>"
        );
    }

    function renderServingNote(note) {
        if (!note) return "";
        return '<p class="dd-serving-note" role="status">' + esc(note) + "</p>";
    }

    function renderPrice(priceDisplay) {
        if (!priceDisplay) return "";
        return '<p class="dd-price" aria-label="Price">' + esc(priceDisplay) + "</p>";
    }

    function renderDecisionBlock(data) {
        if (!data.priceDisplay) return "";
        return (
            '<section class="dd-passport__decision" aria-label="Price">' +
            renderPrice(data.priceDisplay) +
            "</section>"
        );
    }

    function renderPairings(pairings) {
        if (!pairings || !pairings.length) return "";
        var cards = pairings.slice(0, 3).map(function (p) {
            var img =
                p.image && String(p.image).trim()
                    ? '<img class="dd-pairing__img" src="' +
                      escAttr(p.image) +
                      '" alt="" width="72" height="72" decoding="async" loading="lazy" referrerpolicy="no-referrer" onerror="this.onerror=null;this.style.display=\'none\';" />'
                    : '<span class="dd-pairing__ph" aria-hidden="true"></span>';
            return (
                '<button type="button" class="dd-pairing guest-hit" data-pairing-add data-pairing-id="' +
                escAttr(String(p.id || "")) +
                '" data-item-name="' +
                escAttr(p.name || "") +
                '" data-item-price="' +
                escAttr(p.price != null ? String(p.price) : "0") +
                '">' +
                img +
                '<span class="dd-pairing__name">' +
                esc(p.name || "") +
                "</span>" +
                '<span class="dd-pairing__hint" data-i18n="menu.pairingTap">Chạm để thêm</span>' +
                "</button>"
            );
        }).join("");
        return (
            '<section class="dd-pairings" aria-labelledby="dd-pairings-lead">' +
            '<p id="dd-pairings-lead" class="dd-pairings__lead" data-i18n="menu.pairingsLead">We\'d pair this with</p>' +
            '<div class="dd-pairings__strip" role="list">' +
            cards +
            "</div>" +
            "</section>"
        );
    }

    function normalizeInput(raw) {
        if (!raw || typeof raw !== "object") return {};
        return {
            id: raw.id,
            image: raw.image || raw.cardImageUrl || "",
            name: raw.name || "",
            price: raw.price,
            priceDisplay: raw.priceDisplay || "",
            origin: raw.origin || null,
            ingredients: raw.ingredientBreakdown || raw.ingredients || null,
            tastingNotes: raw.tastingNotes || null,
            shortStory: raw.shortStory || null,
            producerStory: raw.producerStory || null,
            subtitle: raw.subtitle || null,
            isSignature: !!raw.isSignature,
            originLetterMode: !!raw.originLetterMode,
            servingNotes: raw.servingNotes || raw.serviceNote || null,
            accentColor: raw.accentColor || "#e8c76b",
            categoryName: raw.categoryName || null,
            canAdd: raw.canAdd !== false,
            serviceNote: raw.serviceNote || raw.servingNotes || null,
            isBakery: !!raw.isBakery,
            pairings: Array.isArray(raw.pairings) ? raw.pairings : []
        };
    }

    function renderOriginLetter(data) {
        var originLine = data.origin || "";
        var title = originLine && data.name ? originLine + " · " + data.name : data.name || "";
        var whyHtml = data.shortStory
            ? '<p class="dd-letter__why">' + esc(data.shortStory) + "</p>"
            : "";
        var producerHtml = data.producerStory
            ? '<p class="dd-letter__producer">' + esc(data.producerStory) + "</p>"
            : "";
        var whisperHtml = data.tastingNotes
            ? '<p class="dd-letter__whisper">' + esc(data.tastingNotes) + "</p>"
            : "";
        var inviteHtml =
            '<p class="dd-letter__invite" data-i18n="ge.originLetter.invite">Khi bạn sẵn sàng — ly đang chờ.</p>';
        return (
            '<article class="dd-passport dd-passport--origin-letter" data-drink-id="' +
            escAttr(String(data.id || "")) +
            '">' +
            renderAirmailEdge() +
            '<div class="dd-passport__inner">' +
            '<header class="dd-letter__head">' +
            '<p class="dd-letter__kicker" data-i18n="ge.originLetter.kicker">Lá thư nguồn gốc</p>' +
            '<h1 id="dd-detail-title" class="dd-letter__title">' +
            esc(title) +
            "</h1>" +
            "</header>" +
            '<section class="dd-letter__body">' +
            whyHtml +
            producerHtml +
            whisperHtml +
            inviteHtml +
            "</section>" +
            '<section class="dd-passport__hero-visual dd-letter__visual">' +
            renderProductImage(data) +
            "</section>" +
            renderDecisionBlock(data) +
            "</div></article>"
        );
    }

    function coffeeToneClass(data) {
        var text = ((data.categoryName || "") + " " + (data.name || "")).toLowerCase();
        if (/(espresso|americano|cold brew|cà phê đen|ca phe den|đen đá|den da|robusta|pour-over|pourover|v60)/i.test(text)) {
            return " dd-passport--craft-coffee";
        }
        if (/(latte|mocha|bạc xỉu|bac xiu|caramel|cappuccino|cà phê sữa|ca phe sua)/i.test(text)) {
            return " dd-passport--comfort-coffee";
        }
        return "";
    }

    function render(raw) {
        var data = normalizeInput(raw);
        if (data.originLetterMode) {
            return renderOriginLetter(data);
        }

        var compositionHtml = data.ingredients
            ? '<p class="dd-composition-line">' + esc(data.ingredients) + "</p>"
            : null;
        var provenanceHtml = data.origin
            ? '<p class="dd-provenance-origin">' + esc(data.origin) + "</p>"
            : null;
        var tastingHtml = data.tastingNotes
            ? '<p class="dd-tasting-body">' + esc(data.tastingNotes) + "</p>"
            : null;

        var hasMeta = !!(compositionHtml || provenanceHtml || tastingHtml);
        var storyHtml = "";
        if (!data.isBakery && (hasMeta || data.serviceNote)) {
            storyHtml =
                '<section class="dd-passport__story">' +
                renderManifestSection("drink.insideGlass", "Inside the glass", compositionHtml, "dd-manifest--composition") +
                renderManifestSection("drink.sourcedFrom", "Sourced from", provenanceHtml, "dd-manifest--provenance") +
                renderManifestSection("drink.onPalate", "On the palate", tastingHtml, "dd-manifest--tasting") +
                renderServingNote(data.serviceNote) +
                "</section>";
        }

        var pairingsHtml =
            !data.isBakery && data.pairings.length ? renderPairings(data.pairings) : "";

        var passportMod =
            (data.isBakery ? " dd-passport--bakery" : "") +
            (!data.isBakery && !hasMeta ? " dd-passport--no-meta" : "") +
            signatureClass(data) +
            coffeeToneClass(data);

        return (
            '<article class="dd-passport' +
            passportMod +
            '" data-drink-id="' +
            escAttr(String(data.id || "")) +
            '">' +
            renderAirmailEdge() +
            '<div class="dd-passport__inner">' +
            '<div class="dd-passport__context">' +
            '<div class="dd-passport__context-deco" aria-hidden="true">' +
            renderPostmark() +
            renderAtelierSeal(data) +
            "</div>" +
            renderTracking(data.categoryName) +
            "</div>" +
            '<section class="dd-passport__identity">' +
            renderTitleBlock(data) +
            renderSensoryMap(data) +
            "</section>" +
            '<section class="dd-passport__hero-visual">' +
            renderProductImage(data) +
            "</section>" +
            storyHtml +
            renderDecisionBlock(data) +
            pairingsHtml +
            "</div></article>"
        );
    }

    function mount(container, raw) {
        if (!container) return null;
        container.innerHTML = render(raw);
        return container.querySelector(".dd-passport");
    }

    function unmount(container) {
        if (!container) return;
        container.querySelectorAll("img").forEach(function (img) {
            img.src = "";
            img.removeAttribute("src");
        });
        container.replaceChildren();
    }

    global.DrinkDetailRenderer = {
        esc: esc,
        render: render,
        mount: mount,
        unmount: unmount,
        normalize: normalizeInput
    };
})(typeof window !== "undefined" ? window : globalThis);
