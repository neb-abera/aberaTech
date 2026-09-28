namespace aberaTech.Scheduling.Alerts;

/// <summary>What one event's alert sends, and where that came from.</summary>
/// <param name="Type"><see cref="AlertTypes.None"/>, <see cref="AlertTypes.Notification"/> or <see cref="AlertTypes.Alarm"/>.</param>
/// <param name="From">"set" on the page, "critical" from the calendar's mark, or "default".</param>
public sealed record EffectiveType(string Type, string From);

/// <summary>
/// The three things an event can send. An alarm uses the alarm settings
/// (priority, repeat, stop, sound). A notification sounds once, at the
/// notification priority and sound. None sends nothing.
/// </summary>
/// <remarks>
/// A choice made on the page wins. Otherwise an event marked #critical in
/// the calendar is an alarm. Otherwise it follows
/// <see cref="AlertSettings.DefaultType"/>, which is none unless the owner
/// changes it.
/// </remarks>
public static class AlertTypes
{
    public const string None = "none";

    public const string Notification = "notification";

    public const string Alarm = "alarm";

    /// <summary>What the page sends to drop its choice, so the event follows the calendar and the default again.</summary>
    public const string Default = "default";

    public const string FromPage = "set";

    public const string FromMark = "critical";

    public const string FromDefault = "default";

    /// <summary>The longest type stored.</summary>
    public const int MaxLength = 16;

    /// <summary>What the page may choose for one event.</summary>
    public static readonly IReadOnlyList<string> Choices = [None, Notification, Alarm];

    /// <summary>
    /// What an unmarked event may send by default. Alarm is not offered:
    /// every event repeating until acknowledged is what #258 did, and the
    /// owner said that was not what he asked for.
    /// </summary>
    public static readonly IReadOnlyList<string> Defaults = [None, Notification];

    /// <summary>The page's choice, then the calendar's mark, then the default.</summary>
    public static EffectiveType Resolve(PlannedAlert alert, string? chosen, AlertSettings settings) =>
        chosen is not null ? new EffectiveType(chosen, FromPage)
        : alert.Critical ? new EffectiveType(Alarm, FromMark)
        : new EffectiveType(settings.DefaultType, FromDefault);
}
