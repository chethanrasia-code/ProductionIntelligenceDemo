namespace ProductionIntelligence.Application;

public static class InsightValidator
{
    public static IReadOnlyList<string> Validate(InsightDraft draft, IReadOnlySet<Guid> knownEventIds)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(draft.Summary))
        {
            errors.Add("Insight summary is required.");
        }

        if (draft.EvidenceEventIds.Count == 0)
        {
            errors.Add("Insight summary must cite at least one source event.");
        }

        if (draft.EvidenceEventIds.Any(id => !knownEventIds.Contains(id)))
        {
            errors.Add("Insight summary cites an event that was not supplied as evidence.");
        }

        if (draft.Recommendations.Count == 0)
        {
            errors.Add("At least one recommendation is required.");
        }

        foreach (var recommendation in draft.Recommendations)
        {
            if (string.IsNullOrWhiteSpace(recommendation.Text))
            {
                errors.Add("Recommendation text is required.");
            }

            if (recommendation.EvidenceEventIds.Count == 0)
            {
                errors.Add("Each recommendation must cite at least one source event.");
            }

            if (recommendation.EvidenceEventIds.Any(id => !knownEventIds.Contains(id)))
            {
                errors.Add("A recommendation cites an event that was not supplied as evidence.");
            }
        }

        return errors;
    }
}
