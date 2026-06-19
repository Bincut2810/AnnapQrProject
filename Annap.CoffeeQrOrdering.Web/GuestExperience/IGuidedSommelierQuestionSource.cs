namespace Annap.CoffeeQrOrdering.Web.GuestExperience;

/// <summary>
/// Future admin/CMS hook: supply guided question sets and option→sensory mappings.
/// Today use <see cref="GuidedSommelierCatalog.Questions"/>.
/// </summary>
public interface IGuidedSommelierQuestionSource
{
    IReadOnlyList<GuidedQuestionSeed> GetQuestions(string? culture);
}
