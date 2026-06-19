namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Copies owner-provided .webp assets into wwwroot for static serving.</summary>
public static class DrinkAssetPublisher
{
    public static int SyncFromSource(string? sourceDirectory, string webRootPath, ILogger? log = null)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
        {
            log?.LogDebug("Drink asset sync skipped: source not found ({Path}).", sourceDirectory);
            return 0;
        }

        var dest = Path.Combine(webRootPath, DrinkAssetResolver.WebFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(dest);

        var copied = 0;
        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*.webp", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            var target = Path.Combine(dest, name);
            try
            {
                File.Copy(file, target, overwrite: true);
                copied++;
            }
            catch (Exception ex)
            {
                log?.LogWarning(ex, "Failed to copy drink asset {File}.", name);
            }
        }

        log?.LogInformation("Drink asset sync: {Count} webp file(s) → {Dest}.", copied, dest);
        return copied;
    }
}
