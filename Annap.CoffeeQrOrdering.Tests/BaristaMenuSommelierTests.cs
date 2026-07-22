using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;
using Annap.CoffeeQrOrdering.Web.GuestExperience;

namespace Annap.CoffeeQrOrdering.Tests;

public class BaristaMenuSommelierTests
{
    [Fact]
    public void Juice_path_resolves_in_two_answers_and_targets_orange()
    {
        Assert.True(GuidedSommelierCatalog.TryResolveBranchPath(
            ["q0_juice", "q_ju_fruit_orange"],
            out var resolved,
            out var error), error);
        Assert.Equal(2, resolved.Count);
        Assert.Contains("Nước Ép Cam", GuidedSommelierCatalog.CollectMenuTargets(resolved));
    }

    [Fact]
    public void Cold_brew_pure_ends_branch_without_fruit_step()
    {
        Assert.True(GuidedSommelierCatalog.TryResolveBranchPath(
            ["q0_coldbrew", "q_cb_style_pure"],
            out var resolved,
            out var error), error);
        Assert.Equal(2, resolved.Count);
        Assert.True(resolved[^1].EndsBranch);
        Assert.Contains("Cold Brew", GuidedSommelierCatalog.CollectMenuTargets(resolved));
    }

    [Fact]
    public void Cold_brew_fruit_requires_second_question()
    {
        Assert.False(GuidedSommelierCatalog.TryResolveBranchPath(
            ["q0_coldbrew", "q_cb_style_fruit"],
            out _,
            out _));

        Assert.True(GuidedSommelierCatalog.TryResolveBranchPath(
            ["q0_coldbrew", "q_cb_style_fruit", "q_cb_fruit_mulberry"],
            out var resolved,
            out var error), error);
        Assert.Contains("Cold Brew Dâu Tằm", GuidedSommelierCatalog.CollectMenuTargets(resolved));
    }

    [Fact]
    public void Espresso_milk_detail_is_gated_from_black_path()
    {
        Assert.False(GuidedSommelierCatalog.TryResolveBranchPath(
            ["q0_espresso", "q_es_body_black", "q_es_detail_chocolate"],
            out _,
            out _));

        Assert.True(GuidedSommelierCatalog.TryResolveBranchPath(
            ["q0_espresso", "q_es_body_milk", "q_es_detail_chocolate"],
            out var resolved,
            out var error), error);
        Assert.Contains("Mocha", GuidedSommelierCatalog.CollectMenuTargets(resolved));
    }

    [Fact]
    public void Vietnamese_bac_xiu_ends_without_temp_step()
    {
        Assert.True(GuidedSommelierCatalog.TryResolveBranchPath(
            ["q0_vietnamese", "q_vn_style_bacxiu"],
            out var resolved,
            out var error), error);
        Assert.True(resolved[^1].EndsBranch);
        Assert.Contains("Bạc Xỉu", GuidedSommelierCatalog.CollectMenuTargets(resolved));
    }

    [Fact]
    public void Specialty_path_length_and_option_ids_unchanged()
    {
        Assert.Equal(
            ["q_sp_tried", "q_sp_profile", "q_sp_adventure", "q_sp_format"],
            GuidedSommelierCatalog.Branches[GuidedSommelierCatalog.BranchSpecialty]);

        Assert.True(GuidedSommelierCatalog.TryResolveBranchPath(
            [
                "q0_specialty",
                "q_sp_tried_first",
                "q_sp_profile_floral",
                "q_sp_adventure_safe",
                "q_sp_format_one"
            ],
            out var resolved,
            out var error), error);
        Assert.Equal(5, resolved.Count);
        Assert.True(GuidedSommelierRecommendationEngine.IsSpecialtyCoffeePath(resolved));
    }

    [Fact]
    public void Menu_target_filter_hard_selects_named_drink()
    {
        var coco = Guid.NewGuid();
        var fame = Guid.NewGuid();
        var rows = new[]
        {
            new MenuItemScoringRow(coco, "Coco Bơ", 65000, null, null, null, null, new DrinkSensoryProfile(), "Signature"),
            new MenuItemScoringRow(fame, "Dưa Fame", 65000, null, null, null, null, new DrinkSensoryProfile(), "Signature")
        };
        var selected = new[]
        {
            new GuidedOptionSeed(
                "q0_signature",
                "Signature",
                "signature",
                new DrinkSensoryProfile(),
                CategoryIntentKey: BeverageFamilyGrounding.Signature,
                BranchKey: GuidedSommelierCatalog.BranchSignature),
            new GuidedOptionSeed(
                "q_sg_feel_creamy",
                "🥥 Béo ngậy",
                "béo ngậy",
                new DrinkSensoryProfile { Texture = "velvet" },
                CategoryIntentKey: BeverageFamilyGrounding.Signature,
                FlavorTagsJson: """["Coco Bơ"]""")
        };

        var ranked = GuidedSommelierRecommendationEngine.Rank(
            GuidedSommelierCatalog.MergeGuestHints(selected),
            selected,
            rows,
            take: 3);

        Assert.Single(ranked);
        Assert.Equal(coco, ranked[0].MenuItemId);
    }

    [Theory]
    [InlineData(BeverageFamilyGrounding.Espresso, "Espresso", "Latte", true)]
    [InlineData(BeverageFamilyGrounding.Espresso, "Cold Brew", "Cold Brew", false)]
    [InlineData(BeverageFamilyGrounding.ColdBrew, "Cold Brew", "Cold Brew Cam", true)]
    [InlineData(BeverageFamilyGrounding.Vietnamese, "Vietnamese Coffee", "Bạc Xỉu", true)]
    [InlineData(BeverageFamilyGrounding.Vietnamese, "Espresso", "Latte", false)]
    public void Narrow_coffee_families_do_not_bleed(
        string family,
        string category,
        string name,
        bool expected)
    {
        Assert.Equal(expected, BeverageFamilyGrounding.Matches(family, category, name));
    }
}
