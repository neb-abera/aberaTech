using System.IO.Compression;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Ingest;
using NodaTime;
using NodaTime.Text;

namespace aberaTech.Fitness.IntervalsIcu;

/// <summary>
/// An intervals.icu activity as this library's activity: read from the
/// original FIT file when there is one, from the listing when there is not.
/// </summary>
/// <remarks>
/// The file is the better witness — it is what a manual upload would have
/// been, laps and sub-sport included — so it is read first and the listing
/// only fills in what the file does not carry: the name the athlete gave the
/// session, and the trainer flag when the file's sub-sport said nothing.
/// Like Strava, intervals.icu has no ruck; the name decides, as it does on
/// every other path into this log.
/// </remarks>
public static class IntervalsIcuMapping
{
    public const string Source = "intervals-icu";

    public static string ExternalId(string activityId) => $"intervals:{activityId}";

    /// <summary>The activity, from the file when it parses and the listing otherwise.</summary>
    public static Activity? Map(IcuActivity summary, byte[]? file)
    {
        var activity = file is null ? null : FromFile(file);
        activity ??= FromSummary(summary);
        if (activity is null) return null;

        var name = summary.Name ?? "";
        activity.Source = Source;
        activity.ExternalId = ExternalId(summary.Id);
        activity.Name = name;
        activity.Indoor ??= summary.Trainer;

        // The name is the only place a ruck is ever declared.
        if (name.Contains("ruck", StringComparison.OrdinalIgnoreCase)) activity.Sport = "ruck";
        if (activity.Sport == "ruck") activity.LoadKg ??= RuckLoad.Parse(name);

        return activity;
    }

    /// <summary>The file as the device wrote it, gunzipped if intervals.icu stored it so.</summary>
    internal static Activity? FromFile(byte[] file)
    {
        try
        {
            using var stream = Open(file);
            return FitImport.Parse(stream);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return null;
        }
    }

    private static Stream Open(byte[] file)
    {
        // gzip magic: 1f 8b.
        if (file.Length > 2 && file[0] == 0x1f && file[1] == 0x8b)
        {
            using var gz = new GZipStream(new MemoryStream(file), CompressionMode.Decompress);
            var inflated = new MemoryStream();
            gz.CopyTo(inflated);
            inflated.Position = 0;
            return inflated;
        }

        return new MemoryStream(file);
    }

    internal static Activity? FromSummary(IcuActivity summary)
    {
        var started = Started(summary);
        if (started is null) return null;

        var seconds = summary.MovingTimeSeconds ?? summary.ElapsedTimeSeconds ?? 0;
        if (seconds <= 0) return null;

        return new Activity
        {
            Id = Guid.NewGuid(),
            Source = Source,
            ExternalId = ExternalId(summary.Id),
            StartedAt = started.Value,
            Sport = MapSport(summary.Type ?? ""),
            Name = summary.Name ?? "",
            DistanceMeters = summary.DistanceMeters is > 0 ? summary.DistanceMeters : null,
            DurationSeconds = seconds,
            AverageHr = Hr(summary.AverageHeartRate),
            MaxHr = Hr(summary.MaxHeartRate),
            Indoor = summary.Trainer
        };
    }

    /// <summary>
    /// The start as an instant. intervals.icu gives a zoned start_date and a
    /// wall-clock start_date_local; the zoned one is the truth, and the local
    /// one is read as UTC only when it is all there is.
    /// </summary>
    internal static Instant? Started(IcuActivity summary)
    {
        if (summary.StartDate is { } zoned && IsoInstant.Parse(zoned) is { } instant) return instant;

        if (summary.StartDateLocal is { } local)
        {
            var parsed = LocalDateTimePattern.ExtendedIso.Parse(local);
            if (parsed.Success) return parsed.Value.InUtc().ToInstant();
        }

        return null;
    }

    public static string MapSport(string type) => type.Trim().ToLowerInvariant() switch
    {
        "run" or "trailrun" or "virtualrun" or "treadmill" => "run",
        "walk" or "hike" => "ruck",
        "weighttraining" or "crossfit" or "workout" or "strength" => "strength",
        _ => "other"
    };

    private static int? Hr(double? bpm) => bpm is > 0 ? (int)Math.Round(bpm.Value) : null;
}
