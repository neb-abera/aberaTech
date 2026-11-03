using aberaTech.Fitness.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

public sealed class ReadinessTests
{
    private static Dictionary<string, Measurement> Measurements(params (string Metric, double Value)[] values) =>
        values.ToDictionary(
            v => v.Metric,
            v => new Measurement(v.Metric, v.Value, Basis.Measured, "test", new LocalDate(2026, 9, 1)));

    [Theory]
    [InlineData(SelectionReadiness.Units.Seconds, 9900, "2:45:00")]
    [InlineData(SelectionReadiness.Units.Reps, 12, "12 reps")]
    [InlineData(SelectionReadiness.Units.Bodyweights, 1.5, "1.50× bodyweight")]
    [InlineData(SelectionReadiness.Units.Pounds, 340, "340 lb (154 kg)")]
    [InlineData(SelectionReadiness.Units.Percent, 14.5, "14.5%")]
    [InlineData(SelectionReadiness.Units.SecondsPerMile, 480, "8:00/mile")]
    [InlineData(SelectionReadiness.Units.Meters, 100, "100 m")]
    public void Values_read_in_their_own_units(string unit, double value, string expected)
    {
        Assert.Equal(expected, SelectionReadiness.Describe(unit, value));
    }
}
