using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("menu_items");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CatalogKey).HasMaxLength(160);
        builder.Property(x => x.IconGlyph).HasMaxLength(32);
        builder.Property(x => x.ItemType).HasMaxLength(80);
        builder.Property(x => x.Subtitle).HasMaxLength(240);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.TastingNotes).HasMaxLength(800);
        builder.Property(x => x.MoodProfile).HasMaxLength(160);
        builder.Property(x => x.ShortStory).HasMaxLength(1200);
        builder.Property(x => x.IngredientBreakdown).HasMaxLength(2000);
        builder.Property(x => x.CaffeineLevel);
        builder.Property(x => x.SweetnessLevel);
        builder.Property(x => x.AcidityLevel);
        builder.Property(x => x.Price).HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.DisplaySortOrder).HasDefaultValue(0);
        builder.Property(x => x.IsFeatured).HasDefaultValue(false);
        builder.Property(x => x.IsSignature).HasDefaultValue(false);
        builder.Property(x => x.IsSeasonalHighlight).HasDefaultValue(false);
        builder.Property(x => x.IsArchived).HasDefaultValue(false);
        builder.Property(x => x.ImageUrl).HasMaxLength(2000);
        builder.Property(x => x.DetailPosterImagePath).HasMaxLength(2000);

        builder.Property(x => x.DiscoveryWeight).HasPrecision(10, 4).HasDefaultValue(1m);
        builder.Property(x => x.IsHiddenDiscovery).HasDefaultValue(false);
        builder.Property(x => x.StoryCopy).HasMaxLength(2000);
        builder.Property(x => x.DiscoveryStory).HasMaxLength(2000);
        builder.Property(x => x.IsDiscoveryEligible).HasDefaultValue(true);
        builder.Property(x => x.MoodTags).HasMaxLength(600);
        builder.Property(x => x.FlavorTags).HasMaxLength(600);

        // Persist as jsonb; optional at DB level so existing rows / partial seeds stay valid.
        builder.Property(x => x.SensoryProfile)
            .HasColumnType("jsonb")
            .IsRequired(false)
            .HasConversion(new DrinkSensoryProfileConverter());

        builder.Property(x => x.EmbeddingModel).HasMaxLength(200);

        builder.Property(x => x.Embedding)
            .HasConversion(new EmbeddingVectorConverter())
            .HasColumnType("vector");

        builder.HasIndex(x => x.CategoryId);
    }
}

