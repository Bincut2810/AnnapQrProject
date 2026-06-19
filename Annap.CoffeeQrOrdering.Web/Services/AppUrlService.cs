using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Web.Services;

public sealed class AppUrlService(
    IHttpContextAccessor httpContextAccessor,
    IOptionsSnapshot<AppUrlOptions> appUrlOptions,
    IApplicationDbContext db) : IAppUrlService
{
    private const string RequestCacheKey = "__annapResolvedPublicBaseUrl";

    public string GetBaseUrl(HttpContext? httpContext = null)
    {
        var ctx = httpContext ?? httpContextAccessor.HttpContext;
        if (ctx?.Items.TryGetValue(RequestCacheKey, out var cached) == true && cached is string hit && !string.IsNullOrEmpty(hit))
            return hit;

        var resolved = Resolve(ctx);
        if (ctx is not null)
            ctx.Items[RequestCacheKey] = resolved;
        return resolved;
    }

    public string BuildAbsoluteUrl(string relativePath, HttpContext? httpContext = null)
    {
        var path = (relativePath ?? "").Trim();
        if (path.Length == 0)
            path = "/";
        if (path[0] != '/')
            path = "/" + path;
        var baseUrl = GetBaseUrl(httpContext);
        if (string.IsNullOrEmpty(baseUrl))
            return path;
        return baseUrl + path;
    }

    private string Resolve(HttpContext? ctx)
    {
        var dbOverride = db.AppNetworkSettings.AsNoTracking()
            .Where(x => x.Id == AppNetworkSettings.SingletonId)
            .Select(x => x.PublicBaseUrlOverride)
            .FirstOrDefault();
        var fromDb = NormalizeBase(dbOverride);
        if (!string.IsNullOrEmpty(fromDb))
            return fromDb;

        var fromCfg = NormalizeBase(appUrlOptions.Value.PublicBaseUrl);
        if (!string.IsNullOrEmpty(fromCfg))
            return fromCfg;

        if (ctx?.Request is { } req)
        {
            var host = req.Host;
            if (host.HasValue)
                return $"{req.Scheme}://{host.Value}".TrimEnd('/');
        }

        return "";
    }

    private static string NormalizeBase(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";
        var t = raw.Trim().TrimEnd('/');
        return t;
    }
}
