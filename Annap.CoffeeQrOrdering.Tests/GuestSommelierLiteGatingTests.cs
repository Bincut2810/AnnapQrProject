using Annap.CoffeeQrOrdering.Web.GuestExperience;

using Annap.CoffeeQrOrdering.Web.Internal;



namespace Annap.CoffeeQrOrdering.Tests;



public sealed class GuestSommelierLiteGatingTests

{

    [Fact]

    public void Default_seed_catalog_is_compatible()

    {

        var catalog = GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.Questions);

        var compat = GuestSommelierLiteCompatibility.Assess(catalog);



        Assert.True(compat.IsCompatible, compat.ReasonCode);

        Assert.Empty(compat.MissingOptionIds);

    }



    [Theory]

    [InlineData("sweet", "yes", "low", "hot")]

    [InlineData("balanced", "no", "yes", "iced")]

    [InlineData("strong", "either", "no", "either")]

    [InlineData("refreshing", "no", "no", "iced")]

    public void TryMap_valid_preferences_produces_resolvable_option_ids(

        string taste,

        string milk,

        string caffeine,

        string temperature)

    {

        var catalog = GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.Questions);

        var result = GuestSommelierLiteOptionMapper.TryMap(catalog, taste, milk, caffeine, temperature);



        Assert.True(result.Success, result.ErrorMessage);

        Assert.True(result.CatalogCompatible);

        Assert.Equal(4, result.OptionIds.Count);

        Assert.DoesNotContain(result.OptionIds, id => id.StartsWith("q_sc_", StringComparison.OrdinalIgnoreCase));

    }



    [Fact]

    public void Missing_required_option_returns_incompatible_catalog()

    {

        var catalog = GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.Questions);

        var q1 = catalog.First(q => q.QuestionId == "q1");

        var trimmed = q1.Options.Where(o => !string.Equals(o.OptionId, "q1_light", StringComparison.OrdinalIgnoreCase)).ToList();

        var broken = catalog

            .Select(q => q.QuestionId == "q1" ? q with { Options = trimmed } : q)

            .ToList();



        var compat = GuestSommelierLiteCompatibility.Assess(broken);



        Assert.False(compat.IsCompatible);

        Assert.Equal("required_options_missing", compat.ReasonCode);

        Assert.Contains("q1_light", compat.MissingOptionIds);

    }



    [Fact]

    public void Missing_core_question_returns_incompatible_catalog()

    {

        var catalog = GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.Questions)

            .Where(q => q.QuestionId != "q4")

            .ToList();



        var compat = GuestSommelierLiteCompatibility.Assess(catalog);



        Assert.False(compat.IsCompatible);

        Assert.Equal("core_questions_missing", compat.ReasonCode);

    }



    [Fact]

    public void Unknown_preference_does_not_crash_and_fails_validation()

    {

        var catalog = GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.Questions);

        var result = GuestSommelierLiteOptionMapper.TryMap(catalog, "bogus", "yes", "low", "hot");



        Assert.False(result.Success);

        Assert.Equal("invalid_preference", result.ErrorCode);

        Assert.True(result.CatalogCompatible);

        Assert.Empty(result.OptionIds);

    }



    [Fact]

    public void Incompatible_catalog_map_result_is_not_success()

    {

        var catalog = GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.Questions)

            .Where(q => q.QuestionId != "q2")

            .ToList();

        var result = GuestSommelierLiteOptionMapper.TryMap(catalog, "balanced", "yes", "low", "hot");



        Assert.False(result.Success);

        Assert.False(result.CatalogCompatible);

        Assert.Equal("core_questions_missing", result.ErrorCode);

    }



    [Theory]

    [InlineData(true, true, SommelierLiteUiState.Offered)]

    [InlineData(true, false, SommelierLiteUiState.Hidden)]

    [InlineData(false, true, SommelierLiteUiState.Hidden)]

    [InlineData(false, false, SommelierLiteUiState.Hidden)]

    public void Cta_gating_depends_on_slim_arrival_and_cms_only(

        bool showSlimArrival,

        bool sommelierEnabled,

        SommelierLiteUiState expected)

    {

        var state = GuestSommelierLiteGating.Resolve(showSlimArrival, sommelierEnabled);



        Assert.Equal(expected, state);

        Assert.Equal(

            showSlimArrival && sommelierEnabled,

            GuestSommelierLiteGating.ShowCta(showSlimArrival, sommelierEnabled));

    }



    [Theory]

    [InlineData(true)]

    [InlineData(false)]

    public void Catalog_compatibility_does_not_affect_cta_gating(bool _)

    {

        const bool showSlimArrival = true;

        const bool sommelierEnabled = true;



        var state = GuestSommelierLiteGating.Resolve(showSlimArrival, sommelierEnabled);



        Assert.Equal(SommelierLiteUiState.Offered, state);

        Assert.True(GuestSommelierLiteGating.ShowCta(showSlimArrival, sommelierEnabled));

    }



    [Fact]

    public void Guest_sommelier_lite_js_keeps_cta_when_config_incompatible()

    {

        var jsPath = Path.Combine(

            AppContext.BaseDirectory,

            "..", "..", "..", "..",

            "Annap.CoffeeQrOrdering.Web",

            "wwwroot",

            "js",

            "guest-sommelier-lite.js");

        jsPath = Path.GetFullPath(jsPath);

        Assert.True(File.Exists(jsPath), $"Missing {jsPath}");



        var source = File.ReadAllText(jsPath);



        Assert.DoesNotContain("disableSommelierUi", source, StringComparison.Ordinal);

        Assert.Contains("incompatibleSheetMessage", source, StringComparison.Ordinal);

        Assert.Contains("data-somm-capability-notice", source, StringComparison.Ordinal);

    }

}


