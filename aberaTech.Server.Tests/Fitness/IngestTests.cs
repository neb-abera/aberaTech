using aberaTech.Fitness.Ingest;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

public sealed class HevyCsvTests
{
    private static readonly DateTimeZone Utc = DateTimeZoneProviders.Tzdb["Etc/UTC"];

    private const string Export =
        "title,start_time,end_time,description,exercise_title,superset_id,exercise_notes,set_index,set_type,weight_lbs,reps,distance_miles,duration_seconds,rpe\n" +
        "\"Evening workout\",\"19 Aug 2026, 20:20\",\"19 Aug 2026, 20:43\",\"\",\"Bench Press (Barbell)\",\"\",\"\",0,\"normal\",135,12,,,\n" +
        "\"Evening workout\",\"19 Aug 2026, 20:20\",\"19 Aug 2026, 20:43\",\"\",\"Bench Press (Barbell)\",\"\",\"\",1,\"normal\",155,8,,,\n" +
        "\"Morning workout\",\"15 May 2026, 11:42\",\"15 May 2026, 12:15\",\"\",\"Bench Press (Barbell)\",\"\",\"\",0,\"normal\",155,12,,,\n";

    [Fact]
    public void Groups_set_rows_into_workouts()
    {
        var activities = HevyCsv.Parse(Export, Utc);

        Assert.Equal(2, activities.Count);

        var evening = Assert.Single(activities, a => a.Name == "Evening workout");
        Assert.Equal("strength", evening.Sport);
        Assert.Equal("hevy-csv", evening.Source);
        Assert.Equal(2, evening.Sets.Count);
        Assert.Equal(23 * 60, evening.DurationSeconds);
    }

    [Fact]
    public void Converts_pound_columns_to_kilograms()
    {
        var activities = HevyCsv.Parse(Export, Utc);
        var set = activities.Single(a => a.Name == "Evening workout").Sets.Single(s => s.SetIndex == 1);

        Assert.Equal(155 / 2.2046226218, set.WeightKg, precision: 3);
        Assert.Equal(8, set.Reps);
    }

    [Fact]
    public void Reimporting_produces_the_same_external_ids()
    {
        var first = HevyCsv.Parse(Export, Utc).Select(a => a.ExternalId).OrderBy(x => x).ToArray();
        var second = HevyCsv.Parse(Export, Utc).Select(a => a.ExternalId).OrderBy(x => x).ToArray();
        Assert.Equal(first, second);
        Assert.All(first, Assert.NotNull);
    }

    [Fact]
    public void Refuses_files_that_are_not_hevy_exports()
    {
        Assert.Throws<FormatException>(() => HevyCsv.Parse("a,b,c\n1,2,3\n", Utc));
    }
}

public sealed class GarminActivitiesCsvTests
{
    private static readonly DateTimeZone Utc = DateTimeZoneProviders.Tzdb["Etc/UTC"];

    private const string Export =
        "Activity Type,Date,Favorite,Title,Distance,Calories,Time,Avg HR,Max HR\n" +
        "Treadmill Running,2026-08-27 19:12:33,false,\"Treadmill Running\",3.02,250,20:00,153,167\n" +
        "Running,2026-04-03 07:00:00,false,\"El Paso - 2 mile TT\",3.23,210,16:49,185,196\n" +
        "Strength Training,2026-08-19 20:20:00,false,\"Push day\",--,120,23:00,--,--\n" +
        "Rucking,2026-06-01 08:00:00,false,\"Ruck\",\"5.00\",500,\"1:24:58\",140,155\n";

    [Fact]
    public void Parses_summary_rows_with_garmin_quirks()
    {
        var activities = GarminActivitiesCsv.Parse(Export, Utc);

        Assert.Equal(4, activities.Count);

        var treadmill = Assert.Single(activities, a => a.Name == "Treadmill Running");
        Assert.Equal("run", treadmill.Sport);
        Assert.Equal(3020, treadmill.DistanceMeters!.Value, precision: 3);
        Assert.Equal(1200, treadmill.DurationSeconds);
        Assert.Equal(153, treadmill.AverageHr);

        var strength = Assert.Single(activities, a => a.Sport == "strength");
        Assert.Null(strength.AverageHr);
        Assert.Null(strength.DistanceMeters);

        var ruck = Assert.Single(activities, a => a.Sport == "ruck");
        Assert.Equal(1 * 3600 + 24 * 60 + 58, ruck.DurationSeconds);
    }

    [Theory]
    [InlineData("Treadmill Running", "run")]
    [InlineData("Track Running", "run")]
    [InlineData("Rucking", "ruck")]
    [InlineData("Hiking", "ruck")]
    [InlineData("Strength Training", "strength")]
    [InlineData("Pool Swimming", "other")]
    public void Maps_activity_types(string garminType, string sport)
    {
        Assert.Equal(sport, GarminActivitiesCsv.MapSport(garminType));
    }

    [Theory]
    [InlineData("20:00", 1200)]
    [InlineData("1:24:58", 5098)]
    [InlineData("00:20:00.0", 1200)]
    [InlineData("garbage", 0)]
    public void Parses_duration_formats(string value, double seconds)
    {
        Assert.Equal(seconds, GarminActivitiesCsv.ParseDuration(value));
    }
}
