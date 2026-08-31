using aberaTech.Fitness.Domain;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The dose-response surface is calibrated by two independent conditions, and
/// these are them. If either breaks, the constants moved and the model no
/// longer says what its documentation claims it says.
/// </summary>
public sealed class DoseResponseTests
{
    [Fact]
    public void Optimal_split_of_a_normal_week_reproduces_the_eighty_twenty_distribution()
    {
        // Seiler's finding, not an assumption baked into the allocator: given
        // eight hours and told to maximise the ceiling, the model spends them
        // the way elite endurance athletes are observed to.
        var dose = DoseResponse.Allocate(8).Dose;

        Assert.Equal(8, dose.RunningHours, precision: 6);
        Assert.InRange(dose.EasyShare, 0.78, 0.82);
        Assert.InRange(dose.IntervalHours, 0.4, 0.8);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    public void Agrees_with_the_calibrated_linear_ceiling_over_the_range_it_was_fitted_on(double hours)
    {
        // The previous model was C = 38 + 1.6h, fitted to documented
        // aerobic-deficiency recoveries. The surface must not throw that away
        // where the evidence actually lives.
        var dose = DoseResponse.Allocate(hours).Dose;
        Assert.InRange(DoseResponse.Ceiling(dose), 38 + 1.6 * hours - 1.6, 38 + 1.6 * hours + 1.6);
    }

    [Fact]
    public void Parts_company_with_the_straight_line_where_the_line_becomes_nonsense()
    {
        // Forty hours a week on the old line implied VDOT 102 — faster than
        // any human has run. The surface saturates instead.
        var dose = DoseResponse.Allocate(40).Dose;
        Assert.True(DoseResponse.Ceiling(dose) < 70);
        // Forty hours is more recovery than any athlete has: the answer is the
        // longest week the budget affords, not the week that was asked for.
        Assert.Equal(DoseResponse.EliteStrain, dose.RunningHours, precision: 6);
        Assert.True(DoseResponse.Ceiling(dose) > DoseResponse.Ceiling(DoseResponse.Allocate(20).Dose));
    }

    [Fact]
    public void Every_zone_in_use_returns_the_same_ceiling_per_hour_at_the_optimum()
    {
        // The Lagrangian stationarity condition, which is what makes the split
        // explainable: at the optimum no hour can be moved to a better home.
        // It holds zone-for-zone while recovery is free; once the strain
        // budget binds, the zones equalise net of the price of recovery
        // instead, which the squeezed-week test covers.
        var allocation = DoseResponse.Allocate(9);
        Assert.Equal(0, allocation.StrainPrice);
        foreach (var zone in TrainingDose.RunningZones)
        {
            Assert.True(allocation.Dose[zone] > 0);
            Assert.Equal(allocation.HourPrice, DoseResponse.Marginal(allocation.Dose, zone), precision: 4);
        }
    }

    [Fact]
    public void The_shadow_price_of_an_hour_falls_as_the_week_lengthens()
    {
        Assert.True(DoseResponse.Allocate(4).HourPrice > DoseResponse.Allocate(8).HourPrice);
        Assert.True(DoseResponse.Allocate(8).HourPrice > DoseResponse.Allocate(16).HourPrice);
    }

    [Fact]
    public void A_recovery_budget_pushes_hours_out_of_the_hard_zones()
    {
        var free = DoseResponse.Allocate(10);
        var squeezed = DoseResponse.Allocate(10, new DoseLimits(MaxStrain: 11));

        Assert.True(squeezed.Dose.Strain <= 11.001);
        Assert.True(squeezed.Dose.EasyShare > free.Dose.EasyShare);
        Assert.True(squeezed.StrainPrice > 0);
    }

    [Fact]
    public void Responsiveness_scales_the_whole_surface()
    {
        var dose = DoseResponse.Allocate(8).Dose;
        Assert.Equal(
            2 * DoseResponse.Gain(dose),
            DoseResponse.Gain(dose, responsiveness: 2),
            precision: 6);
    }

    [Fact]
    public void Inverting_the_surface_roundtrips()
    {
        var hours = DoseResponse.HoursForCeiling(52);
        Assert.NotNull(hours);
        Assert.Equal(52, DoseResponse.Ceiling(DoseResponse.Allocate(hours!.Value).Dose), precision: 4);
    }

    [Fact]
    public void A_ceiling_no_sustainable_week_supports_is_refused()
    {
        Assert.Null(DoseResponse.HoursForCeiling(80));
    }

    [Fact]
    public void Strain_prices_the_hard_zones_the_way_session_load_does()
    {
        var dose = new TrainingDose(EasyHours: 4, ThresholdHours: 1, IntervalHours: 0.5, StrengthHours: 1);
        Assert.Equal(4 + 2.5 + 2.25 + 1.5, dose.Strain, precision: 6);
    }

    [Fact]
    public void The_trace_reconstructs_the_ceiling_it_reports()
    {
        var dose = DoseResponse.Allocate(7).Dose;
        var steps = DoseResponse.Explain(dose);

        Assert.Contains(steps, s => s.Label == "Ceiling this week supports");
        Assert.Contains(steps, s => s.Label == "Recovery cost");
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Expression)));
    }
}
