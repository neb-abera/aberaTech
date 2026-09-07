using aberaTech.Fitness.Sync;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

public sealed class SyncScheduleTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 9, 7, 12, 0);

    [Fact]
    public void A_source_that_never_ran_is_due()
    {
        Assert.True(SyncSchedule.IsDue(SyncSchedule.HevySource, null, Now));
        Assert.True(SyncSchedule.IsDue(SyncSchedule.StravaSource, null, Now));
    }

    [Fact]
    public void Hevy_is_daily_and_strava_is_hourly()
    {
        Assert.False(SyncSchedule.IsDue(SyncSchedule.HevySource, Now - Duration.FromHours(23), Now));
        Assert.True(SyncSchedule.IsDue(SyncSchedule.HevySource, Now - Duration.FromHours(24), Now));

        Assert.False(SyncSchedule.IsDue(SyncSchedule.StravaSource, Now - Duration.FromMinutes(59), Now));
        Assert.True(SyncSchedule.IsDue(SyncSchedule.StravaSource, Now - Duration.FromMinutes(60), Now));
    }

    [Fact]
    public void An_unknown_source_is_refused_rather_than_guessed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SyncSchedule.IsDue("garmin", Now - Duration.FromDays(3), Now));
    }

    [Fact]
    public void A_listing_starts_two_days_before_the_last_activity_seen()
    {
        var last = Instant.FromUtc(2026, 9, 5, 6, 0);
        Assert.Equal(Instant.FromUtc(2026, 9, 3, 6, 0), SyncSchedule.ListFrom(last));
        Assert.Equal(Instant.FromUtc(2015, 1, 1, 0, 0), SyncSchedule.ListFrom(null));
    }
}
