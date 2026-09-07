using Dynastream.Fit;
using NodaTime;
using Activity = aberaTech.Fitness.Data.Activity;
using Lap = aberaTech.Fitness.Data.Lap;

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

        var activity = new Activity
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
            MaxHr = session.GetMaxHeartRate(),
            Indoor = IsIndoor(session.GetSubSport())
        };

        // The laps as the watch recorded them, in order. A lap that never
        // ran — the zero-length one a stop leaves behind — is not a lap.
        var index = 0;
        foreach (var lap in messages.LapMesgs)
        {
            var seconds = lap.GetTotalTimerTime() ?? lap.GetTotalElapsedTime() ?? 0;
            if (seconds <= 0) continue;

            activity.Laps.Add(new Lap
            {
                Id = Guid.NewGuid(),
                ActivityId = activity.Id,
                Index = index++,
                DistanceMeters = lap.GetTotalDistance() is { } metres and > 0 ? metres : null,
                Seconds = seconds,
                AverageHr = lap.GetAvgHeartRate()
            });
        }

        return activity;
    }

    /// <summary>Whether the sub-sport says the session was indoors; null when it says nothing.</summary>
    internal static bool? IsIndoor(SubSport? subSport) => subSport switch
    {
        SubSport.Treadmill or SubSport.IndoorRunning or SubSport.VirtualActivity
            or SubSport.IndoorCycling or SubSport.IndoorRowing => true,
        SubSport.Generic or null => null,
        _ => false
    };

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
