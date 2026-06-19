(function () {
    /** Two-column floor: inbox (all active) → served (completed). */
    const STAFF_TWO_COLUMN = true;
    const STAFF_FLOW = ["pending", "preparing", "finishing", "ready", "served"];
    const PACING_COPY = {
        steady: "Nhịp ổn định",
        watch: "Đang chú ý",
        brisk: "Bàn giao nhanh"
    };

    let hubConnection = null;
    let hubRefreshTimer = null;
    let backupPollTimer = null;
    let loadInFlight = false;
    let firstPaint = true;
    let previousIds = new Set();
    let elapsedTimer = null;
    let staffIdentityName = null;
    let lastBoardPulseAt = null;

    const el = (sel) => document.querySelector(sel);

    function showError(msg) {
        const box = el("#staff-error");
        if (!box) return;
        if (!msg) {
            box.classList.add("hidden");
            box.textContent = "";
            return;
        }
        box.textContent = msg;
        box.classList.remove("hidden");
    }

    function fmtClock(iso) {
        if (!iso) return "—";
        const d = new Date(iso);
        return d.toLocaleTimeString(undefined, { hour: "numeric", minute: "2-digit" });
    }

    function fmtRel(iso) {
        if (!iso) return "—";
        const t = new Date(iso).getTime();
        if (Number.isNaN(t)) return "—";
        const s = Math.max(0, Math.floor((Date.now() - t) / 1000));
        if (s < 60) return `${s}s`;
        if (s < 3600) return `${Math.floor(s / 60)}m`;
        return `${Math.floor(s / 3600)}h`;
    }

    function escapeHtml(s) {
        return String(s)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function colBody(status) {
        return document.querySelector(`[data-staff-drop="${status}"]`);
    }

    function isActiveStaffStatus(st) {
        return st && st !== "served" && st !== "cancelled";
    }

    function nextStaff(st) {
        if (STAFF_TWO_COLUMN) {
            if (!isActiveStaffStatus(st)) return null;
            return "served";
        }
        const i = STAFF_FLOW.indexOf(st);
        if (i < 0 || i >= STAFF_FLOW.length - 1) return null;
        return STAFF_FLOW[i + 1];
    }

    function prevStaff(st) {
        if (STAFF_TWO_COLUMN) return null;
        const i = STAFF_FLOW.indexOf(st);
        if (i <= 0) return null;
        return STAFF_FLOW[i - 1];
    }

    function moneyFmt(v) {
        return new Intl.NumberFormat(undefined, { style: "currency", currency: "USD", maximumFractionDigits: 2 }).format(v);
    }

    function pacingClass(key) {
        return key === "watch" || key === "brisk" ? key : "steady";
    }

    function ownershipChipsHtml(o) {
        if (STAFF_TWO_COLUMN) return "";
        const me = staffIdentityName;
        if (!me || !o.id) return "";
        const st = o.staffStatus;
        const id = String(o.id);
        const bits = [];
        if (st === "preparing" || st === "finishing") {
            if (o.brewingOwner === me)
                bits.push(
                    `<button type="button" class="staff-own-chip guest-hit" data-staff-act="release-brew" data-order-id="${id}">Thả quầy</button>`
                );
            else if (!o.brewingOwner)
                bits.push(
                    `<button type="button" class="staff-own-chip guest-hit" data-staff-act="claim-brew" data-order-id="${id}">Giữ quầy</button>`
                );
        }
        if (st === "ready") {
            if (o.servingOwner === me)
                bits.push(
                    `<button type="button" class="staff-own-chip guest-hit" data-staff-act="release-serve" data-order-id="${id}">Thả bàn giao</button>`
                );
            else if (!o.servingOwner)
                bits.push(
                    `<button type="button" class="staff-own-chip guest-hit" data-staff-act="claim-serve" data-order-id="${id}">Nhận bàn giao</button>`
                );
        }
        return bits.join("");
    }

    function buildCardHtml(o, opts) {
        const servedCol = !!opts?.servedColumn;
        const phase = escapeHtml(o.phaseLabel || o.staffStatus || "");
        const pacingKey = pacingClass(o.pacing);
        const pacingLabel = PACING_COPY[pacingKey] || PACING_COPY.steady;
        const notes = o.guestNotes
            ? `<p class="staff-order-note staff-order-note--ticket mt-3">Ghi chú · ${escapeHtml(o.guestNotes)}</p>`
            : "";
        const cups = typeof o.totalCups === "number" ? o.totalCups : (o.items || []).reduce((a, it) => a + (Number(it.quantity) || 0), 0);
        const lines = (o.items || [])
            .map((it) => {
                const note = it.notes
                    ? `<p class="staff-order-note staff-order-note--item mt-1">Ghi chú: ${escapeHtml(it.notes)}</p>`
                    : "";
                return `<li class="border-b border-white/[0.05] py-2.5 last:border-0 last:pb-0">
                        <div class="flex items-baseline justify-between gap-2">
                            <span class="min-w-0 truncate font-medium text-[rgb(var(--fg))]">${escapeHtml(it.name)}</span>
                            <span class="shrink-0 tabular-nums text-xs text-[rgb(var(--muted))]">×${it.quantity}</span>
                        </div>${note}</li>`;
            })
            .join("");
        const totalLine =
            o.totalAmount != null
                ? `<p class="mt-3 text-right text-xs tracking-wide text-[rgb(var(--muted))]">Tổng <span class="font-medium tabular-nums text-[rgb(var(--fg))]">${moneyFmt(o.totalAmount)}</span></p>`
                : "";
        const hasPrev = !!prevStaff(o.staffStatus);
        const stepIso = o.statusChangedAtUtc || o.updatedAtUtc || o.createdAtUtc;
        const qa = Number(o.queueAhead) || 0;
        const queueLine =
            qa > 0
                ? `<p data-field="queue-line" class="mt-1 text-[10px] tracking-[0.16em] text-[rgb(var(--muted-2))]">Hàng chờ yên tĩnh · ${qa} phiếu phía trước</p>`
                : `<p data-field="queue-line" class="mt-1 hidden text-[10px] tracking-[0.16em] text-[rgb(var(--muted-2))]"></p>`;
        const brew = o.brewingOwner ? escapeHtml(o.brewingOwner) : "";
        const serv = o.servingOwner ? escapeHtml(o.servingOwner) : "";
        const ownBits = [];
        if (brew) ownBits.push(`Quầy pha · ${brew}`);
        if (serv) ownBits.push(`Bàn giao · ${serv}`);
        const ownLine =
            ownBits.length > 0
                ? `<p data-field="ownership-line" class="mt-2 text-[10px] leading-relaxed tracking-[0.14em] text-[rgb(var(--muted-2))]">${ownBits.join(" · ")}</p>`
                : `<p data-field="ownership-line" class="mt-2 hidden text-[10px] text-[rgb(var(--muted-2))]"></p>`;

        const ownWrapInner = !servedCol ? ownershipChipsHtml(o) : "";

        const advanceLabel = STAFF_TWO_COLUMN && !servedCol ? "Hoàn thành" : "Tiếp tục";
        const ops = servedCol
            ? ""
            : `<div class="staff-order-card__ops mt-4 flex flex-col gap-2">
                    <div class="flex flex-wrap items-stretch gap-2">
                    <button type="button" class="staff-order-advance guest-hit" data-staff-act="advance" data-order-id="${o.id}">${advanceLabel}</button>
                    <button type="button" class="staff-order-back guest-hit" data-staff-act="back" data-order-id="${o.id}"${hasPrev ? "" : " hidden"}>Lùi bước</button>
                    </div>
                    <div data-field="own-wrap" class="flex flex-wrap gap-2">${ownWrapInner}</div>
               </div>`;
        return `<article class="staff-order-card surface-glass relative overflow-hidden rounded-2xl p-4 ring-1 ring-white/[0.07]"
                data-order-id="${o.id}" data-staff-status="${o.staffStatus}" data-created-at="${o.createdAtUtc || ""}" data-updated-at="${o.updatedAtUtc || ""}" data-status-changed-at="${stepIso || ""}">
                <div class="flex flex-wrap items-start justify-between gap-3">
                    <div>
                        <p class="text-[10px] font-medium tracking-[0.28em] uppercase text-[rgb(var(--muted-2))]">Bàn</p>
                        <p class="mt-1 font-display text-3xl leading-none tracking-tight text-[rgb(var(--fg))]">${escapeHtml(o.tableCode)}</p>
                    </div>
                    <div class="text-right text-xs tabular-nums text-[rgb(var(--muted))]">
                        <p>Kể từ khi đặt <span data-field="elapsed" class="text-[rgb(var(--fg))]">${fmtRel(o.createdAtUtc)}</span></p>
                        <p class="mt-0.5">Bước này <span data-field="step-elapsed" class="text-[rgb(var(--fg))]">${fmtRel(stepIso)}</span></p>
                        <p class="mt-1">${fmtClock(o.createdAtUtc)}</p>
                        ${o.updatedAtUtc ? `<p class="mt-2 text-[10px] uppercase tracking-wider text-[rgb(var(--muted-2))]">Cập nhật ${fmtClock(o.updatedAtUtc)}</p>` : ""}
                    </div>
                </div>
                <div class="mt-3 flex flex-wrap items-center gap-3">
                    <span class="rounded-full bg-white/[0.05] px-3 py-1 text-[10px] font-medium uppercase tracking-[0.2em] text-[rgb(var(--fg))] ring-1 ring-white/[0.08]">${phase}</span>
                    <span class="staff-pacing staff-pacing--${pacingKey}" data-field="pacing-wrap">
                        <span class="staff-pacing__dot" aria-hidden="true"></span>
                        <span data-field="pacing-label">${escapeHtml(pacingLabel)}</span>
                    </span>
                    <span class="text-[10px] uppercase tracking-[0.22em] text-[rgb(var(--muted-2))]">${cups} ly</span>
                </div>
                ${queueLine}
                ${ownLine}
                <ul class="mt-4 list-none border-t border-white/[0.06] text-sm">${lines || '<li class="py-3 text-[rgb(var(--muted))]">Không có món</li>'}</ul>
                ${notes}
                ${totalLine}
                ${ops}
            </article>`;
    }

    function renderEmptyNode(text) {
        const p = document.createElement("p");
        p.className =
            "staff-col-empty rounded-xl bg-white/[0.03] px-3 py-10 text-center text-sm text-[rgb(var(--muted))] ring-1 ring-white/[0.05]";
        p.textContent = text;
        return p;
    }

    function clearEmptyPlaceholders(body) {
        body.querySelectorAll(".staff-col-empty").forEach((n) => n.remove());
    }

    function ensureEmpty(body, text) {
        if (body.querySelector("article.staff-order-card")) return;
        if (body.querySelector(".staff-col-empty")) return;
        body.appendChild(renderEmptyNode(text));
    }

    function updateCardContent(card, o) {
        const pacingKey = pacingClass(o.pacing);
        const pacingLabel = PACING_COPY[pacingKey] || PACING_COPY.steady;
        const rel = card.querySelector('[data-field="elapsed"]');
        if (rel) rel.textContent = fmtRel(o.createdAtUtc);
        const stepIso = o.statusChangedAtUtc || o.updatedAtUtc || o.createdAtUtc;
        const stEl = card.querySelector('[data-field="step-elapsed"]');
        if (stEl) stEl.textContent = fmtRel(stepIso);
        card.dataset.statusChangedAt = stepIso || "";
        const pl = card.querySelector('[data-field="pacing-label"]');
        if (pl) pl.textContent = pacingLabel;
        const pw = card.querySelector('[data-field="pacing-wrap"]');
        if (pw) {
            pw.className = `staff-pacing staff-pacing--${pacingKey}`;
        }
        const ql = card.querySelector('[data-field="queue-line"]');
        if (ql) {
            const qa = Number(o.queueAhead) || 0;
            if (qa > 0) {
                ql.textContent = `Hàng chờ yên tĩnh · ${qa} phiếu phía trước`;
                ql.classList.remove("hidden");
            } else {
                ql.textContent = "";
                ql.classList.add("hidden");
            }
        }
        const ownLine = card.querySelector('[data-field="ownership-line"]');
        if (ownLine) {
            const brew = o.brewingOwner ? String(o.brewingOwner) : "";
            const serv = o.servingOwner ? String(o.servingOwner) : "";
            const parts = [];
            if (brew) parts.push(`Machine · ${brew}`);
            if (serv) parts.push(`Handoff · ${serv}`);
            if (parts.length) {
                ownLine.textContent = parts.join(" · ");
                ownLine.classList.remove("hidden");
            } else {
                ownLine.textContent = "";
                ownLine.classList.add("hidden");
            }
        }
        const ow = card.querySelector('[data-field="own-wrap"]');
        if (ow) ow.innerHTML = ownershipChipsHtml(o);
        card.dataset.staffStatus = o.staffStatus;
        card.dataset.createdAt = o.createdAtUtc || "";
        card.dataset.updatedAt = o.updatedAtUtc || "";
        const back = card.querySelector('[data-staff-act="back"]');
        if (back) {
            if (prevStaff(o.staffStatus)) back.removeAttribute("hidden");
            else back.setAttribute("hidden", "hidden");
        }
    }

    function syncColumn(board, status, orders, emptyText, isServed) {
        const body = colBody(status);
        if (!body) return;
        clearEmptyPlaceholders(body);
        const sorted = [...orders].sort((a, b) => String(a.createdAtUtc).localeCompare(String(b.createdAtUtc)));
        sorted.forEach((o, idx) => {
            o.queueAhead = idx;
        });
        const desiredIds = sorted.map((o) => o.id);
        const byId = Object.fromEntries(sorted.map((o) => [o.id, o]));

        for (const id of desiredIds) {
            const o = byId[id];
            let card = board.querySelector(`article.staff-order-card[data-order-id="${id}"]`);
            if (!card) {
                const wrap = document.createElement("div");
                wrap.innerHTML = buildCardHtml(o, { servedColumn: isServed }).trim();
                card = wrap.firstElementChild;
                const isNew = previousIds.size > 0 && !previousIds.has(id);
                body.appendChild(card);
                if (isNew) {
                    card.classList.add("staff-order-card--enter");
                    window.setTimeout(() => card.classList.remove("staff-order-card--enter"), 700);
                }
            } else {
                updateCardContent(card, o);
                if (card.parentNode !== body) {
                    body.appendChild(card);
                    card.classList.add("staff-order-card--shift");
                    window.setTimeout(() => card.classList.remove("staff-order-card--shift"), 450);
                }
            }
        }

        for (const id of desiredIds) {
            const card = board.querySelector(`article.staff-order-card[data-order-id="${id}"]`);
            if (card && card.parentNode === body) updateCardContent(card, byId[id]);
        }

        for (const id of desiredIds) {
            const card = board.querySelector(`article.staff-order-card[data-order-id="${id}"]`);
            if (card && card.parentNode === body) body.appendChild(card);
        }

        if (!body.querySelector("article.staff-order-card")) {
            ensureEmpty(body, emptyText);
        }
    }

    function collectAllIds(data) {
        const s = new Set();
        (data.active || []).forEach((o) => s.add(o.id));
        (data.recentServed || []).forEach((o) => s.add(o.id));
        return s;
    }

    function removeOrphanCards(board, allIds) {
        board.querySelectorAll("article.staff-order-card").forEach((c) => {
            const id = c.getAttribute("data-order-id");
            if (!allIds.has(id)) {
                c.classList.add("staff-order-card--exit");
                window.setTimeout(() => {
                    if (c.parentNode) c.parentNode.removeChild(c);
                }, 380);
            }
        });
    }

    function mergeBoard(data) {
        const board = el("[data-staff-board]");
        if (!board) return;

        staffIdentityName = (data.youAre && String(data.youAre).trim()) || null;
        if (STAFF_TWO_COLUMN) {
            const activeInbox = [];
            for (const o of data.active || []) {
                if (o.staffStatus === "cancelled") continue;
                activeInbox.push(o);
            }
            syncColumn(board, "pending", activeInbox, "Khoảnh khắc yên lặng — chưa có phiếu đang mở.", false);
            syncColumn(board, "served", data.recentServed || [], "Không có đơn nào gần đây.", true);
        } else {
            const activeBy = { pending: [], preparing: [], finishing: [], ready: [] };
            for (const o of data.active || []) {
                const st = o.staffStatus;
                if (st === "cancelled") continue;
                if (activeBy[st]) activeBy[st].push(o);
                else activeBy.pending.push(o);
            }

            syncColumn(board, "pending", activeBy.pending, "Khoảnh khắc yên lặng — chưa có gì mới.", false);
            syncColumn(board, "preparing", activeBy.preparing, "Không có phiếu nào tại quầy.", false);
            syncColumn(board, "finishing", activeBy.finishing, "Không có gì đang hoàn thiện.", false);
            syncColumn(board, "ready", activeBy.ready, "Không có bàn giao nào đang chờ.", false);
            syncColumn(board, "served", data.recentServed || [], "Không có đơn nào gần đây.", true);
        }

        const all = collectAllIds(data);
        removeOrphanCards(board, all);
        previousIds = all;

        window.setTimeout(() => {
            const emptyCopy = STAFF_TWO_COLUMN
                ? {
                      pending: "Khoảnh khắc yên lặng — chưa có phiếu đang mở.",
                      served: "Không có đơn nào gần đây."
                  }
                : {
                      pending: "Khoảnh khắc yên lặng — chưa có gì mới.",
                      preparing: "Không có phiếu nào tại quầy.",
                      finishing: "Không có gì đang hoàn thiện.",
                      ready: "Không có bàn giao nào đang chờ.",
                      served: "Không có đơn nào gần đây."
                  };
            const cols = STAFF_TWO_COLUMN ? ["pending", "served"] : ["pending", "preparing", "finishing", "ready", "served"];
            for (const st of cols) {
                const b = colBody(st);
                if (b && !b.querySelector("article.staff-order-card")) {
                    ensureEmpty(b, emptyCopy[st]);
                }
            }
        }, 420);
    }

    function setSkeletons(visible) {
        document.querySelectorAll(".staff-col-skeleton").forEach((sk) => {
            if (visible) sk.classList.add("is-visible");
            else {
                sk.classList.remove("is-visible");
                sk.remove();
            }
        });
    }

    function updateElapsedAll() {
        const board = el("[data-staff-board]");
        if (!board) return;
        board.querySelectorAll("article.staff-order-card").forEach((card) => {
            const iso = card.getAttribute("data-created-at");
            const rel = card.querySelector('[data-field="elapsed"]');
            if (rel && iso) rel.textContent = fmtRel(iso);
            const stepIso = card.getAttribute("data-status-changed-at");
            const stEl = card.querySelector('[data-field="step-elapsed"]');
            if (stEl && stepIso) stEl.textContent = fmtRel(stepIso);
        });
    }

    function setHubUi(state) {
        const dot = el(".staff-pulse-dot");
        const label = el("#staff-last-sync");
        if (dot) {
            dot.classList.remove("staff-sync-dot--live", "staff-sync-dot--soft", "staff-sync-dot--reconnect");
            if (state === "connected") dot.classList.add("staff-sync-dot--live");
            else if (state === "reconnecting") dot.classList.add("staff-sync-dot--reconnect");
            else dot.classList.add("staff-sync-dot--soft");
        }
        if (label) {
            if (state === "connected") label.textContent = "Trực tiếp · đồng bộ sàn";
            else if (state === "reconnecting") label.textContent = "Đang kết nối lại…";
            else if (state === "connecting") label.textContent = "Đang kết nối…";
            else label.textContent = "Chế độ yên lặng · làm mới nếu cần";
        }
        try {
            const d = window.AnnapGuestLanDebug;
            if (d && typeof d.setHub === "function") {
                const map = { connected: "Connected", reconnecting: "Reconnecting", connecting: "Connecting", offline: "Offline" };
                d.setHub(map[state] || state);
            }
        } catch {
            /* ignore */
        }
    }

    async function loadBoard(opts) {
        const userRefresh = !!(opts && opts.user);
        if (loadInFlight) return;
        loadInFlight = true;
        if (userRefresh) showError("");
        try {
            const res = await fetch(typeof window.__annapApiUrl === "function" ? window.__annapApiUrl("/api/staff/orders") : "/api/staff/orders", { headers: { Accept: "application/json" }, credentials: "same-origin" });
            if (!res.ok) throw new Error("Không thể tải đơn hàng.");
            const data = await res.json();
            mergeBoard(data);
            if (firstPaint) {
                firstPaint = false;
                setSkeletons(false);
            }
            const stamp = el("#staff-last-updated");
            if (stamp) stamp.textContent = fmtClock(new Date().toISOString());
        } catch (e) {
            showError(e.message || "Lỗi mạng.");
        } finally {
            loadInFlight = false;
        }
    }

    function scheduleHubRefresh() {
        if (hubRefreshTimer) window.clearTimeout(hubRefreshTimer);
        hubRefreshTimer = window.setTimeout(() => {
            hubRefreshTimer = null;
            loadBoard({});
        }, 280);
    }

    async function patchStatus(orderId, nextStaffStatus, cardEl) {
        if (!orderId || !nextStaffStatus) return;
        const prevStatus = cardEl ? cardEl.getAttribute("data-staff-status") : null;
        const prevParent = cardEl ? cardEl.parentNode : null;
        if (cardEl) {
            cardEl.classList.add("staff-order-card--syncing");
            const dropKey =
                STAFF_TWO_COLUMN && nextStaffStatus === "served"
                    ? "served"
                    : STAFF_TWO_COLUMN && isActiveStaffStatus(nextStaffStatus)
                      ? "pending"
                      : nextStaffStatus;
            const body = colBody(dropKey);
            if (body && cardEl.parentNode !== body) {
                clearEmptyPlaceholders(body);
                body.appendChild(cardEl);
                cardEl.classList.add("staff-order-card--shift");
                window.setTimeout(() => cardEl.classList.remove("staff-order-card--shift"), 450);
            }
            cardEl.setAttribute("data-staff-status", nextStaffStatus);
        }
        try {
            const res = await fetch(
                (typeof window.__annapApiUrl === "function"
                    ? window.__annapApiUrl("/api/staff/orders/" + orderId + "/status")
                    : "/api/staff/orders/" + orderId + "/status"),
                {
                method: "PATCH",
                headers: { "Content-Type": "application/json", Accept: "application/json" },
                credentials: "same-origin",
                body: JSON.stringify({ staffStatus: nextStaffStatus })
            });
            const j = await res.json().catch(() => null);
            if (!res.ok) {
                const msg = j?.error || "Update failed.";
                if (res.status === 409) {
                    showError(msg);
                    if (cardEl) {
                        cardEl.classList.remove("staff-order-card--syncing");
                        if (prevParent && prevStatus) {
                            cardEl.setAttribute("data-staff-status", prevStatus);
                            prevParent.appendChild(cardEl);
                        }
                    }
                    await loadBoard({});
                    return;
                }
                throw new Error(msg);
            }
            if (cardEl) cardEl.classList.remove("staff-order-card--syncing");
            await loadBoard({});
        } catch (e) {
            showError(e.message || "Update failed.");
            if (cardEl) {
                cardEl.classList.remove("staff-order-card--syncing");
                if (prevParent && prevStatus) {
                    cardEl.setAttribute("data-staff-status", prevStatus);
                    prevParent.appendChild(cardEl);
                }
            }
            await loadBoard({});
        }
    }

    async function patchOwnership(orderId, body) {
        if (!orderId) return;
        try {
            const res = await fetch(
                (typeof window.__annapApiUrl === "function"
                    ? window.__annapApiUrl("/api/staff/orders/" + orderId + "/ownership")
                    : "/api/staff/orders/" + orderId + "/ownership"),
                {
                method: "PATCH",
                headers: { "Content-Type": "application/json", Accept: "application/json" },
                credentials: "same-origin",
                body: JSON.stringify(body)
            });
            const j = await res.json().catch(() => null);
            if (!res.ok) {
                const msg = j?.error || "Could not update who holds this ticket.";
                if (res.status === 409) {
                    showError(msg);
                    await loadBoard({});
                    return;
                }
                throw new Error(msg);
            }
            await loadBoard({});
        } catch (e) {
            showError(e.message || "Update failed.");
        }
    }

    function startBackupPoll() {
        stopBackupPoll();
        const cb = el("#staff-backup-poll");
        if (cb?.checked) backupPollTimer = window.setInterval(() => loadBoard({}), 60000);
    }

    function stopBackupPoll() {
        if (backupPollTimer) {
            window.clearInterval(backupPollTimer);
            backupPollTimer = null;
        }
    }

    async function startHub() {
        if (!window.signalR) {
            setHubUi("offline");
            return;
        }
        setHubUi("connecting");
        try {
            hubConnection = new signalR.HubConnectionBuilder()
                .withUrl(typeof window.__annapHubUrl === "function" ? window.__annapHubUrl() : "/hubs/orders")
                .withAutomaticReconnect([0, 3000, 8000, 20000, 45000, 60000])
                .build();
            hubConnection.on("boardRefresh", (payload) => {
                const at = payload && typeof payload.atUtc === "number" ? payload.atUtc : null;
                if (at != null && lastBoardPulseAt === at) return;
                lastBoardPulseAt = at;
                showError("");
                scheduleHubRefresh();
            });
            hubConnection.onreconnecting(() => setHubUi("reconnecting"));
            hubConnection.onreconnected(async () => {
                setHubUi("connected");
                lastBoardPulseAt = null;
                try {
                    await hubConnection.invoke("JoinStaffBoard");
                } catch {
                    /* ignore */
                }
                await loadBoard({});
            });
            hubConnection.onclose(() => setHubUi("offline"));
            await new Promise((resolve, reject) => {
                const t = setTimeout(() => reject(new Error("SignalR start timeout")), 22000);
                hubConnection
                    .start()
                    .then(() => {
                        clearTimeout(t);
                        resolve();
                    })
                    .catch((e) => {
                        clearTimeout(t);
                        reject(e);
                    });
            });
            await hubConnection.invoke("JoinStaffBoard");
            setHubUi("connected");
            console.info("[annap] staff SignalR connected");
        } catch (e) {
            console.warn("[annap] staff SignalR failed", e);
            hubConnection = null;
            setHubUi("offline");
        }
    }

    function wireUi() {
        el("#staff-refresh-btn")?.addEventListener("click", () => loadBoard({ user: true }));
        el("#staff-backup-poll")?.addEventListener("change", () => {
            if (el("#staff-backup-poll")?.checked) startBackupPoll();
            else stopBackupPoll();
        });
        document.addEventListener("visibilitychange", () => {
            if (document.visibilityState === "visible") loadBoard({});
        });

        document.querySelector("[data-staff-board]")?.addEventListener("click", (e) => {
            const actEl = e.target.closest("[data-staff-act]");
            if (actEl) {
                const act = actEl.getAttribute("data-staff-act");
                const oid = actEl.getAttribute("data-order-id");
                if (act === "claim-brew" && oid) {
                    void patchOwnership(oid, { claimBrewing: true });
                    return;
                }
                if (act === "release-brew" && oid) {
                    void patchOwnership(oid, { releaseBrewing: true });
                    return;
                }
                if (act === "claim-serve" && oid) {
                    void patchOwnership(oid, { claimServing: true });
                    return;
                }
                if (act === "release-serve" && oid) {
                    void patchOwnership(oid, { releaseServing: true });
                    return;
                }
            }
            const adv = e.target.closest('[data-staff-act="advance"]');
            if (adv) {
                const id = adv.getAttribute("data-order-id");
                const board = el("[data-staff-board]");
                const card = board?.querySelector(`article.staff-order-card[data-order-id="${id}"]`);
                const cur = card?.getAttribute("data-staff-status");
                const n = cur ? nextStaff(cur) : null;
                if (id && n) void patchStatus(id, n, card || null);
                return;
            }
            const back = e.target.closest('[data-staff-act="back"]');
            if (back) {
                const id = back.getAttribute("data-order-id");
                const board = el("[data-staff-board]");
                const card = board?.querySelector(`article.staff-order-card[data-order-id="${id}"]`);
                const cur = card?.getAttribute("data-staff-status");
                const p = cur ? prevStaff(cur) : null;
                if (id && p) void patchStatus(id, p, card || null);
            }
        });
    }

    function boot() {
        if (!document.querySelector("[data-staff-board]")) return;
        setSkeletons(true);
        wireUi();
        void loadBoard({});
        void startHub();
        startBackupPoll();
        if (elapsedTimer) window.clearInterval(elapsedTimer);
        elapsedTimer = window.setInterval(updateElapsedAll, 10000);
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", boot);
    else boot();
})();
