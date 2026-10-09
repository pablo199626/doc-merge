using DocMerge.Domain.Transfer;

namespace DocMerge.Console.Presentation;

public static class TransferPlanFormatter
{
    private static readonly IReadOnlyDictionary<ActionType, string> ActionLabels =
        new Dictionary<ActionType, string>
        {
            [ActionType.Add] = "Añadir",
            [ActionType.Replace] = "Reemplazar",
            [ActionType.Keep] = "Conservar",
            [ActionType.Delete] = "Eliminar",
            [ActionType.Skip] = "Omitir",
        };

    private static readonly IReadOnlyDictionary<ActionType, string> CountLabels =
        new Dictionary<ActionType, string>
        {
            [ActionType.Add] = "anadir",
            [ActionType.Replace] = "reemplazar",
            [ActionType.Keep] = "conservar",
            [ActionType.Delete] = "eliminar",
            [ActionType.Skip] = "omitir",
        };

    public static string FormatSimulation(TransferPlan plan) =>
        FormatPlan(plan, "Simulación de transferencia:");

    public static string FormatExecution(TransferPlan plan) =>
        FormatPlan(plan, "Resumen de transferencia:");

    public static string FormatIssues(TransferPlan plan)
    {
        if (plan.Issues.Count == 0)
        {
            return "Sin incidencias pendientes de revisión manual.";
        }

        var lines = new List<string>
        {
            $"Incidencias para revisión manual ({plan.Issues.Count}):",
        };
        lines.AddRange(plan.Issues.Select(FormatIssue));
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatPlan(TransferPlan plan, string heading)
    {
        var lines = new List<string> { heading };
        lines.AddRange(plan.Actions.Select(action =>
            $"- {ActionLabels[action.ActionType]}: {action.Name}{FormatReason(action.Reason)}"));
        var totals = Enum.GetValues<ActionType>().Select(type =>
            $"{CountLabels[type]}={plan.ActionCounts[type]}");
        lines.Add($"Totales: {string.Join(", ", totals)}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatIssue(TransferIssue issue)
    {
        var references = issue.References.Count == 0
            ? string.Empty
            : $" [{string.Join("; ", issue.References)}]";
        return $"- {issue.Name}: {issue.Detail}{references}";
    }

    private static string FormatReason(IssueType? reason)
    {
        return reason is null ? string.Empty : $" ({IssueValue(reason.Value)})";
    }

    private static string IssueValue(IssueType type) => type switch
    {
        IssueType.DuplicatePdfEntry => "pdf_duplicado",
        IssueType.AmbiguousDestination => "destino_ambiguo",
        IssueType.MissingSource => "origen_ausente",
        IssueType.AmbiguousSource => "origen_ambiguo",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}
