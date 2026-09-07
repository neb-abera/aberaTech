using aberaTech.Fitness.Ingest;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

public sealed class RuckLoadTests
{
    private const double Lb = 1 / 2.2046226218;

    [Theory]
    [InlineData("Ruck 45lb", 45 * Lb)]
    [InlineData("12 mi ruck @ 45 lbs", 45 * Lb)]
    [InlineData("#35 ruck", 35 * Lb)]
    [InlineData("Ruck 35#", 35 * Lb)]
    [InlineData("Camp Mackall 55-pound", 55 * Lb)]
    [InlineData("Ruck 20kg", 20.0)]
    [InlineData("ruck 20.4 kg", 20.4)]
    [InlineData("Morning Ruck", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("ruck 500 lb", null)] // nobody carries that; a typo, not a load
    public void Reads_a_load_out_of_the_name_in_either_unit(string? name, double? expectedKg)
    {
        var parsed = RuckLoad.Parse(name);

        if (expectedKg is null) Assert.Null(parsed);
        else Assert.Equal(expectedKg.Value, parsed!.Value, precision: 3);
    }

    [Fact]
    public void Garmin_csv_rucks_carry_their_named_load_and_runs_do_not()
    {
        const string export =
            "Activity Type,Date,Favorite,Title,Distance,Calories,Time,Avg HR,Max HR\n" +
            "Rucking,2026-06-01 08:00:00,false,\"Ruck 45lb\",\"12.00\",900,\"2:58:00\",140,155\n" +
            "Running,2026-06-02 08:00:00,false,\"45lb of regret\",\"5.00\",300,\"30:00\",150,165\n";

        var activities = GarminActivitiesCsv.Parse(export, DateTimeZoneProviders.Tzdb["Etc/UTC"]);

        var ruck = Assert.Single(activities, a => a.Sport == "ruck");
        Assert.Equal(45 * Lb, ruck.LoadKg!.Value, precision: 3);
        Assert.Null(Assert.Single(activities, a => a.Sport == "run").LoadKg);
    }

    [Fact]
    public void Hevy_csv_keeps_a_timed_hold()
    {
        const string export =
            "title,start_time,end_time,description,exercise_title,superset_id,exercise_notes,set_index,set_type,weight_lbs,reps,distance_miles,duration_seconds,rpe\n" +
            "\"Core\",\"19 Aug 2026, 20:20\",\"19 Aug 2026, 20:43\",\"\",\"Plank\",\"\",\"\",0,\"normal\",,,,150,\n" +
            "\"Core\",\"19 Aug 2026, 20:20\",\"19 Aug 2026, 20:43\",\"\",\"Pull Up\",\"\",\"\",1,\"normal\",,11,,,\n";

        var activity = Assert.Single(HevyCsv.Parse(export, DateTimeZoneProviders.Tzdb["Etc/UTC"]));

        Assert.Equal(150, activity.Sets.Single(s => s.Exercise == "Plank").DurationSeconds);
        Assert.Null(activity.Sets.Single(s => s.Exercise == "Pull Up").DurationSeconds);
    }
}
