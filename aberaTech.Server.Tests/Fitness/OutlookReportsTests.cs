using aberaTech.Fitness.Api;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The week the forecast starts from. The slider used to be seeded with the
/// log's thin recent average, which made every gate look hopeless before
/// anyone touched it; the profile's own week comes first now.
/// </summary>
public sealed class OutlookReportsTests
{
    [Fact]
    public void A_named_week_wins()
    {
        var (hours, basis) = OutlookReports.DefaultWeek(named: 6, measured: 0.9, availableHoursPerWeek: 7, planMinutesPerWeek: 160);
        Assert.Equal(6, hours);
        Assert.Contains("slider", basis);
    }

    [Fact]
    public void Otherwise_the_profiles_available_hours_seed_the_slider_not_the_log()
    {
        var (hours, basis) = OutlookReports.DefaultWeek(named: null, measured: 0.9, availableHoursPerWeek: 7, planMinutesPerWeek: 160);
        Assert.Equal(7, hours);
        Assert.Contains("profile says you can train", basis);
        Assert.Contains("7.0 h", basis);
    }

    [Fact]
    public void Then_the_planned_minutes_then_the_log()
    {
        var (fromPlan, planBasis) = OutlookReports.DefaultWeek(null, 0.9, availableHoursPerWeek: 0, planMinutesPerWeek: 180);
        Assert.Equal(3, fromPlan);
        Assert.Contains("planned 180 minutes", planBasis);

        var (fromLog, logBasis) = OutlookReports.DefaultWeek(null, 0.9, availableHoursPerWeek: 0, planMinutesPerWeek: 0);
        Assert.Equal(0.9, fromLog);
        Assert.Contains("log", logBasis);
    }
}
