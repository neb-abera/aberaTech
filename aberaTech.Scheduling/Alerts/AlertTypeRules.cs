namespace aberaTech.Scheduling.Alerts;

/// <summary>
/// One rule for what an event sends: when <paramref name="Field"/> contains
/// <paramref name="Text"/>, the event's alert is <paramref name="Type"/>.
/// The rules are kept in order and the first that matches wins.
/// </summary>
/// <param name="Field"><see cref="AlertTypeRuleFields.Title"/>, <see cref="AlertTypeRuleFields.Organizer"/> or <see cref="AlertTypeRuleFields.Location"/>.</param>
/// <param name="Text">
/// What the field contains, any case. Blank matches any event that has the
/// field: any location, or any organizer.
/// </param>
/// <param name="Type"><see cref="AlertTypes.Alarm"/>, <see cref="AlertTypes.Notification"/> or <see cref="AlertTypes.None"/>.</param>
public sealed record AlertTypeRule(string Field, string Text, string Type)
{
    /// <summary>The event matches when its field contains the text, any case.</summary>
    public bool Matches(PlannedAlert alert)
    {
        var value = Field switch
        {
            AlertTypeRuleFields.Title => alert.Title,
            AlertTypeRuleFields.Organizer => alert.Organizer,
            AlertTypeRuleFields.Location => alert.Location,
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(value)) return false;
        return Text.Length == 0 || value.Contains(Text, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>What a rule can look at.</summary>
public static class AlertTypeRuleFields
{
    /// <summary>The title as the page shows it, without #critical.</summary>
    public const string Title = "title";

    /// <summary>The organizer's email address. A domain such as army.mil matches every address at it.</summary>
    public const string Organizer = "organizer";

    public const string Location = "location";

    public static readonly IReadOnlyList<string> All = [Title, Organizer, Location];
}

/// <summary>The bounds every rule save is checked against, on the page, the phone and here.</summary>
public static class AlertTypeRuleBounds
{
    public const int MaxRules = 50;

    public const int MaxTextLength = 100;

    /// <summary>
    /// The rules as sent, trimmed, or the errors by field. A title rule needs
    /// text, since a blank one would match every event. Organizer and
    /// location may be blank, for "has any".
    /// </summary>
    public static (IReadOnlyList<AlertTypeRule>? Rules, Dictionary<string, string[]> Errors) Validate(
        IReadOnlyList<AlertTypeRuleRequest?>? rules)
    {
        var errors = new Dictionary<string, string[]>();
        if (rules is null)
        {
            errors["rules"] = ["Required: a list, empty to remove every rule."];
            return (null, errors);
        }

        if (rules.Count > MaxRules)
        {
            errors["rules"] = [$"At most {MaxRules} rules."];
            return (null, errors);
        }

        var valid = new List<AlertTypeRule>(rules.Count);
        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            var field = rule?.Field;
            var text = rule?.Text?.Trim() ?? "";
            var type = rule?.Type;
            if (field is null || !AlertTypeRuleFields.All.Contains(field))
            {
                errors[$"rules[{i}].field"] = ["\"title\", \"organizer\" or \"location\"."];
            }

            if (text.Length > MaxTextLength || text.Any(char.IsControl))
            {
                errors[$"rules[{i}].text"] = [$"At most {MaxTextLength} characters."];
            }
            else if (field == AlertTypeRuleFields.Title && text.Length == 0)
            {
                errors[$"rules[{i}].text"] = ["A title rule needs a word to look for."];
            }

            if (type is null || !AlertTypes.Choices.Contains(type))
            {
                errors[$"rules[{i}].type"] = ["\"none\", \"notification\" or \"alarm\"."];
            }

            if (field is not null && type is not null) valid.Add(new AlertTypeRule(field, text, type));
        }

        return errors.Count > 0 ? (null, errors) : (valid, errors);
    }
}

/// <summary>One rule as the page or the phone sends it.</summary>
public sealed record AlertTypeRuleRequest(string? Field, string? Text, string? Type);

/// <summary>The whole ordered list of rules, as one save.</summary>
public sealed record AlertTypeRulesRequest(IReadOnlyList<AlertTypeRuleRequest?>? Rules);
