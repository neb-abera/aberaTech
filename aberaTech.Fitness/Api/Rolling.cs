namespace aberaTech.Fitness.Api;

/// <summary>Rolling endurance minutes, read from the daily series that ends today.</summary>
/// <param name="Last7">Today and the six days before it.</param>
/// <param name="Previous7">The seven days before those.</param>
/// <param name="Last28PerWeek">The last 28 days, divided by four.</param>
public sealed record Rolling(double Last7, double Previous7, double Last28PerWeek)
{
    public static Rolling Of(IReadOnlyList<DayVolumeDto> days)
    {
        double Sum(int skip, int take) => days.Reverse().Skip(skip).Take(take).Sum(d => d.Minutes);
        return new Rolling(Sum(0, 7), Sum(7, 7), Sum(0, 28) / 4);
    }
}
