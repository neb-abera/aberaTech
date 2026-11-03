using aberaTech.Fitness.Domain;
using aberaTech.Fitness.OneDrive;
using aberaTech.Fitness.Workbook;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The goals workbook read as the athlete wrote it: every essential row, in
/// order, under its group, and nothing below the non-essential line.
/// </summary>
public sealed class GoalWorkbookTests
{
    private static GoalSheet Parse(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return GoalWorkbook.Parse(stream);
    }

    [Fact]
    public void Reads_the_nineteen_essential_rows_in_order()
    {
        var sheet = Parse(WorkbookFixture.Build());

        Assert.Equal("goals", sheet.SheetName);
        Assert.Equal(2027, sheet.GoalYear);
        Assert.Equal(19, sheet.Rows.Count);
        Assert.Equal(Enumerable.Range(3, 19), sheet.Rows.Select(r => r.Row));
        Assert.Equal("5 mile run", sheet.Rows[0].Label);
        Assert.Equal("Back Squat", sheet.Rows[^1].Label);
        Assert.DoesNotContain(sheet.Rows, r => r.Label is "Clean" or "500y Swim");
    }

    [Fact]
    public void Groups_come_from_column_A_and_a_multiple_does_not_open_one()
    {
        var sheet = Parse(WorkbookFixture.Build());
        var groups = sheet.Rows.ToDictionary(r => r.Label, r => r.Group);

        Assert.Equal("Run calculator", groups["Aerobic threshold pace (flat, per mile)"]);
        Assert.Equal("AFT 428 on 04APR26", groups["2 mile run"]);
        Assert.Equal("FBI", groups["Push-ups"]);
        Assert.Equal("AFT 428 on 04APR26", groups["HR pushups"]);
        Assert.Equal("Body comp", groups["Bench Press"]);

        var bench = sheet.Rows.Single(r => r.Label == "Bench Press");
        Assert.Equal(1.5, bench.Multiple);
    }

    [Fact]
    public void Times_are_read_as_day_fractions_and_text_stays_text()
    {
        var sheet = Parse(WorkbookFixture.Build());
        var sprint = sheet.Rows.Single(r => r.Label == "300m sprint");

        Assert.Equal("untested", sprint.Baseline.Text);
        Assert.Null(sprint.Baseline.Number);
        Assert.Equal(40.9, GoalCatalog.Read(sprint.Goal, GoalUnit.Seconds));

        var tenMile = sheet.Rows.Single(r => r.Label == "10 mile run");
        Assert.Equal(4254, GoalCatalog.Read(tenMile.Goal, GoalUnit.Seconds));
        Assert.Equal("inferred from the 5-mile", tenMile.Evidence);
    }

    [Fact]
    public void A_new_row_before_the_end_line_is_an_essential_row()
    {
        var lines = WorkbookFixture.Standard()
            .Select(l => l.Row >= 22 ? l with { Row = l.Row + 1 } : l)
            .Append(new WorkbookFixture.Line(22, WorkbookFixture.Cells(("B", "Rope climb"), ("F", 3.0))))
            .ToArray();

        var sheet = Parse(WorkbookFixture.Build(lines));

        Assert.Equal(20, sheet.Rows.Count);
        Assert.Equal("Rope climb", sheet.Rows[^1].Label);
        Assert.Null(GoalCatalog.Find("Rope climb"));
    }

    [Fact]
    public void No_header_row_is_refused_by_name()
    {
        var error = Assert.Throws<GoalWorkbookException>(() => Parse(WorkbookFixture.Build(WorkbookFixture.Without(2))));
        Assert.Contains("Exercise", error.Message);
    }

    [Fact]
    public void No_end_line_is_refused_rather_than_reading_the_whole_sheet()
    {
        var error = Assert.Throws<GoalWorkbookException>(() => Parse(WorkbookFixture.Build(WorkbookFixture.Without(22))));
        Assert.Contains("Non-essential", error.Message);
    }

    [Fact]
    public void A_file_that_is_not_a_workbook_is_refused()
    {
        Assert.Throws<GoalWorkbookException>(() => Parse("not a zip at all"u8.ToArray()));
    }

    [Fact]
    public void An_oversized_file_is_refused_before_it_is_opened()
    {
        var bytes = new byte[GoalWorkbook.MaxBytes + 1];
        var error = Assert.Throws<GoalWorkbookException>(() => Parse(bytes));
        Assert.Contains("larger", error.Message);
    }

