using aberaTech.Scheduling.Alerts;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Alerts;

/// <summary>
/// The rings of routine alarms as the phone works them out: the weekdays,
/// the one-off ring after the save, the key, a clock change in either
/// direction, an edit that changes later rings only, and the window.
/// </summary>
public sealed class RoutineRingsTests
{
    private static readonly DateTimeZone Amman = DateTimeZoneProviders.Tzdb["Asia/Amman"];
    private static readonly DateTimeZone NewYork = DateTimeZoneProviders.Tzdb["America/New_York"];
    private static readonly Duration ThreeHours = Duration.FromHours(3);
    private static readonly Guid Id = Guid.Parse("0D8C6F1E-3F6E-4A53-9D53-8F1B2A7C4D10");

    private static AlertRoutine Routine(
        int hour, int minute, int[] days, Instant updatedAt, bool enabled = true, string label = "Wake up") =>
        new(Id, label, hour, minute, days, enabled, 9, updatedAt);

    private static IReadOnlyList<RoutineRing> Plan(AlertRoutine routine, DateTimeZone zone, Instant from, Instant until) =>
        RoutineRings.Plan([routine], zone, from, until, ThreeHours);

    [Fact]
    public void A_routine_with_days_rings_on_each_of_those_weekdays_at_its_wall_clock_time()
    {
        // Monday 26 October 2026 to the Monday after, in Amman (UTC+3).
        var from = Instant.FromUtc(2026, 10, 25, 21, 0);
        var routine = Routine(6, 30, [1, 3, 5], Instant.FromUtc(2026, 10, 1, 0, 0));

        var rings = Plan(routine, Amman, from, from + Duration.FromDays(7));

        Assert.Equal(
            [Instant.FromUtc(2026, 10, 26, 3, 30), Instant.FromUtc(2026, 10, 28, 3, 30), Instant.FromUtc(2026, 10, 30, 3, 30)],
            rings.Select(ring => ring.AlertAt));
        Assert.All(rings, ring => Assert.Equal(ring.AlertAt + ThreeHours, ring.StartsAt));
        Assert.All(rings, ring => Assert.Equal("Wake up", ring.Label));
        Assert.All(rings, ring => Assert.Equal(Id, ring.RoutineId));
    }

    [Fact]
    public void The_key_is_the_lowercase_id_and_the_scheduled_local_date_and_time()
    {
        var from = Instant.FromUtc(2026, 10, 28, 0, 0);
        var routine = Routine(6, 30, [3], Instant.FromUtc(2026, 10, 1, 0, 0));

        var ring = Assert.Single(Plan(routine, Amman, from, from + Duration.FromDays(1)));

        Assert.Equal("routine:0d8c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10:2026-10-28T06:30", ring.Key);
        Assert.True(RoutineRings.IsKey(ring.Key));
        Assert.False(RoutineRings.IsKey("standup@google.com|20261028T130000Z"));
    }

    [Fact]
    public void A_routine_with_no_days_rings_once_at_the_next_hour_and_minute_after_it_was_saved()
    {
        var from = Instant.FromUtc(2026, 10, 27, 0, 0);
        var until = from + Duration.FromDays(5);

        // Saved at 07:00 in Amman: 06:30 has passed, so tomorrow.
        var late = Plan(Routine(6, 30, [], Instant.FromUtc(2026, 10, 28, 4, 0)), Amman, from, until);
        Assert.Equal([Instant.FromUtc(2026, 10, 29, 3, 30)], late.Select(ring => ring.AlertAt));
        Assert.Equal("routine:0d8c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10:2026-10-29T06:30", late.Single().Key);

        // Saved at 06:00: today.
        var early = Plan(Routine(6, 30, [], Instant.FromUtc(2026, 10, 28, 3, 0)), Amman, from, until);
        Assert.Equal([Instant.FromUtc(2026, 10, 28, 3, 30)], early.Select(ring => ring.AlertAt));

        // Saved at 06:30 exactly: the next one after it, tomorrow.
        var exact = Plan(Routine(6, 30, [], Instant.FromUtc(2026, 10, 28, 3, 30)), Amman, from, until);
        Assert.Equal([Instant.FromUtc(2026, 10, 29, 3, 30)], exact.Select(ring => ring.AlertAt));
    }

