using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>
/// Idempotent bootstrap for the four flagship specialty coffees (editorial content from house catalog).
/// Never overwrites durable Cloudinary / managed media URLs — admin uploads must survive redeploy.
/// </summary>
public static class AnnapSpecialtyCoffeeBootstrap
{
    /// <summary>UI-only placeholder. Must not be persisted when a durable Cloudinary URL already exists.</summary>
    public const string FallbackImageUrl = MenuMediaResolver.FallbackPlaceholderUrl;

    private const decimal CupPriceVnd = 80000m;

    public static async Task EnsureSpecialtyCoffeesAsync(
        IApplicationDbContext db,
        DrinkAssetResolver assetResolver,
        ILogger log,
        CancellationToken cancellationToken = default)
    {
        var category = await EnsureCategoryAsync(db, cancellationToken).ConfigureAwait(false);

        foreach (var seed in Seeds)
            await UpsertAsync(db, assetResolver, category.Id, seed, cancellationToken).ConfigureAwait(false);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        log.LogInformation("Specialty coffee bootstrap: {Count} flagship coffees ensured.", Seeds.Length);
    }

    private static async Task<MenuCategory> EnsureCategoryAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var existing = await db.MenuCategories
            .FirstOrDefaultAsync(c => c.Name == AnnapSpecialtyCoffeeCatalog.CategoryName, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
            return existing;

        var maxSort = await db.MenuCategories
            .Select(c => (int?)c.SortOrder)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false);

        var category = new MenuCategory
        {
            Name = AnnapSpecialtyCoffeeCatalog.CategoryName,
            SortOrder = 0
        };
        if (maxSort.HasValue && maxSort.Value >= category.SortOrder)
            category.SortOrder = maxSort.Value + 1;

