using System.Text;

namespace aberaTech.Scheduling.Alerts;

/// <summary>Reads the calendar from its secret iCal address.</summary>
/// <remarks>
/// The address is a credential: anyone holding it reads the calendar. It is
/// never logged and never put in an exception message. The client is
/// registered with its loggers removed, and Program.cs filters it out of
/// the request traces.
/// </remarks>
public sealed class CalendarFeed(HttpClient http, AlertsOptions options)
{
    /// <summary>A calendar with years of history is a few megabytes. Past this it is not a calendar.</summary>
    public const int MaxBytes = 20 * 1024 * 1024;

    public async Task<string> FetchAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, options.CalendarIcsUrl);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new CalendarFeedException($"HTTP {(int)response.StatusCode}");
        }

        if (response.Content.Headers.ContentLength > MaxBytes)
        {
            throw new CalendarFeedException("The feed is larger than 20 MB.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
            {
                throw new CalendarFeedException("The feed is larger than 20 MB.");
            }

            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}

/// <summary>What Pushover said. The error is safe to show and log: a status or an exception type.</summary>
public sealed record PushoverResult(bool Ok, string? Error)
{
    public string Outcome => Ok ? "sent" : $"failed: {Error}";
}

/// <summary>
/// One Pushover message at emergency priority. It sounds again every
/// <see cref="RetrySeconds"/> until the owner acknowledges it in the
/// Pushover app, for at most 50 sounds or <see cref="ExpireSeconds"/>,
/// whichever comes first. Repeating until acknowledged is the point of the
/// feature: a single sound is missed.
/// </summary>
/// <remarks>
/// A send that failed before Pushover took it (a 5xx, or no connection) is
/// tried once more after <see cref="AlertsOptions.PushoverRetrySeconds"/>.
/// A timeout is not: the first request may have been delivered, and one
/// alert means one.
/// </remarks>
public sealed class PushoverClient(HttpClient http, AlertsOptions options)
{
    public const string Endpoint = "https://api.pushover.net/1/messages.json";

    /// <summary>
    /// Emergency: bypasses Pushover's quiet hours and repeats until
    /// acknowledged. With the app's Critical Alerts setting on, an iPhone
    /// plays it through the silent switch and Focus too. Every alert and the
    /// test button. See pushover.net/api#priority.
    /// </summary>
    public const int EmergencyPriority = 2;

    /// <summary>Seconds between the sounds of an emergency message. Pushover's floor is 30.</summary>
    public const int RetrySeconds = 60;

    /// <summary>
    /// When an unacknowledged emergency message stops: 3 hours, Pushover's
    /// ceiling. Its 50-sound cap ends it first, 50 minutes in at 60 seconds.
    /// </summary>
    public const int ExpireSeconds = 10800;

    public const int MaxTitle = 250;

    public const int MaxMessage = 1024;

    public async Task<PushoverResult> SendAsync(string title, string message, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var last = attempt == 2;
            try
            {
                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["token"] = options.PushoverAppToken ?? "",
                    ["user"] = options.PushoverUserKey ?? "",
                    ["title"] = AlertText.Title(title),
                    ["message"] = message.Length <= MaxMessage ? message : message[..MaxMessage],
                    ["priority"] = EmergencyPriority.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["retry"] = RetrySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["expire"] = ExpireSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
                using var response = await http.PostAsync(Endpoint, content, cancellationToken);

                if (response.IsSuccessStatusCode) return new PushoverResult(true, null);

                var failure = $"HTTP {(int)response.StatusCode}";
                if (last || (int)response.StatusCode < 500) return new PushoverResult(false, failure);
            }
            catch (HttpRequestException exception) when (!last && exception.HttpRequestError
                                                             is HttpRequestError.ConnectionError
                                                             or HttpRequestError.NameResolutionError)
            {
                // Never reached Pushover: nothing was delivered. Try once more.
            }
            catch (HttpRequestException exception)
            {
                return new PushoverResult(false, exception.HttpRequestError == HttpRequestError.Unknown
                    ? nameof(HttpRequestException)
                    : exception.HttpRequestError.ToString());
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new PushoverResult(false, "Timeout");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, options.PushoverRetrySeconds)), cancellationToken);
        }
    }
}
