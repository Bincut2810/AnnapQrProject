using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.Services;

namespace Annap.CoffeeQrOrdering.Tests;

public class SpecialtyCoffeeMediaPipelineTests
{
    private const string CloudinaryHero =
        "https://res.cloudinary.com/annap/image/upload/v1/annap/menu-items/specialty-hero.webp";

    private const string CloudinaryPoster =
        "https://res.cloudinary.com/annap/image/upload/v1/annap/menu-items/specialty-poster.webp";

    [Fact]
    public void Specialty_bootstrap_never_overwrites_Cloudinary_ImageUrl()
    {
        var item = new MenuItem
        {
            ImageUrl = CloudinaryHero,
            DetailPosterImagePath = CloudinaryPoster
        };

        AnnapSpecialtyCoffeeBootstrap.ApplyCanonicalImages(item, "/images/annap-drinks/seed.webp");

        Assert.Equal(CloudinaryHero, item.ImageUrl);
        Assert.Equal(CloudinaryPoster, item.DetailPosterImagePath);
    }

    [Fact]
    public void Specialty_bootstrap_heals_wiped_card_url_from_Cloudinary_poster()
    {
        var item = new MenuItem
        {
            ImageUrl = AnnapSpecialtyCoffeeBootstrap.FallbackImageUrl,
            DetailPosterImagePath = CloudinaryPoster
        };

        AnnapSpecialtyCoffeeBootstrap.ApplyCanonicalImages(item, "/images/annap-drinks/seed.webp");

        Assert.Equal(CloudinaryPoster, item.ImageUrl);
        Assert.Equal(CloudinaryPoster, item.DetailPosterImagePath);
    }

    [Fact]
    public void Specialty_bootstrap_uses_seed_asset_only_when_no_durable_media()
    {
        var item = new MenuItem();

        AnnapSpecialtyCoffeeBootstrap.ApplyCanonicalImages(item, "/images/annap-drinks/seed.webp");

        Assert.Equal("/images/annap-drinks/seed.webp", item.ImageUrl);
        Assert.Equal("/images/annap-drinks/seed.webp", item.DetailPosterImagePath);
    }

    [Fact]
    public void Card_resolver_recovers_Cloudinary_from_detail_poster_when_card_field_is_fallback()
    {
        var resolved = MenuMediaResolver.TryResolveCardImageUrl(
            null,
            null,
            AnnapSpecialtyCoffeeBootstrap.FallbackImageUrl,
            null,
            "Kinini Village — Dufatanye",
            "Specialty Coffee",
            CloudinaryPoster);

        Assert.Equal(CloudinaryPoster, resolved);
    }

    [Fact]
    public void Detail_and_card_prefer_same_Cloudinary_source_of_truth()
    {
        var card = MenuMediaResolver.TryResolveCardImageUrl(
            null, null, CloudinaryHero, null, "Drink", "Specialty Coffee", CloudinaryPoster);
        var detail = MenuMediaResolver.TryResolveDetailPosterUrl(
            CloudinaryPoster, CloudinaryHero, "Drink", "Specialty Coffee");

        Assert.Equal(CloudinaryHero, card);
        Assert.Equal(CloudinaryPoster, detail);
        Assert.True(MenuMediaResolver.IsCloudinaryUrl(card));
        Assert.True(MenuMediaResolver.IsCloudinaryUrl(detail));
    }

    [Fact]
    public void Ephemeral_placeholder_is_not_durable_media()
    {
        Assert.False(MenuMediaResolver.IsDurableMediaUrl(AnnapSpecialtyCoffeeBootstrap.FallbackImageUrl));
        Assert.True(MenuMediaResolver.IsEphemeralPlaceholderUrl(AnnapSpecialtyCoffeeBootstrap.FallbackImageUrl));
        Assert.True(MenuMediaResolver.IsDurableMediaUrl(CloudinaryHero));
    }
}
