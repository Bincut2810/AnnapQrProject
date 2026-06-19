/**
 * Menu curation — live preview, dirty strip, dropzone affordance, reduced motion aware.
 */
(function () {
    "use strict";

    function prefersReducedMotion() {
        try {
            return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        } catch {
            return false;
        }
    }

    function parseCategoryLabels(form) {
        try {
            var raw = form.getAttribute("data-category-labels");
            return raw ? JSON.parse(raw) : {};
        } catch {
            return {};
        }
    }

    function formatMoney(n) {
        try {
            return new Intl.NumberFormat(undefined, { style: "currency", currency: "USD" }).format(Number(n) || 0);
        } catch {
            return String(n);
        }
    }

    function isBakeryCategoryName(name) {
        if (!name) return false;
        var t = String(name).trim().toLowerCase();
        return t === "bánh" || t === "banh" || t.indexOf("bánh") >= 0;
    }

    function bindMenuCurationForm(form) {
        if (!form || !form.hasAttribute("data-menu-curation-form")) return;

        var strip = document.getElementById("menu-curation-dirty-state");
        var stripWrap = document.getElementById("menu-curation-save-strip");
        var categoryLabels = parseCategoryLabels(form);
        var dirty = false;

        function setDirty(v) {
            dirty = v;
            if (!strip || !stripWrap) return;
            strip.textContent = v ? "Chưa lưu thay đổi" : "Đang yên";
            stripWrap.classList.toggle("menu-curation-save-strip--dirty", v);
            stripWrap.classList.toggle("menu-curation-save-strip--calm", !v);
        }

        form.addEventListener("input", function () {
            setDirty(true);
        });
        form.addEventListener("change", function () {
            setDirty(true);
        });

        form.addEventListener("submit", function () {
            if (strip) strip.textContent = "Đang lưu…";
            if (stripWrap) {
                stripWrap.classList.add("menu-curation-save-strip--saving");
                stripWrap.classList.remove("menu-curation-save-strip--dirty");
            }
        });

        var elName = form.querySelector("[data-preview='name']");
        var elSubtitle = form.querySelector("[data-preview='subtitle']");
        var elTasting = form.querySelector("[data-preview='tasting']");
        var elPrice = form.querySelector("[data-preview='price']");
        var elCategory = form.querySelector("[data-preview='category']");
        var elAvail = form.querySelector("[data-preview='available']");
        var formRoot = form.querySelector(".menu-curation-form") || form;
        var img = document.getElementById("menu-preview-img");
        var eyebrow = document.getElementById("menu-preview-eyebrow");
        var pvName = document.getElementById("menu-preview-name");
        var pvSubtitle = document.getElementById("menu-preview-subtitle");
        var pvTasting = document.getElementById("menu-preview-tasting");
        var pvPrice = document.getElementById("menu-preview-price");
        var pvAvail = document.getElementById("menu-preview-avail");
        var fileInput = form.querySelector("[data-preview-file]");
        var removeCb = form.querySelector("input[name='Input.RemoveHeroImage']");
        var objectUrl = null;

        function syncBakeryMode() {
            if (!elCategory || !formRoot) return;
            var label = categoryLabels[elCategory.value] || "";
            var opt = elCategory.options[elCategory.selectedIndex];
            if (opt && opt.textContent) label = opt.textContent.trim();
            formRoot.classList.toggle("menu-curation-form--bakery", isBakeryCategoryName(label));
        }

        function sync() {
            if (pvName && elName) pvName.textContent = elName.value.trim() || "Ly chưa đặt tên";
            if (pvSubtitle && elSubtitle) {
                var s = elSubtitle.value.trim();
                pvSubtitle.textContent = s || "—";
            }
            if (pvTasting && elTasting) {
                var t = elTasting.value.trim();
                pvTasting.textContent = t || "Ghi chú nếm sẽ hiện ở đây.";
            }
            if (pvPrice && elPrice) pvPrice.textContent = formatMoney(elPrice.value);
            if (eyebrow && elCategory) {
                var id = elCategory.value;
                eyebrow.textContent = categoryLabels[id] || "—";
            }
            if (pvAvail && elAvail) {
                var on = elAvail.checked;
                pvAvail.textContent = on ? "Đang phục vụ" : "Tạm nghỉ";
                pvAvail.className =
                    "admin-badge " + (on ? "admin-badge--sage" : "admin-badge--mist");
            }
            syncBakeryMode();
        }

        ["input", "change"].forEach(function (ev) {
            form.addEventListener(
                ev,
                function (e) {
                    if (!e.target || !e.target.closest) return;
                    if (e.target.closest("[data-preview], [data-preview-file], [data-preview='available']"))
                        sync();
                },
                true
            );
        });

        if (fileInput && img) {
            fileInput.addEventListener("change", function () {
                if (objectUrl) {
                    URL.revokeObjectURL(objectUrl);
                    objectUrl = null;
                }
                var hint = fileInput.closest("[data-dropzone]");
                if (fileInput.files && fileInput.files[0]) {
                    objectUrl = URL.createObjectURL(fileInput.files[0]);
                    img.src = objectUrl;
                    if (hint) {
                        var sub = hint.querySelector(".menu-curation-dropzone__hint");
                        if (sub) sub.textContent = "Đã chọn — ảnh sẽ được tối ưu khi lưu.";
                    }
                } else if (removeCb && removeCb.checked) {
                    /* let server render handle on reload */
                }
                sync();
            });
        }

        if (removeCb && img) {
            removeCb.addEventListener("change", function () {
                if (removeCb.checked && fileInput && !fileInput.files?.length) {
                    var fb = img.getAttribute("data-fallback");
                    if (fb) img.src = fb;
                }
            });
        }

        var dz = form.querySelector("[data-dropzone]");
        if (dz && fileInput && !prefersReducedMotion()) {
            ;["dragenter", "dragover"].forEach(function (ev) {
                dz.addEventListener(ev, function (e) {
                    e.preventDefault();
                    dz.classList.add("menu-curation-dropzone--active");
                });
            });
            ;["dragleave", "drop"].forEach(function (ev) {
                dz.addEventListener(ev, function (e) {
                    e.preventDefault();
                    dz.classList.remove("menu-curation-dropzone--active");
                });
            });
            dz.addEventListener("drop", function (e) {
                var f = e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files[0];
                if (!f || !String(f.type || "").startsWith("image/")) return;
                try {
                    var dt = new DataTransfer();
                    dt.items.add(f);
                    fileInput.files = dt.files;
                    fileInput.dispatchEvent(new Event("change", { bubbles: true }));
                } catch {
                    /* ignore */
                }
            });
        }

        sync();
    }

    document.addEventListener("DOMContentLoaded", function () {
        var form = document.getElementById("menu-curation-form");
        bindMenuCurationForm(form);
    });
})();
