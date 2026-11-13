using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using aberaTech.Fitness.Data;
using aberaTech.Fitness.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// A treadmill run on an incline is read as the flat run of the same cost,
/// so moving the treadmill from 0% to 1% does not read as lost fitness.
/// </summary>
public sealed class TreadmillGradeTests
{
    [Fact]
    public void One_percent_reads_as_four_and_a_half_percent_more_flat_distance()
    {
        // ACSM: 0.2·v' = 0.2·v + 0.9·v·0.01, so v' = 1.045·v.
        Assert.Equal(10_450, TreadmillGrade.FlatEquivalentMeters(10_000, 1), precision: 6);
        Assert.Equal(10_000, TreadmillGrade.FlatEquivalentMeters(10_000, 0), precision: 6);
    }

    [Theory]
    [InlineData("Treadmill 2%", 2.0)]
    [InlineData("Easy 0.5 % incline", 0.5)]
    [InlineData("Run at 0%", 0.0)]
    [InlineData("Treadmill Running", null)]
    [InlineData("4x1km at 105% effort", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void A_title_can_name_the_grade(string? title, double? expected)
    {
        Assert.Equal(expected, TreadmillGrade.FromTitle(title));
    }

    [Fact]
    public void The_title_beats_the_setting_and_the_setting_starts_on_its_date()
    {
        var since = new LocalDate(2026, 10, 1);

        Assert.Equal(0, TreadmillGrade.For(true, since.PlusDays(-1), "Easy", 1, since));
        Assert.Equal(1, TreadmillGrade.For(true, since, "Easy", 1, since));
        Assert.Equal(2, TreadmillGrade.For(true, since, "Test 2%", 1, since));
        Assert.Equal(0, TreadmillGrade.For(true, since, "Back to 0%", 1, since));
        Assert.Equal(0, TreadmillGrade.For(false, since, "Hill 4%", 1, since));
        Assert.Equal(0, TreadmillGrade.For(true, since, "Easy", null, null));
    }

    [Fact]
    public void The_same_fitness_at_one_percent_reads_the_same_as_at_zero()
    {
        // September at 0% and 6:00/km; October the same effort at 1%, which the
        // belt shows 4.5% slower. Before the grade was read, October looked
        // like 16 s/km of lost fitness.
        var september = new LocalDate(2026, 9, 1);
        var october = new LocalDate(2026, 10, 1);
        var runs = new[]
        {
            new SteadyRun(september.PlusDays(3), 8000, 8 * 360, 150, Indoor: true),
            new SteadyRun(october.PlusDays(3), 8000, 8 * 360 * 1.045, 150, Indoor: true, GradePercent: 1),
        };

        var trend = AerobicAnalysis.MonthlyTrend(runs, 150);

        Assert.Equal(360, trend[0].MedianNormalizedSecPerKm, precision: 6);
        Assert.Equal(360, trend[1].MedianNormalizedSecPerKm, precision: 6);
        Assert.Equal(0, trend[0].GradedRuns);
        Assert.Equal(1, trend[1].GradedRuns);
    }

    [Fact]
    public void The_weekly_outlook_reads_the_grade_too()
    {
        var today = new LocalDate(2026, 11, 13);
        var runs = Enumerable.Range(0, 9)
            .Select(d => new SteadyRun(today.PlusDays(-d * 3), 8000, 8 * 360 * 1.045, 150, Indoor: true, GradePercent: 1));

        var outlook = AerobicOutlook.Build(runs, 150, today);

        Assert.All(outlook.Weeks, w => Assert.Equal(360, w.MedianSecPerKm, precision: 6));
    }

    [Fact]
    public async Task The_setting_round_trips_and_moves_the_aerobic_trend()
    {
        using var app = new FitnessApp();
        await app.SeedAsync(OneMonthOfTreadmillRuns);
        var client = app.Factory.CreateClient();

        var before = await LatestMonthAsync(client);
        Assert.Equal(376.2, before.GetProperty("medianSecPerKm").GetDouble(), precision: 6);
        Assert.Equal(0, before.GetProperty("gradedRuns").GetInt32());

        var saved = await client.PutAsJsonAsync("/api/fitness/settings", Update(1, "2026-06-01"));
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);

        var summary = await client.GetFromJsonAsync<JsonElement>("/api/fitness/summary");
        Assert.Equal(1, summary.GetProperty("settings").GetProperty("treadmillGradePercent").GetDouble());
        Assert.Equal("2026-06-01", summary.GetProperty("settings").GetProperty("treadmillGradeSince").GetString());

        var after = await LatestMonthAsync(client);
        Assert.Equal(360, after.GetProperty("medianSecPerKm").GetDouble(), precision: 6);
        Assert.Equal(4, after.GetProperty("gradedRuns").GetInt32());
    }

    [Theory]
    [InlineData(1.0, null)]
    [InlineData(null, "2026-06-01")]
    [InlineData(16.0, "2026-06-01")]
    [InlineData(-1.0, "2026-06-01")]
    [InlineData(1.0, "June 1")]
    public async Task A_setting_that_cannot_be_read_is_refused(double? percent, string? since)
    {
        using var app = new FitnessApp();
        await app.SeedAsync(OneMonthOfTreadmillRuns);
        var client = app.Factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/fitness/settings", Update(percent, since));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<JsonElement> LatestMonthAsync(HttpClient client)
    {
        var summary = await client.GetFromJsonAsync<JsonElement>("/api/fitness/summary");
        return summary.GetProperty("aerobicTrend").EnumerateArray().Last();
    }

    private static object Update(double? percent, string? since) => new
    {
        referenceHr = 150,
        ltSecondsPerKm = (double?)null,
        planMinutesPerWeek = 240,
        startVdot = 40,
        vdotMeasuredOn = (string?)null,
        availableHoursPerWeek = 7,
        homeAltitudeMeters = 0,
        treadmillGradePercent = percent,
        treadmillGradeSince = since,
    };

    /// <summary>Four steady treadmill runs in June at 6:16.2/km, which is 6:00 flat at 1%.</summary>
    private static Task OneMonthOfTreadmillRuns(FitnessDbContext database)
    {
        database.Settings.Add(new AthleteSettings { Id = 1, ReferenceHr = 150, StartVdot = 40 });

        for (var day = 1; day <= 4; day++)
        {
            database.Activities.Add(new Activity
            {
                Id = Guid.NewGuid(),
                Source = "garmin-fit",
                ExternalId = $"treadmill-{day}",
                StartedAt = Instant.FromUtc(2026, 6, day * 2, 6, 0),
                Sport = "run",
                Name = "Treadmill Running",
                DistanceMeters = 8000,
                DurationSeconds = 8 * 376.2,
                AverageHr = 150,
                Indoor = true,
            });
        }

        return Task.CompletedTask;
    }
}
