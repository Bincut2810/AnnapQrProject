using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

public sealed class ExperienceGuidedQuestion : AuditableEntity
{
    /// <summary>Stable id aligned with guest payloads (e.g. q1).</summary>
    public string ExternalKey { get; set; } = "";

    /// <summary>Groups questions into a named questionnaire (e.g. "atelier_v2"). Enables seasonal/campaign sets.</summary>
    public string SetKey { get; set; } = "";

    public string Prompt { get; set; } = "";

    /// <summary>Longer host-facing line shown in CMS and optional guest copy.</summary>
    public string? Description { get; set; }

    public int SortOrder { get; set; }
    public bool IsOptional { get; set; }
    public bool IsEnabled { get; set; } = true;

    public List<ExperienceGuidedOption> Options { get; set; } = [];
}
