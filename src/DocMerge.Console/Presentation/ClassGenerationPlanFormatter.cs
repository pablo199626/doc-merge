using DocMerge.Domain.Generation;

namespace DocMerge.Console.Presentation;

public static class ClassGenerationPlanFormatter
{
    public static string Format(ClassGenerationPlan plan, bool simulation)
    {
        var lines = new List<string>
        {
            simulation ? "Simulación de generación de anexos:" : "Resumen de generación de anexos:",
        };
        foreach (var classPlan in plan.Classes)
        {
            var counts = classPlan.Rows.GroupBy(row => row.Resolution.ResolutionType)
                .ToDictionary(group => group.Key, group => group.Count());
            lines.Add(
                $"- {classPlan.Roster.ClassIdentifier.CanonicalCode}: " +
                $"listado={classPlan.Rows.Count}, " +
                $"encontrados={Count(counts, HistoricalResolutionType.Found)}, " +
                $"nuevos={Count(counts, HistoricalResolutionType.NewStudent)}, " +
                $"conflictos_omitidos={Count(counts, HistoricalResolutionType.Conflict)}, " +
                $"documento={(classPlan.ProducesDocument ? "sí" : "no")}");
        }

        lines.AddRange(FormatIssues(plan.Issues));
        return string.Join(Environment.NewLine, lines);
    }

    private static IEnumerable<string> FormatIssues(IReadOnlyList<GenerationIssue> issues)
    {
        if (issues.Count == 0)
        {
            return ["Sin incidencias pendientes de revisión manual."];
        }

        return new[] { $"Avisos para revisión manual ({issues.Count}):" }
            .Concat(issues.Select(issue =>
                $"- [{issue.Code}] {issue.StudentName ?? issue.ClassIdentifier?.CanonicalCode}: " +
                issue.Detail));
    }

    private static int Count(
        IReadOnlyDictionary<HistoricalResolutionType, int> counts,
        HistoricalResolutionType type) => counts.GetValueOrDefault(type);
}
