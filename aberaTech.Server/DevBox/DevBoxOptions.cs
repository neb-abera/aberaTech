namespace aberaTech.Server.DevBox;

/// <summary>
/// Which virtual machine the owner page may start. The "DevBox" section.
/// </summary>
/// <remarks>
/// The subscription id is the one value that has to be set per deployment
/// (a container app environment variable, DevBox__SubscriptionId). The group
/// and name default to what devbox/README.md in repos-conventions built.
/// Without a subscription the feature is off: the status route answers
/// "not configured" and nothing else is mapped.
/// </remarks>
public sealed class DevBoxOptions
{
    public const string Section = "DevBox";

    public string? SubscriptionId { get; init; }

    public string ResourceGroup { get; init; } = "devbox-rg";

    public string VmName { get; init; } = "devbox";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SubscriptionId)
        && !string.IsNullOrWhiteSpace(ResourceGroup)
        && !string.IsNullOrWhiteSpace(VmName);
}
