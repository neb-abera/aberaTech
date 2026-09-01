using System.IO.Compression;
using System.Text;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Ingest;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The archive Garmin actually emails when you ask for your data, and its
/// units — centimetres, milliseconds, and two epoch clocks where only one is
/// really UTC. Getting any of those wrong turns a 3 km run into 3 metres or
/// 30 km, and the resulting VDOT would still look like a number.
/// </summary>
public sealed class GarminExportJsonTests
{
    private const string Export = """
        [
          {
            "summarizedActivitiesExport": [
              {
                "activityId": 24171185538,
                "activityType": "treadmill_running",
                "name": "Run (MAF intensity)",
                "startTimeGmt": 1787803056000.0,
                "startTimeLocal": 1787813856000.0,
                "distance": 301541.9921875,
                "duration": 1199577.0263671875,
                "avgHr": 153.0,
                "maxHr": 161.0
              },
              {
                "activityId": 24171185539,
                "activityType": "hiking",
                "name": "Ruck",
                "startTimeGmt": 1787903056000.0,
                "distance": 800000.0,
                "duration": 3600000.0
              }
            ]
          }
        ]
        """;

    [Fact]
    public void Reads_centimetres_as_metres_and_milliseconds_as_seconds()
    {
        var run = GarminExportJson.Parse(Export).Single(a => a.Sport == "run");

        Assert.Equal(3015.42, run.DistanceMeters!.Value, precision: 2);
        Assert.Equal(1199.577, run.DurationSeconds, precision: 3);
        Assert.Equal(153, run.AverageHr);
        Assert.Equal(161, run.MaxHr);
        Assert.Equal("Run (MAF intensity)", run.Name);
    }

    [Fact]
    public void Times_the_activity_by_the_gmt_clock_not_the_local_one()
    {
        var run = GarminExportJson.Parse(Export).Single(a => a.Sport == "run");

        // startTimeLocal is the same instant already shifted three hours; using
        // it would date every activity by the watch's wall clock.
        Assert.Equal(Instant.FromUnixTimeMilliseconds(1787803056000L), run.StartedAt);
    }

    [Fact]
    public void Keys_activities_by_garmins_own_id_so_re_importing_updates()
    {
        var first = GarminExportJson.Parse(Export).Select(a => a.ExternalId).ToArray();
        var second = GarminExportJson.Parse(Export).Select(a => a.ExternalId).ToArray();

        Assert.Equal(first, second);
        Assert.Contains("garmin:24171185538", first);
    }

    [Fact]
    public void Maps_garmins_snake_case_types_onto_sports()
    {
        var activities = GarminExportJson.Parse(Export);

        Assert.Equal("run", activities.Single(a => a.Name == "Run (MAF intensity)").Sport);
        Assert.Equal("ruck", activities.Single(a => a.Name == "Ruck").Sport);
    }

    [Fact]
    public void Skips_records_with_no_usable_clock_or_duration()
    {
        const string partial = """
            [{"summarizedActivitiesExport":[{"activityId":1,"activityType":"running","duration":0,"startTimeGmt":1787803056000}]}]
            """;

        Assert.Empty(GarminExportJson.Parse(partial));
    }
}

/// <summary>
/// That a file is read for what it is. The page no longer asks which button a
/// download belongs to, so everything here rests on the bytes deciding.
/// </summary>
public sealed class ImportTests
{
    private static readonly DateTimeZone Utc = DateTimeZoneProviders.Tzdb["Etc/UTC"];

    private const string Summaries = """
        [{"summarizedActivitiesExport":[
          {"activityId":11,"activityType":"running","name":"One","startTimeGmt":1787803056000,"distance":500000.0,"duration":1800000.0,"avgHr":150},
          {"activityId":12,"activityType":"running","name":"Two","startTimeGmt":1787903056000,"distance":600000.0,"duration":2400000.0,"avgHr":152}
        ]}]
        """;

    private const string GarminCsv =
        "Activity Type,Date,Title,Distance,Time,Avg HR,Max HR\n" +
        "Running,2026-08-27 06:57:36,Treadmill Running,3.02,0:20:00,153,161\n";

    private const string HevyCsvExport =
        "title,start_time,end_time,exercise_title,set_index,weight_lbs,reps\n" +
        "\"Evening workout\",\"19 Aug 2026, 20:20\",\"19 Aug 2026, 20:43\",\"Bench Press (Barbell)\",0,135,12\n";