    /// <summary>
    /// The parser reads a file from outside the server. Whatever bytes arrive,
    /// it either reads rows or refuses with a reason; it never throws anything
    /// the endpoint would turn into a 500.
    /// </summary>
    [Fact]
    public void Corrupted_bytes_are_refused_never_thrown()
    {
        var good = WorkbookFixture.Build();
        var random = new Random(20261102);

        for (var trial = 0; trial < 300; trial++)
        {
            var bytes = (byte[])good.Clone();
            var flips = random.Next(1, 40);
            for (var i = 0; i < flips; i++) bytes[random.Next(bytes.Length)] = (byte)random.Next(256);
            if (trial % 10 == 0) Array.Resize(ref bytes, random.Next(1, bytes.Length));

            try
            {
                Parse(bytes);
            }
            catch (GoalWorkbookException)
            {
            }
        }
    }
}

/// <summary>Every label in the athlete's workbook, mapped to what the log measures.</summary>
public sealed class GoalCatalogTests
{
    [Theory]
    [InlineData("5 mile run", SelectionReadiness.Metrics.RunFiveMile, GoalUnit.Seconds, true)]
    [InlineData("10 mile run", SelectionReadiness.Metrics.RunTenMile, GoalUnit.Seconds, true)]
    [InlineData("12mi ruck @ 35lbs", SelectionReadiness.Metrics.RuckTwelveMileAt35, GoalUnit.Seconds, true)]
    [InlineData("Aerobic threshold pace (flat, per mile)", SelectionReadiness.Metrics.AerobicThresholdPace, GoalUnit.SecondsPerMile, true)]
    [InlineData("Max 3 rep deadlift", SelectionReadiness.Metrics.DeadliftTripleLb, GoalUnit.Pounds, false)]
    [InlineData("HR pushups", SelectionReadiness.Metrics.HandReleasePushUps, GoalUnit.Reps, false)]
    [InlineData("sprint drag carry", SelectionReadiness.Metrics.SprintDragCarry, GoalUnit.Seconds, true)]
    [InlineData("plank", SelectionReadiness.Metrics.Plank, GoalUnit.Seconds, false)]
    [InlineData("2 mile run", SelectionReadiness.Metrics.RunTwoMile, GoalUnit.Seconds, true)]
    [InlineData("Pull-ups", SelectionReadiness.Metrics.PullUps, GoalUnit.Reps, false)]
    [InlineData("300m sprint", SelectionReadiness.Metrics.Sprint300m, GoalUnit.Seconds, true)]
    [InlineData("Push-ups", SelectionReadiness.Metrics.PushUps, GoalUnit.Reps, false)]
    [InlineData("1.5m run", SelectionReadiness.Metrics.RunOneAndHalfMile, GoalUnit.Seconds, true)]
    [InlineData("Bodyweight", SelectionReadiness.Metrics.BodyweightLb, GoalUnit.Pounds, true)]
    [InlineData("Body fat %", SelectionReadiness.Metrics.BodyFat, GoalUnit.Percent, true)]
    [InlineData("Bench Press", SelectionReadiness.Metrics.BenchPressLb, GoalUnit.Pounds, false)]
    [InlineData("Front Squat", SelectionReadiness.Metrics.FrontSquatLb, GoalUnit.Pounds, false)]
    [InlineData("Farmer carry 1.5x bw m", SelectionReadiness.Metrics.FarmersCarryMeters, GoalUnit.Meters, false)]
    [InlineData("Back Squat", SelectionReadiness.Metrics.BackSquatLb, GoalUnit.Pounds, false)]
    public void Every_workbook_label_maps_to_its_measurement(string label, string metric, GoalUnit unit, bool lowerIsBetter)
    {
        var definition = GoalCatalog.Find(label);

        Assert.NotNull(definition);
        Assert.Equal(metric, definition.Metric);
        Assert.Equal(unit, definition.Unit);
        Assert.Equal(lowerIsBetter, definition.LowerIsBetter);
    }

    [Fact]
    public void The_two_push_ups_are_two_events_of_two_tests()
    {
        var aft = GoalCatalog.Find("HR pushups");
        var fbi = GoalCatalog.Find("Push-ups");

        Assert.Equal(AftEvent.HandReleasePushUps, aft!.Aft);
        Assert.Null(aft.FbiEvent);
        Assert.Equal(FbiEvent.PushUps, fbi!.FbiEvent);
        Assert.Null(fbi.Aft);
    }

