using Annap.CoffeeQrOrdering.Web.GuestExperience;
using Annap.CoffeeQrOrdering.Web.Internal;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// AI Sommelier Lite mapped to atelier_v4 universal questions.
/// atelier_v5 is category-branched — Lite degrades as incompatible until remapped.
/// </summary>
public class GuestSommelierLiteGatingTests
{
    [Fact]
    public void Assess_marks_atelier_v5_incompatible()
    {
        var catalog = GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.AllQuestions);
        var result = GuestSommelierLiteCompatibility.Assess(catalog);

        Assert.False(result.IsCompatible);
        Assert.Equal("core_questions_missing", result.ReasonCode);
    }
}
