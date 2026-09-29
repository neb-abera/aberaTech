using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace aberaTech.Scheduling.Alerts;

/// <summary>One sound the owner can pick. Custom is one uploaded to the Pushover account, not one of the built-ins.</summary>
public sealed record PushoverSound(string Name, string Description, bool Custom);

/// <summary>
/// The sounds the app's Pushover account offers: the account's own uploads
/// first, then Pushover's built-ins. Read from pushover.net/api#sounds at
/// most once an hour. When Pushover cannot be reached the built-ins stand
/// in, and Pushover is asked again a minute later.
/// </summary>
/// <remarks>
/// The request carries the app token in its query string, as the API
/// requires. It goes through <see cref="PushoverClient"/>, whose HTTP client
/// has its loggers removed, so the URL is never logged. A failure is logged
/// by its kind alone.
/// </remarks>
public sealed class PushoverSounds(IServiceScopeFactory scopes, IClock clock, ILogger<PushoverSounds> logger)
{
    /// <summary>How long a list read from Pushover is used before it is read again.</summary>
    public static readonly Duration Fresh = Duration.FromHours(1);

    /// <summary>How long the built-ins stand in after Pushover could not be reached.</summary>
    public static readonly Duration AfterFailure = Duration.FromMinutes(1);

    /// <summary>
    /// Pushover's built-in sounds and their names, as pushover.net/api#sounds
    /// lists them on 2026-09-29.
    /// </summary>
    public static readonly IReadOnlyList<PushoverSound> BuiltIn =
    [
        new("pushover", "Pushover (default)", false),
        new("bike", "Bike", false),
        new("bugle", "Bugle", false),
        new("cashregister", "Cash Register", false),
        new("classical", "Classical", false),
        new("cosmic", "Cosmic", false),
        new("falling", "Falling", false),
        new("gamelan", "Gamelan", false),
        new("incoming", "Incoming", false),
        new("intermission", "Intermission", false),
        new("magic", "Magic", false),
        new("mechanical", "Mechanical", false),
        new("pianobar", "Piano Bar", false),
        new("siren", "Siren", false),
        new("spacealarm", "Space Alarm", false),
        new("tugboat", "Tug Boat", false),
        new("alien", "Alien Alarm (long)", false),
        new("climb", "Climb (long)", false),
        new("persistent", "Persistent (long)", false),
        new("echo", "Pushover Echo (long)", false),
        new("updown", "Up Down (long)", false),
        new("vibrate", "Vibrate Only", false),
        new("none", "None (silent)", false)
    ];

    private static readonly HashSet<string> BuiltInNames = [.. BuiltIn.Select(sound => sound.Name)];

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<PushoverSound>? _sounds;
    private Instant _until;

    /// <summary>The account's sounds first, then the built-ins. Never empty.</summary>
    public async Task<IReadOnlyList<PushoverSound>> CurrentAsync(CancellationToken cancellationToken)
    {
        if (_sounds is { } cached && clock.GetCurrentInstant() < _until) return cached;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_sounds is { } again && clock.GetCurrentInstant() < _until) return again;

            IReadOnlyDictionary<string, string>? read;
            using (var scope = scopes.CreateScope())
            {
                read = await scope.ServiceProvider.GetRequiredService<PushoverClient>().SoundsAsync(cancellationToken);
            }

            if (read is null)
            {
                logger.LogWarning("Pushover's sound list could not be read. The built-in sounds stand in.");
                _sounds = BuiltIn;
                _until = clock.GetCurrentInstant() + AfterFailure;
            }
            else
            {
                _sounds =
                [
                    .. read.Where(sound => !BuiltInNames.Contains(sound.Key))
                        .Select(sound => new PushoverSound(sound.Key, sound.Value, true)),
                    .. BuiltIn
                ];
                _until = clock.GetCurrentInstant() + Fresh;
            }

            return _sounds;
        }
        finally
        {
            _gate.Release();
        }
    }
}