    [Theory]
    [InlineData("AFT 428 on 04APR26", "Army Fitness Test")]
    [InlineData("FBI", "FBI Physical Fitness Test")]
    [InlineData("Body comp", "Body composition")]
    [InlineData("Run calculator", "Run calculator")]
    public void Section_names_follow_the_sheet(string group, string name)
    {
        Assert.Equal(name, GoalCatalog.SectionName(group));
    }

    [Fact]
    public void An_unknown_label_is_not_guessed()
    {
        Assert.Null(GoalCatalog.Find("Concentration Curl"));
    }
}

/// <summary>The FBI tables against rows read off fbijobs.gov, Events 1 to 4, men and women.</summary>
public sealed class FbiTests
{
    [Theory]
    [InlineData(FbiEvent.PullUps, 20, 10)]
    [InlineData(FbiEvent.PullUps, 15, 7)]
    [InlineData(FbiEvent.PullUps, 6, 3)]
    [InlineData(FbiEvent.PullUps, 1, 0)]
    [InlineData(FbiEvent.Sprint300m, 40.9, 10)]
    [InlineData(FbiEvent.Sprint300m, 41.0, 9)]
    [InlineData(FbiEvent.Sprint300m, 46.0, 5)]
    [InlineData(FbiEvent.Sprint300m, 52.4, 1)]
    [InlineData(FbiEvent.Sprint300m, 55.0, 0)]
    [InlineData(FbiEvent.Sprint300m, 55.1, -2)]
    [InlineData(FbiEvent.PushUps, 80, 10)]
    [InlineData(FbiEvent.PushUps, 71, 10)]
    [InlineData(FbiEvent.PushUps, 70, 9)]
    [InlineData(FbiEvent.PushUps, 30, 1)]
    [InlineData(FbiEvent.PushUps, 20, 0)]
    [InlineData(FbiEvent.PushUps, 19, -2)]
    [InlineData(FbiEvent.Run1_5Mile, 539, 10)]
    [InlineData(FbiEvent.Run1_5Mile, 540, 9)]
    [InlineData(FbiEvent.Run1_5Mile, 744, 1)]
    [InlineData(FbiEvent.Run1_5Mile, 745, 0)]
    [InlineData(FbiEvent.Run1_5Mile, 810, -2)]
    public void Men(FbiEvent fbiEvent, double raw, int points)
    {
        Assert.Equal(points, Fbi.Points(fbiEvent, raw, female: false));
    }

    [Theory]
    [InlineData(FbiEvent.PullUps, 10, 10)]
    [InlineData(FbiEvent.Sprint300m, 49.9, 10)]
    [InlineData(FbiEvent.Sprint300m, 64.9, 1)]
    [InlineData(FbiEvent.PushUps, 45, 10)]
    [InlineData(FbiEvent.PushUps, 14, 1)]
    [InlineData(FbiEvent.Run1_5Mile, 634, 10)]
    [InlineData(FbiEvent.Run1_5Mile, 839, 1)]
    public void Women(FbiEvent fbiEvent, double raw, int points)
    {
        Assert.Equal(points, Fbi.Points(fbiEvent, raw, female: true));
    }
}

/// <summary>The PKCE half of the OneDrive grant, against RFC 7636's own example.</summary>
public sealed class OneDriveClientTests
{
    [Fact]
    public void Challenge_matches_the_rfc_example()
    {
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", OneDriveClient.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
    }

    [Fact]
    public void The_authorize_link_asks_for_read_only_files_at_the_consumers_authority()
    {
        var url = OneDriveClient.AuthorizeRedirect("client", "https://abera.tech/api/fitness/onedrive/callback", "state", "challenge");

        Assert.StartsWith("https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize?", url);
        Assert.Contains("scope=Files.Read%20offline_access", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.DoesNotContain("ReadWrite", url);
    }

    [Fact]
    public void A_path_is_escaped_segment_by_segment()
    {
        Assert.Equal("Documents/physical%20fitness/physical%20fitness%20goals.xlsx",
            OneDriveClient.EscapePath("/Documents/physical fitness/physical fitness goals.xlsx"));
    }
}
