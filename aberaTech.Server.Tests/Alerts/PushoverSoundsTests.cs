using System.Net;
using aberaTech.Scheduling.Alerts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// The sound list the Sound setting offers: the account's uploads first,
/// then the built-ins, read from Pushover at most once an hour, with the
/// built-ins standing in when Pushover cannot be reached. The token goes in
/// the request and nowhere else.
/// </summary>
public sealed class PushoverSoundsTests
{
    private const string Token = "app-token-sounds-tests-Zq9";
    private static readonly Instant Noon = Instant.FromUtc(2026, 10, 28, 12, 0);

    private readonly FakeClock _clock = new(Noon);
    private readonly RecordingHandler _pushover = new(() => RecordingHandler.Text(HttpStatusCode.OK, "{\"status\":1}"));
    private readonly RecordingLogger _log = new();

    private PushoverSounds Sounds()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new AlertsOptions { PushoverAppToken = Token });
        services.AddHttpClient<PushoverClient>().ConfigurePrimaryHttpMessageHandler(() => _pushover).RemoveAllLoggers();
        var provider = services.BuildServiceProvider();
        return new PushoverSounds(provider.GetRequiredService<IServiceScopeFactory>(), _clock, _log);
    }

    [Fact]
    public async Task The_accounts_uploads_come_first_then_the_built_ins()
    {
        _pushover.Sounds = () => RecordingHandler.SoundList(("aberaalarm", "Abera alarm (29.5 s)"));

        var sounds = await Sounds().CurrentAsync(CancellationToken.None);

        Assert.Equal(new PushoverSound("aberaalarm", "Abera alarm (29.5 s)", true), sounds[0]);
        Assert.Equal(PushoverSounds.BuiltIn, sounds.Skip(1));
        var url = Assert.Single(_pushover.SoundRequests);
        Assert.Equal($"https://api.pushover.net/1/sounds.json?token={Token}", url.ToString());
        Assert.Empty(_pushover.Requests);
    }

    [Fact]
    public async Task The_list_is_read_once_an_hour()
    {
        var catalog = Sounds();

        await catalog.CurrentAsync(CancellationToken.None);
        _clock.Now = Noon + Duration.FromMinutes(59);
        await catalog.CurrentAsync(CancellationToken.None);
        Assert.Single(_pushover.SoundRequests);

        _pushover.Sounds = () => RecordingHandler.SoundList(("aberaalarm", "Abera alarm"));
        _clock.Now = Noon + Duration.FromHours(1);
        var later = await catalog.CurrentAsync(CancellationToken.None);

        Assert.Equal(2, _pushover.SoundRequests.Count);
        Assert.Equal("aberaalarm", later[0].Name);
    }

    public static IEnumerable<object[]> Outages =>
    [
        ["HTTP 500"],
        ["HTTP 401"],
        ["no connection"],
        ["not JSON"],
        ["no sounds field"],
        ["too large"]
    ];

    [Theory]
    [MemberData(nameof(Outages))]
    public async Task When_pushover_cannot_answer_the_built_ins_stand_in_and_it_is_asked_again_a_minute_later(string outage)
    {
        _pushover.Sounds = outage switch
        {
            "HTTP 500" => () => RecordingHandler.Text(HttpStatusCode.InternalServerError, ""),
            "HTTP 401" => () => RecordingHandler.Text(HttpStatusCode.Unauthorized, "{\"status\":0}"),
            "no connection" => () => throw new HttpRequestException(HttpRequestError.ConnectionError),
            "not JSON" => () => RecordingHandler.Text(HttpStatusCode.OK, "<html>", "text/html"),
            "no sounds field" => () => RecordingHandler.Text(HttpStatusCode.OK, "{\"status\":1}", "application/json"),
            _ => () => RecordingHandler.Text(
                HttpStatusCode.OK, "{\"sounds\":{\"a\":\"" + new string('x', PushoverClient.MaxSoundsBytes) + "\"}}", "application/json")
        };
        var catalog = Sounds();

        Assert.Equal(PushoverSounds.BuiltIn, await catalog.CurrentAsync(CancellationToken.None));
        Assert.Contains(_log.Lines, line => line.Contains("built-in sounds stand in", StringComparison.Ordinal));
        Assert.DoesNotContain(_log.Lines, line => line.Contains(Token, StringComparison.Ordinal));

        _pushover.Sounds = () => RecordingHandler.SoundList(("aberaalarm", "Abera alarm"));
        _clock.Now = Noon + Duration.FromSeconds(59);
        Assert.Equal(PushoverSounds.BuiltIn, await catalog.CurrentAsync(CancellationToken.None));
        _clock.Now = Noon + Duration.FromMinutes(1);
        Assert.Equal("aberaalarm", (await catalog.CurrentAsync(CancellationToken.None))[0].Name);
    }

    [Fact]
    public async Task A_name_that_is_not_a_plain_sound_name_is_dropped_and_a_long_description_is_cut()
    {
        _pushover.Sounds = () => RecordingHandler.SoundList(
            ("good_one-2", new string('d', 300)),
            ("../x", "path"),
            ("with space", "space"),
            (new string('n', PushoverClient.MaxSoundName + 1), "long"));

        var custom = (await Sounds().CurrentAsync(CancellationToken.None)).Where(sound => sound.Custom).ToList();

        var only = Assert.Single(custom);
        Assert.Equal("good_one-2", only.Name);
        Assert.Equal(PushoverClient.MaxSoundDescription, only.Description.Length);
    }

    private sealed class RecordingLogger : ILogger<PushoverSounds>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
