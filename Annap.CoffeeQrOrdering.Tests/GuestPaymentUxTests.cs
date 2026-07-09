namespace Annap.CoffeeQrOrdering.Tests;

public sealed class GuestPaymentUxTests
{
    private static readonly string WebRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "wwwroot"));

    [Fact]
    public void Tray_polls_guest_track_json_api_not_html_track_page()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        Assert.Contains("fetchGuestOrderStatus", js, StringComparison.Ordinal);
        Assert.Contains("/api/track/orders/", js, StringComparison.Ordinal);
        Assert.Contains("cache: \"no-store\"", js, StringComparison.Ordinal);

        var refreshStart = js.IndexOf("async function refreshSubmittedTrayStatus", StringComparison.Ordinal);
        var refreshEnd = js.IndexOf("function updateTraySheetForState", StringComparison.Ordinal);
        var refreshBlock = js[refreshStart..refreshEnd];
        Assert.Contains("fetchGuestOrderStatus(sess)", refreshBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("buildTrackHref(sess)", refreshBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Guest_i18n_bundles_use_cache_busting_and_no_store()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "guest-i18n.js"));
        Assert.Contains("I18N_BUNDLE_REV", js, StringComparison.Ordinal);
        Assert.Contains("cache: \"no-store\"", js, StringComparison.Ordinal);
        Assert.Contains("/i18n/guest-", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Guest_track_api_url_helper_exists()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "guest-interaction-contract.js"));
        Assert.Contains("buildGuestTrackApiUrl", js, StringComparison.Ordinal);
        Assert.Contains("/api/track/orders/", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Paid_detection_helper_recognizes_paid_preparing_payload()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        Assert.Contains("function isOrderPaidForGuest", js, StringComparison.Ordinal);
        Assert.Contains("paid_preparing", js, StringComparison.Ordinal);
        Assert.Contains("paidAtUtc", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Bank_transfer_qr_uses_updated_keep_screen_open_copy()
    {
        var vi = File.ReadAllText(Path.Combine(WebRoot, "i18n", "guest-vi.json"));
        Assert.Contains("giữ nguyên màn hình chuyển khoản", vi, StringComparison.Ordinal);
        Assert.DoesNotContain("giữ nguyên màn hình này sau khi chuyển khoản", vi, StringComparison.Ordinal);

        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "guest-bank-transfer.js"));
        Assert.Contains("checkout.bankTransferKeepOpen", js, StringComparison.Ordinal);
        Assert.Contains("giữ nguyên màn hình chuyển khoản", js, StringComparison.Ordinal);
        Assert.DoesNotContain("giữ nguyên màn hình này", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Bank_transfer_preview_uses_updated_keep_screen_open_copy()
    {
        var vi = File.ReadAllText(Path.Combine(WebRoot, "i18n", "guest-vi.json"));
        Assert.Contains(
            "Sau khi chuyển khoản, vui lòng giữ nguyên màn hình chuyển khoản để nhân viên ra kiểm tra.",
            vi,
            StringComparison.Ordinal);

        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        Assert.Contains("checkout.bankPreviewKeepOpen", js, StringComparison.Ordinal);
        Assert.Contains("giữ nguyên màn hình chuyển khoản", js, StringComparison.Ordinal);
        Assert.Contains("\"bankTransferPendingBodyShort\": \"Quét mã QR để thanh toán.\"", vi, StringComparison.Ordinal);
    }

    [Fact]
    public void Cash_and_card_preview_does_not_use_bank_transfer_keep_open_copy()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var start = js.IndexOf("function updatePaymentPreviewUi", StringComparison.Ordinal);
        var end = js.IndexOf("function scrollCheckoutCtaIntoView", StringComparison.Ordinal);
        var block = js[start..end];
        Assert.Contains("checkout.cashPreviewTitle", block, StringComparison.Ordinal);
        Assert.Contains("checkout.cardPreviewTitle", block, StringComparison.Ordinal);

        var cardStart = block.IndexOf("PAYMENT_METHOD.CARD", StringComparison.Ordinal);
        var elseStart = block.IndexOf("checkout.cashPreviewTitle", StringComparison.Ordinal);
        Assert.True(cardStart >= 0 && elseStart > cardStart);
        var counterBlock = block[cardStart..elseStart];
        Assert.Contains("keepOpenEl.classList.add(\"hidden\")", counterBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("bankPreviewKeepOpen", counterBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Tray_polls_pending_payment_and_celebrates_once_on_paid_transition()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        Assert.Contains("TRAY_STATUS_POLL_MS", js, StringComparison.Ordinal);
        Assert.Contains("startTrayStatusPolling", js, StringComparison.Ordinal);
        Assert.Contains("celebratedPaidOrderIds", js, StringComparison.Ordinal);
        Assert.Contains("handlePaymentConfirmedCelebration", js, StringComparison.Ordinal);
        Assert.Contains("showPaymentSuccessCelebration", js, StringComparison.Ordinal);
        Assert.Contains("Thanh toán thành công", js, StringComparison.Ordinal);
        Assert.Contains("setTrayOpen(false)", js, StringComparison.Ordinal);

        var refreshStart = js.IndexOf("async function refreshSubmittedTrayStatus", StringComparison.Ordinal);
        var refreshEnd = js.IndexOf("function updateTraySheetForState", StringComparison.Ordinal);
        var refreshBlock = js[refreshStart..refreshEnd];
        Assert.Contains("wasPending && isPaid", refreshBlock, StringComparison.Ordinal);
        Assert.Contains("celebratedPaidOrderIds.has", refreshBlock, StringComparison.Ordinal);
        Assert.Contains("resolveSubmittedCounterState(sess.paymentMethod)", refreshBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("traySubmittedStatus = TRAY_STATE.SUBMITTED_PENDING;", refreshBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Bank_transfer_pending_state_keeps_qr_loading_and_fallback_render()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var start = js.IndexOf("function ensureBankTransferQrMounted", StringComparison.Ordinal);
        var end = js.IndexOf("function applySubmitSuccessUi", StringComparison.Ordinal);
        var block = js[start..end];
        Assert.Contains("checkout.transferQrLoading", block, StringComparison.Ordinal);
        Assert.Contains("/api/track/orders/", block, StringComparison.Ordinal);
        Assert.Contains("track.transferQr", block, StringComparison.Ordinal);
        Assert.Contains("transferUnavailableInTray", js, StringComparison.Ordinal);
        Assert.Contains("transferRetry", js, StringComparison.Ordinal);
        Assert.Contains("window.__annapBankTransferDebug", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Bank_transfer_inline_fallback_renders_qr_image_when_url_exists()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var start = js.IndexOf("function renderInlineTransferFallback", StringComparison.Ordinal);
        var end = js.IndexOf("function renderTransferFatalFallback", StringComparison.Ordinal);
        var block = js[start..end];
        Assert.Contains("guest-bank-transfer__qr", block, StringComparison.Ordinal);
        Assert.Contains("loading=\"eager\"", block, StringComparison.Ordinal);
        Assert.Contains("qrImageLoadState", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Compact_bank_transfer_view_hides_detail_rows_and_copy_buttons()
    {
        var css = File.ReadAllText(Path.Combine(WebRoot, "css", "guest-tray-submitted.css"));
        Assert.Contains(".guest-bank-transfer--tray .guest-bank-transfer__meta", css, StringComparison.Ordinal);
        Assert.Contains(".guest-bank-transfer--tray .guest-bank-transfer__actions", css, StringComparison.Ordinal);
        Assert.Contains("display: none;", css, StringComparison.Ordinal);
        Assert.Contains(".guest-bank-transfer--tray.guest-bank-transfer--fallback .guest-bank-transfer__fallback-details", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Bank_transfer_render_calls_ensure_mount_after_submitted_bank_card_render()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var start = js.IndexOf("function renderSubmittedTraySheet", StringComparison.Ordinal);
        var end = js.IndexOf("function updateTraySummary", StringComparison.Ordinal);
        var block = js[start..end];
        Assert.Contains("if (state === TRAY_STATE.SUBMITTED_BANK) void ensureBankTransferQrMounted(sess);", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Payment_success_celebration_respects_reduced_motion()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var start = js.IndexOf("function runTrayConfettiBurst", StringComparison.Ordinal);
        var end = js.IndexOf("function showPaymentSuccessCelebration", StringComparison.Ordinal);
        var block = js[start..end];
        Assert.Contains("reducedMotionTray()", block, StringComparison.Ordinal);

        var css = File.ReadAllText(Path.Combine(WebRoot, "css", "guest-tray-submitted.css"));
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css, StringComparison.Ordinal);
        Assert.Contains(".order-tray-confetti", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Paid_tray_state_hides_bank_qr_mount_path()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var start = js.IndexOf("function renderSubmittedTraySheet", StringComparison.Ordinal);
        var end = js.IndexOf("function updateTraySummary", StringComparison.Ordinal);
        var block = js[start..end];
        Assert.Contains("TRAY_STATE.SUBMITTED_BANK", block, StringComparison.Ordinal);
        Assert.Contains("ensureBankTransferQrMounted", block, StringComparison.Ordinal);
        Assert.Contains("TRAY_STATE.PAID", block, StringComparison.Ordinal);
    }
}
