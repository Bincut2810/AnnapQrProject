using System.Text.Json;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;
using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.GuestExperience;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Web.Pages;

public sealed class IndexModel(
    IApplicationDbContext db,
    IHostEnvironment env,
    IHttpContextAccessor httpContextAccessor,
    IOptions<DiagnosticsOptions> diagnostics,
    IOptions<GuestOperationalOptions> guestOperational)
    : PageModel
{
    public List<SignatureDrinkVm> SignatureDrinks { get; private set; } = [];
    public List<CategoryVm> Categories { get; private set; } = [];
    public List<CartCatalogRowVm> CartCatalog { get; private set; } = [];

    /// <summary>Serialized guided sommelier catalog for guest boot (CMS-backed when seeded).</summary>
    public string GuidedSommelierCatalogJson { get; private set; } = GuidedSommelierCatalog.ToClientJson();

    /// <summary>CMS copy + limits for the seated group path.</summary>
    public string GroupExperienceJson { get; private set; } = "{}";

    /// <summary>Which arrival paths are visible on the seated homepage.</summary>
    public string HomepageExperienceJson { get; private set; } = "{}";

    /// <summary>Letter Room desk copy + three envelope labels (merged defaults + discovery CMS JSON).</summary>
    public string LetterRoomDeskJson { get; private set; } = "{}";

    public bool IsDevelopment => env.IsDevelopment();

    /// <summary>LAN isolation: skip mood/sommelier/tray scripts; only menu fetch on home (<c>Diagnostics:SlimGuestBoot</c>).</summary>
    public bool SlimGuestBoot => (env.IsDevelopment() || diagnostics.Value.DeveloperOverlays) && diagnostics.Value.SlimGuestBoot;

    /// <summary>When true, seated guest homepage shows a primary link to the menu before optional experiences.</summary>
    public bool MenuFirstArrival => guestOperational.Value.MenuFirstArrival;

    /// <summary>When true, seated guest homepage uses calmer CSS motion for WebViews and low-end devices.</summary>
    public bool CalmArrivalAnimations => guestOperational.Value.CalmArrivalAnimations;

    /// <summary>Set when the guest arrives via table QR (<c>/table/T12</c> or <c>/t/annap-t12</c>).</summary>
    public Guid? VenueTableId { get; private set; }

    /// <summary>Display for hero and tray (falls back to table code).</summary>
    public string? TableGuestLabel { get; private set; }

    public string? PublicSlug { get; private set; }

    public bool HasSeatedTable => VenueTableId is not null;

    /// <summary>Guest scanned <c>/table</c> or <c>/t</c> but no active table matched.</summary>
    public bool QrScanInvalid { get; private set; }

    /// <summary><c>?vt=</c> was supplied but did not resolve to an active table.</summary>
    public bool TableHandoffInvalid { get; private set; }

    public GuestTableContextState TableContextState =>
        GuestTableContext.Resolve(HasSeatedTable, QrScanInvalid, TableHandoffInvalid);

    /// <summary>Homepage CMS: guided sommelier ritual on homepage (not slim QR Lite).</summary>
    public bool IsSommelierEnabled { get; private set; } = true;

    /// <summary>Live guided catalog still exposes q1–q4 and all AI Lite option keys.</summary>
    public bool IsSommelierLiteCompatible { get; private set; }

    public string SommelierLiteBootJson { get; private set; } = "{}";

    public string? SommelierLiteIncompatibleReason { get; private set; }

    public async Task OnGetAsync(Guid? vt, CancellationToken cancellationToken)
    {
        VenueTable? seated = null;

        // Hand back from menu / drink pages (?vt=…) so home stays “seated” without re-scanning the table QR.
        if (vt is Guid handoffId)
        {
            seated = await db.VenueTables.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == handoffId && v.IsActive, cancellationToken);
        }

        var tableDisplayCode =
            httpContextAccessor.HttpContext?.Items["QrTableDisplayCode"] as string
            ?? Request.Query["tc"].FirstOrDefault();
        var publicSlug =
            httpContextAccessor.HttpContext?.Items["QrPublicSlug"] as string
            ?? Request.Query["ts"].FirstOrDefault();

        if (seated is null && !string.IsNullOrWhiteSpace(publicSlug))
        {
            var slug = publicSlug.Trim().ToLowerInvariant();
            seated = await db.VenueTables.AsNoTracking()
                .FirstOrDefaultAsync(v => v.PublicSlug == slug && v.IsActive, cancellationToken);
        }
        else if (seated is null && !string.IsNullOrWhiteSpace(tableDisplayCode))
        {
            var code = tableDisplayCode.Trim();
            seated = await db.VenueTables.AsNoTracking()
                .FirstOrDefaultAsync(
                    v => v.VenueCode == "annap" && v.DisplayCode.ToLower() == code.ToLower() && v.IsActive,
                    cancellationToken);
        }

        if (seated is not null)
        {
            VenueTableId = seated.Id;
            PublicSlug = seated.PublicSlug;
            TableGuestLabel = string.IsNullOrWhiteSpace(seated.DisplayLabel) ? seated.DisplayCode : seated.DisplayLabel;
        }
        else
        {
            var qrAttempted =
                !string.IsNullOrWhiteSpace(httpContextAccessor.HttpContext?.Items["QrTableDisplayCode"] as string)
                || !string.IsNullOrWhiteSpace(httpContextAccessor.HttpContext?.Items["QrPublicSlug"] as string)
                || !string.IsNullOrWhiteSpace(publicSlug)
                || !string.IsNullOrWhiteSpace(tableDisplayCode);
            if (qrAttempted)
                QrScanInvalid = true;
            else if (vt is Guid)
                TableHandoffInvalid = true;
        }

        await HomepageExperienceBootstrapper.EnsureDefaultsAsync(db, cancellationToken);
        await HomepageExperienceBootstrapper.EnsureDevelopmentRitualFlagsAsync(db, env.IsDevelopment(), cancellationToken);
        await ExperienceCatalogBootstrapper.EnsureGuidedAndDiscoveryAsync(db, cancellationToken);
        await ExperienceCatalogBootstrapper.EnsureNativeEnglishCopyAsync(db, cancellationToken);
        await ExperienceCatalogBootstrapper.SyncGuidedDisplayCopyInDevelopmentAsync(db, env.IsDevelopment(), cancellationToken);

        var qSeeds = await GuidedSommelierExperienceCatalog.LoadQuestionSeedsAsync(db, cancellationToken);
        var liteCompat = GuestSommelierLiteCompatibility.Assess(qSeeds);
        IsSommelierLiteCompatible = liteCompat.IsCompatible;
        SommelierLiteIncompatibleReason = liteCompat.ReasonCode;
        SommelierLiteBootJson = JsonSerializer.Serialize(
            liteCompat.ToClientDto(),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        GuidedSommelierCatalogJson = GuidedSommelierExperienceCatalog.ToClientJson(
            qSeeds,
            GuidedSommelierCatalog.QuestionSetId);

        var gid = ExperienceGroupSettingsConfiguration.SingletonId;
        var grp = await db.ExperienceGroupSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == gid, cancellationToken);
        GroupExperienceJson = JsonSerializer.Serialize(
            new
            {
                arrivalKicker = grp?.ArrivalKicker ?? "Together at the table",
                guestCountPrompt = grp?.GuestCountPrompt ?? "How many guests?",
                guestCountLead = grp?.GuestCountLead ?? "",
                minGuests = grp?.MinGuests ?? 1,
                maxGuests = grp?.MaxGuests ?? 10,
                guestTabsIntro = grp?.GuestTabsIntro ?? "",
                guestDoneHint = grp?.GuestDoneHint ?? "",
                summaryHeadline = grp?.SummaryHeadline ?? "Table summary",
                summaryLead = grp?.SummaryLead ?? "",
                hospitalityClosing = grp?.HospitalityClosing ?? ""
            },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var hid = HomepageExperienceSettingsConfiguration.SingletonId;
        var home = await db.HomepageExperienceSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == hid, cancellationToken);
        IsSommelierEnabled = home?.IsSommelierEnabled ?? true;
        HomepageExperienceJson = JsonSerializer.Serialize(
            new
            {
                isGroupEnabled = home?.IsGroupEnabled ?? true,
                isSoloEnabled = home?.IsSoloEnabled ?? true,
                isSommelierEnabled = home?.IsSommelierEnabled ?? true
            },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var discSid = ExperienceDiscoverySettingsConfiguration.SingletonId;
        var discCms = await db.ExperienceDiscoverySettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == discSid, cancellationToken);
        var discTone = discCms?.AdventureTone is >= 1 and <= 5 ? discCms.AdventureTone : 3;
        LetterRoomDeskJson = GuestLetterRoomDesk.ToClientJson(discCms?.LetterRoomContentJson, discTone);

        // When any signature slot rows exist, the group rail is CMS-owned — never fall back to MenuItem.IsSignature
        // (fallback previously showed "old" flagged drinks whenever the strict join returned zero rows).
        var hasSignatureRailCuration = await db.ExperienceSignatureSlots.AsNoTracking()
            .AnyAsync(cancellationToken);

        // Two-step load: slot curation + live MenuItem row guarantees canonical media (ImageUrl) is always read from menu_items,
        // avoiding any join/projection edge cases with navigation properties.
        var activeSlotRows = await db.ExperienceSignatureSlots.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .Select(s => new { s.MenuItemId, s.EditorialKicker, s.EditorialBody })
            .ToListAsync(cancellationToken);

        if (activeSlotRows.Count > 0)
        {
            var slotIds = activeSlotRows.Select(s => s.MenuItemId).Distinct().ToList();
            var menuById = await db.MenuItems.AsNoTracking()
                .Where(m => slotIds.Contains(m.Id) && m.IsAvailable && !m.IsArchived)
                .Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.Subtitle,
                    m.TastingNotes,
                    m.Description,
                    m.MoodProfile,
                    m.Price,
                    CategoryName = m.Category.Name,
                    m.ImageUrl,
                    m.DetailPosterImagePath
                })
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            SignatureDrinks = [];
            foreach (var s in activeSlotRows)
            {
                if (!menuById.TryGetValue(s.MenuItemId, out var m))
                    continue;
                SignatureDrinks.Add(new SignatureDrinkVm(
                    m.Id,
                    m.Name,
                    s.EditorialKicker ?? m.Subtitle,
                    s.EditorialBody ?? m.TastingNotes ?? m.Description,
                    m.MoodProfile,
                    m.Price,
                    m.CategoryName,
                    MenuMediaResolver.ResolveCardImageUrl(
                        null, null, m.ImageUrl, null, m.Name, m.CategoryName, m.DetailPosterImagePath)));
            }
        }
        else if (!hasSignatureRailCuration)
        {
            var sigQuery = db.MenuItems
                .AsNoTracking()
                .Where(x => x.IsAvailable && !x.IsArchived && x.IsSignature);

            if (!await sigQuery.AnyAsync(cancellationToken))
            {
                sigQuery = db.MenuItems.AsNoTracking().Where(x => x.IsAvailable && !x.IsArchived);
            }

            var sigRows = await sigQuery
                .OrderByDescending(x => x.IsSignature)
                .ThenByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Subtitle,
                    TastingLine = x.TastingNotes ?? x.Description,
                    x.MoodProfile,
                    x.Price,
                    CategoryName = x.Category.Name,
                    x.ImageUrl,
                    x.DetailPosterImagePath
                })
            .ToListAsync(cancellationToken);
            SignatureDrinks = sigRows
                .Select(x => new SignatureDrinkVm(
                    x.Id,
                    x.Name,
                    x.Subtitle,
                    x.TastingLine,
                    x.MoodProfile,
                    x.Price,
                    x.CategoryName,
                    MenuMediaResolver.ResolveCardImageUrl(
                        null, null, x.ImageUrl, null, x.Name, x.CategoryName, x.DetailPosterImagePath)))
                .ToList();
        }
        else
        {
            SignatureDrinks = [];
        }

        Categories = await db.MenuCategories
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .Select(x => new CategoryVm(x.Id, x.Name))
            .ToListAsync(cancellationToken);

        CartCatalog = await db.MenuItems
            .AsNoTracking()
            .Where(x => x.IsAvailable && !x.IsArchived)
            .OrderBy(x => x.DisplaySortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new CartCatalogRowVm(x.Id, x.Name, x.Price))
            .ToListAsync(cancellationToken);

        // Seated arrival HTML carries live CMS payloads — discourage shared caches from serving a stale rail.
        if (VenueTableId is not null)
            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    }
}

public sealed record SignatureDrinkVm(
    Guid Id,
    string Name,
    string? Subtitle,
    string? TastingLine,
    string? MoodProfile,
    decimal Price,
    string CategoryName,
    string CardImageUrl);

public sealed record CategoryVm(Guid Id, string Name);

public sealed record CartCatalogRowVm(Guid Id, string Name, decimal Price);
