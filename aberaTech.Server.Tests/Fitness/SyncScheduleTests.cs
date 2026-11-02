using aberaTech.Fitness.Data;
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
        Assert.True(SyncSchedule.IsDue(SyncSchedule.HevySource, null, null, Now));
        Assert.True(SyncSchedule.IsDue(SyncSchedule.IntervalsIcuSource, null, null, Now));
    }

    [Fact]
    public void Hevy_is_daily_and_intervals_icu_is_hourly()
    {
        Assert.False(SyncSchedule.IsDue(SyncSchedule.HevySource, Now - Duration.FromHours(23), Now - Duration.FromHours(23), Now));
        Assert.True(SyncSchedule.IsDue(SyncSchedule.HevySource, Now - Duration.FromHours(24), Now - Duration.FromHours(24), Now));

        Assert.False(SyncSchedule.IsDue(SyncSchedule.IntervalsIcuSource, Now - Duration.FromMinutes(59), Now - Duration.FromMinutes(59), Now));
        Assert.True(SyncSchedule.IsDue(SyncSchedule.IntervalsIcuSource, Now - Duration.FromMinutes(60), Now - Duration.FromMinutes(60), Now));
    }

    [Fact]
    public void A_failed_run_retries_after_fifteen_minutes_for_its_first_hour()
    {
        var success = Now - Duration.FromHours(24) - Duration.FromMinutes(20);
        var failed = Now - Duration.FromMinutes(16);

        Assert.True(SyncSchedule.IsDue(SyncSchedule.HevySource, failed, success, Now));
        Assert.False(SyncSchedule.IsDue(SyncSchedule.HevySource, Now - Duration.FromMinutes(14), success, Now));
    }

    [Fact]
    public void A_source_failing_for_hours_retries_hourly()
    {
        var success = Now - Duration.FromDays(3);

        Assert.False(SyncSchedule.IsDue(SyncSchedule.HevySource, Now - Duration.FromMinutes(30), success, Now));
        Assert.True(SyncSchedule.IsDue(SyncSchedule.HevySource, Now - Duration.FromMinutes(60), success, Now));
        Assert.False(SyncSchedule.IsDue(SyncSchedule.HevySource, Now - Duration.FromMinutes(30), null, Now));
    }

    [Fact]
    public void A_source_past_its_limit_raises_a_finding_with_the_last_outcome()
    {
        var quiet = new SyncState
        {
            Source = SyncSchedule.HevySource,
            LastRunAt = Now,
            LastSuccessAt = Now - Duration.FromHours(27),
            LastOutcome = "Hevy refused the API key; replace it."
        };
        var fresh = new SyncState { Source = SyncSchedule.IntervalsIcuSource, LastRunAt = Now, LastSuccessAt = Now - Duration.FromHours(1) };

        var finding = Assert.Single(SyncSchedule.StaleFindings([quiet, fresh], Now));

        Assert.False(finding.Positive);
        Assert.Contains("Hevy has not synced since 2026-09-06", finding.Headline);
        Assert.Contains("refused the API key", finding.Evidence);
    }

    [Fact]
    public void An_unknown_source_is_refused_rather_than_guessed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SyncSchedule.IsDue("garmin", Now - Duration.FromDays(3), Now - Duration.FromDays(3), Now));
    }

    [Fact]
    public void A_listing_starts_two_days_before_the_last_activity_seen()
    {
        var last = Instant.FromUtc(2026, 9, 5, 6, 0);
        Assert.Equal(Instant.FromUtc(2026, 9, 3, 6, 0), SyncSchedule.ListFrom(last));
        Assert.Equal(Instant.FromUtc(2015, 1, 1, 0, 0), SyncSchedule.ListFrom(null));
    }
}
