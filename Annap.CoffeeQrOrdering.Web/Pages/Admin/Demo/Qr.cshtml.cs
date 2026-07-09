using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Demo;

[Authorize(Policy = "Staff")]
public sealed class QrModel(
    IAppUrlService appUrlService,
    IWebHostEnvironment environment,
    ILanIpDetector lanIpDetector,
    IHttpContextAccessor httpContextAccessor) : PageModel
{
    public string PublicBaseUrl { get; private set; } = "";

    public AppUrlResolution QrResolution { get; private set; } =
        new(AppUrlResolutionSource.Unresolved, "", null, null, null, []);

    /// <summary>When true, show Development-only hint to open QR page via LAN URL (phones cannot use localhost).</summary>
    public bool ShowLocalhostLanBanner { get; private set; }

    /// <summary>Preferred phone URL when this page was opened on localhost (same port as public base when possible).</summary>
    public string PhoneDemoWiFiBaseUrl { get; private set; } = "";

    public IReadOnlyList<DemoTableQrRow> Rows { get; private set; } = [];

    public void OnGet()
    {
        var http = httpContextAccessor.HttpContext;
        QrResolution = appUrlService.DescribeResolution(http);
        var baseUrl = QrResolution.ResolvedBaseUrl.TrimEnd('/');
        PublicBaseUrl = baseUrl;

        var port = 8080;
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var pubUri) && pubUri.Port > 0)
            port = pubUri.Port;

        var detectedIp = lanIpDetector.TryGetPreferredLanIPv4();
        if (!string.IsNullOrEmpty(detectedIp) && port > 0)
            PhoneDemoWiFiBaseUrl = $"http://{detectedIp}:{port}";
        else
            PhoneDemoWiFiBaseUrl = baseUrl;

        var host = http?.Request.Host.Host ?? "";
        ShowLocalhostLanBanner = environment.IsDevelopment()
            && (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.Equals("127.0.0.1", StringComparison.Ordinal));

        var codes = new[] { "T01", "T02", "T03" };
        Rows = codes.Select(code =>
        {
            var url = $"{baseUrl}/table/{code}";
            return new DemoTableQrRow(code, url, QrCodeDataUriBuilder.FromText(url));
        }).ToList();
    }
}

public sealed record DemoTableQrRow(string TableCode, string GuestUrl, string QrDataUri);
