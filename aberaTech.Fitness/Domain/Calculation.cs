namespace aberaTech.Fitness.Domain;

/// <summary>
/// One line of arithmetic behind a result: what was computed, the expression
/// with this athlete's numbers substituted, and the answer.
/// </summary>
/// <param name="Label">What this step establishes.</param>
/// <param name="Expression">The formula with real numbers in it, not symbols.</param>
/// <param name="Value">The result, formatted with its unit.</param>
/// <param name="CitationId">The <see cref="Citations"/> key backing the step, when one does.</param>
public sealed record CalculationStep(string Label, string Expression, string Value, string? CitationId = null);

/// <summary>Collects <see cref="CalculationStep"/>s in the order they were computed.</summary>
/// <remarks>
/// Every number this engine shows should be reconstructible by hand from the
/// trace it ships with. A model that cannot show its arithmetic is a model an
/// athlete has to take on faith, and this one asks for training years on the
/// strength of its answers.
/// </remarks>
public sealed class CalculationTrace
{
    private readonly List<CalculationStep> _steps = [];

    public IReadOnlyList<CalculationStep> Steps => _steps;

    public CalculationTrace Add(string label, string expression, string value, string? citationId = null)
    {
        _steps.Add(new CalculationStep(label, expression, value, citationId));
        return this;
    }

    public CalculationTrace Add(CalculationStep step)
    {
        _steps.Add(step);
        return this;
    }

    public CalculationTrace AddRange(IEnumerable<CalculationStep> steps)
    {
        _steps.AddRange(steps);
        return this;
    }
}