    private static ImportResult Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return Import.Read(stream, Utc);
    }

    private static ImportResult Read(string text) => Read(Encoding.UTF8.GetBytes(text));

    private static byte[] Zip(params (string Name, byte[] Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var stream = archive.CreateEntry(name).Open();
                stream.Write(content);
            }
        }

        return buffer.ToArray();
    }

    private static byte[] Zip(params (string Name, string Content)[] entries) =>
        Zip([.. entries.Select(e => (e.Name, Encoding.UTF8.GetBytes(e.Content)))]);

    [Fact]
    public void Reads_a_garmin_activities_csv()
    {
        var result = Read(GarminCsv);

        Assert.Equal("export CSV", result.Kind);
        Assert.Equal("garmin-csv", Assert.Single(result.Activities).Source);
    }

    [Fact]
    public void Reads_a_hevy_export_csv_without_being_told_which_it_is()
    {
        var result = Read(HevyCsvExport);

        Assert.Equal("hevy-csv", Assert.Single(result.Activities).Source);
    }

    [Fact]
    public void Reads_the_summaries_json_on_its_own()
    {
        var result = Read(Summaries);

        Assert.Equal("Garmin activity summaries", result.Kind);
        Assert.Equal(2, result.Activities.Count);
    }

    [Fact]
    public void Walks_the_export_archive_for_the_file_that_matters()
    {
        var archive = Zip(
            ("DI_CONNECT/DI-Connect-Fitness/x_1_summarizedActivities.json", Summaries),
            ("DI_CONNECT/DI-Connect-User/user_profile.json", "{\"displayName\":\"someone\"}"),
            ("IT_ORDERS/orders.json", "[]"));

        var result = Read(archive);

        Assert.Equal("Garmin export archive", result.Kind);
        Assert.Equal(2, result.Activities.Count);
        Assert.All(result.Activities, a => Assert.Equal("garmin-export", a.Source));
    }

    [Fact]
    public void Opens_the_zip_garmin_nests_inside_the_archive()
    {
        var inner = Zip(("x_1_summarizedActivities.json", Summaries));
        var outer = Zip(("DI_CONNECT/DI-Connect-Uploaded-Files/UploadedFiles_0-_Part1.zip", inner));

        Assert.Equal(2, Read(outer).Activities.Count);
    }

    [Fact]
    public void Stops_descending_before_a_nest_of_zips_can_go_on_forever()
    {
        var third = Zip(("x_1_summarizedActivities.json", Summaries));
        var second = Zip(("b.zip", third));
        var first = Zip(("a.zip", second));

        // Three levels is past anything Garmin produces, so the payload at the
        // bottom is never reached — and the walk ends rather than recursing.
        Assert.Throws<FormatException>(() => Read(first));
    }

    [Fact]
    public void Describes_the_same_activity_once_however_many_files_carry_it()
    {
        // The archive splits its summaries across parts, which overlap.
        var archive = Zip(
            ("x_1_summarizedActivities.json", Summaries),
            ("x_2_summarizedActivities.json", Summaries));

        Assert.Equal(2, Read(archive).Activities.Count);
    }

    [Fact]
    public void Refuses_a_member_that_weighs_more_than_the_ceiling()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var stream = zip.CreateEntry("bomb.json").Open();
            // 65 MB of zeros: a few kilobytes on disk, past the per-member
            // ceiling once decompressed. Written in chunks so the test does not
            // hold what it is refusing.
            var chunk = new byte[1024 * 1024];
            for (var i = 0; i < 65; i++) stream.Write(chunk);
        }

        // Nothing importable came out of it, and nothing was held to find that out.
        Assert.Throws<FormatException>(() => Read(buffer.ToArray()));
    }

    [Fact]
    public void Says_what_it_wanted_when_a_file_is_none_of_these()
    {
        var error = Assert.Throws<FormatException>(() => Read("hello, this is not an export"));

        Assert.Contains("Garmin export archive", error.Message);
    }

    [Fact]
    public void Reimporting_the_same_archive_produces_the_same_identities()
    {
        var archive = Zip(("x_1_summarizedActivities.json", Summaries));

        var first = Read(archive).Activities.Select(a => a.ExternalId).OrderBy(x => x).ToArray();
        var second = Read(archive).Activities.Select(a => a.ExternalId).OrderBy(x => x).ToArray();

        Assert.Equal(first, second);
        Assert.All(first, Assert.NotNull);
    }
}

/// <summary>
/// The archive describes the same run twice — once in the summaries and again
/// as the original .fit — and the two have to become one row. A .fit cannot be
/// built by hand in a test, so the merge is exercised directly rather than
/// through a fixture that would have to be a binary blob in the repository.
/// </summary>
public sealed class ImportMergeTests
{
    private static Activity At(string source, Instant started, double? distance = null, int? hr = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Source = source,
            ExternalId = $"{source}:{started.ToUnixTimeSeconds()}",
            StartedAt = started,
            Sport = "run",
            DurationSeconds = 1800,
            DistanceMeters = distance,
            AverageHr = hr
        };

    private static readonly Instant Start = Instant.FromUnixTimeMilliseconds(1787803056000L);

    [Fact]
    public void Folds_the_original_fit_into_the_summary_of_the_same_run()
    {
        var summary = At("garmin-export", Start, distance: 5000);
        // The same run, a few seconds off as the two files record it, and
        // carrying the heart rate the summary lacks.
        var fit = At("garmin-fit", Start.Plus(Duration.FromSeconds(20)), distance: 5001, hr: 150);

        var merged = Import.Merge([summary], [fit], fromArchive: true);

        var only = Assert.Single(merged);
        Assert.Equal("garmin-export", only.Source);
        Assert.Equal(5000, only.DistanceMeters);
        Assert.Equal(150, only.AverageHr);
    }

    [Fact]
    public void Keeps_a_fit_the_summaries_never_mention()
    {
        var summary = At("garmin-export", Start);
        var elsewhere = At("garmin-fit", Start.Plus(Duration.FromHours(4)));

        var merged = Import.Merge([summary], [elsewhere], fromArchive: true);

        Assert.Equal(2, merged.Count);
        // Re-importing the archive has to land it on the same row again.
        Assert.All(merged, a => Assert.Equal("garmin-export", a.Source));
        Assert.Contains(merged, a => a.ExternalId!.StartsWith("garmin:fit:", StringComparison.Ordinal));
    }

    [Fact]
    public void Leaves_a_fit_uploaded_on_its_own_exactly_as_it_was()
    {
        var fit = At("garmin-fit", Start);

        var only = Assert.Single(Import.Merge([], [fit], fromArchive: false));

        Assert.Equal("garmin-fit", only.Source);
        Assert.Equal(fit.ExternalId, only.ExternalId);
    }
}
