using Dynastream.Fit;
using NodaTime;
using Activity = aberaTech.Fitness.Data.Activity;

namespace aberaTech.Fitness.Ingest;

/// <summary>
/// A single Garmin .fit activity file, decoded with Garmin's own SDK — the
/// rich-detail path, straight off the watch or a Connect activity export.
/// </summary>
public static class FitImport
{
    public static Activity? Parse(Stream stream)
    {
        var decode = new Decode();
        var listener = new FitListener();
        decode.MesgEvent += listener.OnMesg;

        if (!decode.IsFIT(stream)) return null;
        stream.Position = 0;
        if (!decode.Read(stream)) return null;

        var messages = listener.FitMessages;
        var session = messages.SessionMesgs.FirstOrDefault();
        if (session is null) return null;

        var startTime = session.GetStartTime()?.GetDateTime()
                        ?? messages.FileIdMesgs.FirstOrDefault()?.GetTimeCreated()?.GetDateTime();
        if (startTime is null) return null;

        var started = Instant.FromDateTimeUtc(System.DateTime.SpecifyKind(startTime.Value, DateTimeKind.Utc));

        var timerSeconds = session.GetTotalTimerTime() ?? session.GetTotalElapsedTime() ?? 0;
        if (timerSeconds <= 0) return null;

        return new Activity
        {
            Id = Guid.NewGuid(),
            Source = "garmin-fit",
            ExternalId = $"fit:{started}",
            StartedAt = started,
            Sport = MapSport(session.GetSport(), session.GetSubSport()),
            Name = "",
            DistanceMeters = session.GetTotalDistance(),
            DurationSeconds = timerSeconds,
            AverageHr = session.GetAvgHeartRate(),
            MaxHr = session.GetMaxHeartRate()
        };
    }

    internal static string MapSport(Sport? sport, SubSport? subSport) => sport switch
    {
        Sport.Running => "run",
        Sport.Hiking => "ruck",
        Sport.Walking when subSport == SubSport.CasualWalking => "other",
        Sport.Walking => "ruck",
        Sport.Training when subSport == SubSport.StrengthTraining => "strength",
        _ => "other"
    };
}
