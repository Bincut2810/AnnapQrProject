using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Annap.CoffeeQrOrdering.Web.Services;

internal static class MenuCatalogBootstrapPaths
{
    internal static string ResolveJsonCatalogPath(IConfiguration configuration, IHostEnvironment environment)
    {
        var p = configuration["MenuCatalog:ImportPath"]?.Trim();
        if (string.IsNullOrEmpty(p))
            return Path.Combine(environment.ContentRootPath, "wwwroot", "data", "menu-catalog.json");
        return Path.IsPathRooted(p) ? p : Path.Combine(environment.ContentRootPath, p);
    }

    /// <summary>Canonical Annap beverage CSV (real data mode).</summary>
    internal static string ResolveAnnapDrinksCsvPath(IConfiguration configuration, IHostEnvironment environment)
    {
        var p = configuration["MenuCatalog:AnnapDrinksCsvPath"]?.Trim();
        if (!string.IsNullOrEmpty(p))
            return Path.IsPathRooted(p) ? p : Path.Combine(environment.ContentRootPath, p);

        var repoDocs = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "docs", "AnnapDrinks.csv"));
        if (File.Exists(repoDocs))
            return repoDocs;

        return Path.Combine(environment.ContentRootPath, "wwwroot", "data", "AnnapDrinks.csv");
    }

    internal static string ResolveAnnapAssetsSourcePath(IConfiguration configuration, IHostEnvironment environment)
    {
        var p = configuration["MenuCatalog:AssetsSourcePath"]?.Trim();
        if (!string.IsNullOrEmpty(p))
            return Path.IsPathRooted(p) ? p : Path.Combine(environment.ContentRootPath, p);

        var repoDocs = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "docs", "Assets"));
        if (Directory.Exists(repoDocs))
            return repoDocs;

        return Path.Combine(environment.ContentRootPath, "wwwroot", DrinkAssetResolver.WebFolder);
    }
}