    [Fact]
    public void A_one_off_ring_that_has_passed_out_of_the_window_rings_no_more()
    {
        var routine = Routine(6, 30, [], Instant.FromUtc(2026, 10, 28, 3, 0));

        var later = Plan(routine, Amman, Instant.FromUtc(2026, 10, 28, 7, 0), Instant.FromUtc(2026, 11, 5, 0, 0));

        Assert.Empty(later);
    }

    [Fact]
    public void A_time_the_clock_skips_rings_at_the_first_time_that_exists_after_it()
    {
        // New York, Sunday 8 March 2026: 02:00 EST becomes 03:00 EDT.
        var routine = Routine(2, 30, [7], Instant.FromUtc(2026, 3, 1, 0, 0));

        var ring = Assert.Single(Plan(routine, NewYork, Instant.FromUtc(2026, 3, 8, 0, 0), Instant.FromUtc(2026, 3, 9, 0, 0)));

        // 03:00 EDT, which is 07:00 UTC. The key keeps the scheduled time.
        Assert.Equal(Instant.FromUtc(2026, 3, 8, 7, 0), ring.AlertAt);
        Assert.EndsWith(":2026-03-08T02:30", ring.Key);
    }

    [Fact]
    public void A_time_the_clock_passes_twice_rings_the_first_time()
    {
        // New York, Sunday 1 November 2026: 02:00 EDT becomes 01:00 EST, so 01:30 happens twice.
        var routine = Routine(1, 30, [7], Instant.FromUtc(2026, 10, 1, 0, 0));

        var ring = Assert.Single(Plan(routine, NewYork, Instant.FromUtc(2026, 11, 1, 0, 0), Instant.FromUtc(2026, 11, 2, 0, 0)));

        // 01:30 EDT, which is 05:30 UTC, not 01:30 EST at 06:30 UTC.
        Assert.Equal(Instant.FromUtc(2026, 11, 1, 5, 30), ring.AlertAt);
    }

    [Fact]
    public void A_disabled_routine_rings_nothing()
    {
        var routine = Routine(6, 30, [1, 2, 3, 4, 5, 6, 7], Instant.FromUtc(2026, 10, 1, 0, 0), enabled: false);

        Assert.Empty(Plan(routine, Amman, Instant.FromUtc(2026, 10, 26, 0, 0), Instant.FromUtc(2026, 11, 2, 0, 0)));
    }

    [Fact]
    public void An_edit_changes_later_rings_only_so_a_time_already_past_on_the_day_of_the_edit_waits_a_day()
    {
        // Edited at 06:40 in Amman to ring at 06:30 every day.
        var routine = Routine(6, 30, [1, 2, 3, 4, 5, 6, 7], Instant.FromUtc(2026, 10, 28, 3, 40));

        var rings = Plan(routine, Amman, Instant.FromUtc(2026, 10, 28, 0, 0), Instant.FromUtc(2026, 10, 30, 0, 0));

        Assert.Equal([Instant.FromUtc(2026, 10, 29, 3, 30)], rings.Select(ring => ring.AlertAt));
    }

    [Fact]
    public void The_window_holds_rings_from_its_start_to_its_end_inclusive()
    {
        var routine = Routine(6, 30, [1, 2, 3, 4, 5, 6, 7], Instant.FromUtc(2026, 10, 1, 0, 0));
        var ring = Instant.FromUtc(2026, 10, 28, 3, 30);

        Assert.Single(Plan(routine, Amman, ring, ring));
        Assert.Empty(Plan(routine, Amman, ring + Duration.FromSeconds(1), ring + Duration.FromHours(23)));
    }

    [Fact]
    public void A_ring_is_planned_as_an_alarm_titled_with_its_label()
    {
        var routine = Routine(6, 30, [3], Instant.FromUtc(2026, 10, 1, 0, 0), label: "Gym");
        var ring = Plan(routine, Amman, Instant.FromUtc(2026, 10, 28, 0, 0), Instant.FromUtc(2026, 10, 29, 0, 0)).Single();

        var planned = ring.ToPlanned();

        Assert.Equal(ring.Key, planned.Key);
        Assert.Equal("Gym", planned.Title);
        Assert.Equal(ring.AlertAt, planned.AlertAt);
        Assert.Equal(ring.StartsAt, planned.StartsAt);
        Assert.True(planned.Routine);
        var settings = AlertSettings.Defaults(new AlertsOptions());
        Assert.Equal(AlertTypes.Alarm, AlertTypes.Resolve(planned, null, settings).Type);
        Assert.Equal("Routine alarm, 6:30 AM +03, Wed 28 Oct", AlertText.Message(planned, Amman));
    }
}