        db.MenuCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return category;
    }

    private static async Task UpsertAsync(
        IApplicationDbContext db,
        DrinkAssetResolver assetResolver,
        Guid categoryId,
        SpecialtyCoffeeSeed seed,
        CancellationToken cancellationToken)
    {
        var entity = await db.MenuItems
            .FirstOrDefaultAsync(m => m.CatalogKey == seed.CatalogKey, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            entity = await db.MenuItems
                .FirstOrDefaultAsync(
                    m => m.CategoryId == categoryId && m.Name == seed.Name,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (entity is null)
        {
            entity = new MenuItem { CategoryId = categoryId };
            db.MenuItems.Add(entity);
        }

        entity.Name = seed.Name;
        entity.CategoryId = categoryId;
        entity.CatalogKey = seed.CatalogKey;
        entity.Origin = seed.Origin;
        entity.Subtitle = seed.Subtitle;
        entity.ShortStory = seed.ShortStory;
        entity.ProducerStory = seed.ProducerStory;
        entity.TastingNotes = seed.TastingNotes;
        entity.MoodProfile = seed.MoodProfile;
        entity.IngredientBreakdown = seed.IngredientBreakdown;
        entity.FlavorTags = seed.FlavorTags;
        entity.MoodTags = seed.MoodTags;
        entity.ItemType = "pour-over";
        entity.SensoryProfile = seed.Sensory;
        entity.CaffeineLevel = seed.CaffeineLevel;
        entity.SweetnessLevel = seed.SweetnessLevel;
        entity.AcidityLevel = seed.AcidityLevel;
        entity.Price = CupPriceVnd;
        entity.DisplaySortOrder = seed.SortOrder;
        entity.IsAvailable = true;
        entity.IsArchived = false;
        entity.IsSignature = true;
        entity.IsFeatured = true;
        entity.IsSeasonalHighlight = true;
        entity.IsDiscoveryEligible = false;
        entity.DiscoveryWeight = 0m;

        ApplyCanonicalImages(
            entity,
            assetResolver.ResolveWebUrl(AnnapSpecialtyCoffeeCatalog.CategoryName, seed.Name));
    }

    /// <summary>
    /// Production rule: Cloudinary (and managed /media) URLs are durable and must never be clobbered
    /// by seed assets or <see cref="FallbackImageUrl"/>. Also heals card <see cref="MenuItem.ImageUrl"/>
    /// when a prior bootstrap wiped it while leaving a Cloudinary detail poster intact.
    /// </summary>
    internal static void ApplyCanonicalImages(MenuItem entity, string? bootstrapAssetUrl)
    {
        if (!MenuMediaResolver.IsDurableMediaUrl(entity.ImageUrl)
            && MenuMediaResolver.IsDurableMediaUrl(entity.DetailPosterImagePath))
        {
            entity.ImageUrl = entity.DetailPosterImagePath;
        }

        if (!MenuMediaResolver.IsDurableMediaUrl(entity.ImageUrl))
        {
            if (!string.IsNullOrWhiteSpace(bootstrapAssetUrl))
                entity.ImageUrl = bootstrapAssetUrl;
            else if (string.IsNullOrWhiteSpace(entity.ImageUrl)
                     || MenuMediaResolver.IsEphemeralPlaceholderUrl(entity.ImageUrl))
                entity.ImageUrl = null;
        }

        if (string.IsNullOrWhiteSpace(entity.DetailPosterImagePath)
            || MenuMediaResolver.IsEphemeralPlaceholderUrl(entity.DetailPosterImagePath))
        {
            if (MenuMediaResolver.IsDurableMediaUrl(entity.ImageUrl)
                || !string.IsNullOrWhiteSpace(entity.ImageUrl))
                entity.DetailPosterImagePath = entity.ImageUrl;
        }
    }

    private sealed record SpecialtyCoffeeSeed(
        string CatalogKey,
        string Name,
        string Origin,
        string Subtitle,
        string ShortStory,
        string ProducerStory,
        string TastingNotes,
        string MoodProfile,
        string IngredientBreakdown,
        string FlavorTags,
        string MoodTags,
        DrinkSensoryProfile Sensory,
        int CaffeineLevel,
        int SweetnessLevel,
        int AcidityLevel,
        int SortOrder);

    private static readonly SpecialtyCoffeeSeed[] Seeds =
    [
        new(
            AnnapSpecialtyCoffeeCatalog.DufatanyeKey,
            "Kinini Village — Dufatanye",
            "Rwanda",
            "Rwanda · Washed Bourbon · Rulindo",
            "Thành lập năm 2014 tại tỉnh phía bắc Rwanda, Trạm Rửa Cà Phê Kinini phục vụ 48 thành viên từ làng Tumba — 85% là phụ nữ. Dufatanye là câu chuyện của 60 phụ nữ nông dân đang cùng nhau làm cà phê với cam kết bình đẳng giới.",
            "Nhóm Nông Dân Dufatanye ký hợp đồng với trạm Kinini: cung cấp cây giống Bourbon, đào tạo kỹ thuật, và tái đầu tư 10% lợi nhuận vào giáo dục và y tế cộng đồng. Bạn đang uống washed Bourbon từ độ cao 1.800–2.000m tại Rulindo.",
            "Hoa nhài · Cam quýt · Trà đen · Ngọt thanh — nhẹ, thơm, sạch; uống không đường vẫn dễ chịu.",
            "Nhẹ · Thơm hoa · Sạch",
            "Bourbon · Washed · Kinini Village",
            "jasmine,citrus,black tea,floral",
            "calm,welcoming,gentle,clean",
            new DrinkSensoryProfile
            {
                Body = "tea_like",
                Acidity = "quiet",
                Sweetness = "restrained",
                Finish = "clean",
                AromaFamily = "floral",
                TemperatureEmotion = "temperate",
                Energy = "still",
                SocialMood = "quiet",
                Texture = "satin",
                CaffeineIntensity = 2
            },
            2,
            2,
            2,
            1),
        new(
            AnnapSpecialtyCoffeeCatalog.AbateranankungaKey,
            "Kinini Village — Abateranankunga",
            "Rwanda",
            "Rwanda · Washed Bourbon · Rulindo",
            "Cùng thuộc Trạm Rửa Kinini (thành lập 2014), trạm phục vụ 48 thành viên tại làng Tumba, với 85% là phụ nữ. Abateranankunga là 24 phụ nữ nông dân cùng mô hình phát triển cộng đồng: giống, đào tạo kỹ thuật và tái đầu tư lợi nhuận vào giáo dục, y tế.",
            "Cùng vùng Rwanda với Dufatanye nhưng vị ngọt trái cây nổi hơn — washed Bourbon từ Rulindo, độ cao 1.800–2.000m. Nhóm Abateranankunga mang đến cảm giác mùa hè: đào chín, lemon curd và hoa trắng.",
            "Đào chín · Lemon curd · Hoa trắng · Ngọt dịu — ngọt trái cây rõ hơn, thơm mùa hè; hậu vị sạch, không đắng.",
            "Ngọt trái cây · Tươi · Dễ ghiền",
            "Bourbon · Washed · Kinini Village",
            "peach,lemon curd,white flowers,stone fruit",
            "bright,playful,fruit-forward,easy-going",
            new DrinkSensoryProfile
            {
                Body = "round",
                Acidity = "balanced",
                Sweetness = "rounded",
                Finish = "clean",
                AromaFamily = "stone_fruit",
                TemperatureEmotion = "temperate",
                Energy = "playful",
                SocialMood = "gathered",
                Texture = "satin",
                CaffeineIntensity = 2
            },
            2,
            3,
            3,
            2),
        new(
            AnnapSpecialtyCoffeeCatalog.RiftValleyKey,
            "Rift Valley Coffee Caucus",
            "Kenya",
            "Kenya · Natural · Thung Lũng Rift",
            "Khởi nguồn năm 2023 như một nhóm hỗ trợ phi chính thức, do Stephen Nendela của trang trại Muinami khởi xướng. Năm 2024, nhóm hoàn thành lô xuất khẩu trực tiếp đầu tiên. Đến mùa vụ tiếp theo, quy mô mở rộng lên hơn 30 thành viên, trải dài qua Cherengany Hills, Nandi Hills và chân núi Mt. Elgon.",
            "Rift Valley Coffee Caucus gom nông dân Thung Lũng Rift phía tây Kenya — natural Ruiru 11, Batian và SL-28 từ độ cao 1.800–1.900m. Đậm, phức tạp, như uống vang đỏ nhẹ.",
            "Mâm xôi · Nho đen · Socola đắng · Rượu vang — đậm, phức tạp; độ chua bright; hậu vị dài, ấm, có chiều sâu.",
            "Đậm · Phức tạp · Suy ngẫm",
            "Ruiru 11, Batian, SL-28 · Natural · Rift Valley",
            "jam,black grape,chocolate,wine",
            "bold,complex,adventurous,contemplative",
            new DrinkSensoryProfile
            {
                Body = "syrupy",
                Acidity = "lifted",
                Sweetness = "rounded",
                Finish = "linger",
                AromaFamily = "cocoa",
                TemperatureEmotion = "warming",
                Energy = "intense",
                SocialMood = "solitary",
                Texture = "velvet",
                CaffeineIntensity = 4
            },
            4,
            3,
            4,
            3),
        new(
            AnnapSpecialtyCoffeeCatalog.NigussieKey,
            "Nigussie Nare — Murago Outgrowers",
            "Ethiopia",
            "Ethiopia · Natural 74158 · Bombe, Sidama",
            "Trang trại Setame tọa lạc trên sườn đồi ở độ cao 2.300m tại Bombe, rộng 10 hectare, trồng giống 74158 xen canh khoai sọ và khoai lang. Nigussie còn quản lý trang trại tại Kokose và Tiburo, thu mua cherry từ khoảng 60 nông dân lân cận.",
            "Murago Outgrowers là mạng lưới hơn 60 nông dân đối tác cùng Nigussie Nare — natural 74158 từ độ cao cao nhất trong bộ bốn lô specialty của ANNAP. Blueberry nổi ngay từ ngụm đầu, hậu vị kéo rất dài.",
            "Blueberry · Hoa jasmine · Mật ong · Socola sữa — bright, sống động; hậu vị rất dài, ngọt mật ong.",
            "Biểu cảm · Nhiều lớp · Đáng nhớ",
            "74158 · Natural · Bombe, Sidama",
            "blueberry,jasmine,honey,milk chocolate",
            "expressive,layered,premium,memorable",
            new DrinkSensoryProfile
            {
                Body = "round",
                Acidity = "crystalline",
                Sweetness = "luscious",
                Finish = "linger",
                FinishDetail = "ngọt mật ong kéo dài",
                AromaFamily = "floral",
                TemperatureEmotion = "temperate",
                Energy = "focused",
                SocialMood = "quiet",
                Texture = "satin",
                CaffeineIntensity = 3
            },
            3,
            4,
            4,
            4)
    ];
}
