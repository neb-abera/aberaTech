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

    // ------------------------------------------------------------------ steps

    private static readonly Guid Meds = Guid.Parse("5A1E0000-0000-4000-8000-000000000001");
    private static readonly Guid Floss = Guid.Parse("5A1E0000-0000-4000-8000-000000000002");

    private static AlertRoutine WithSteps(AlertRoutine routine, params (Guid Id, string Label, int Offset)[] steps) =>
        routine with { Steps = [.. steps.Select(step => new AlertRoutineStep(step.Id, step.Label, step.Offset, routine.UpdatedAt))] };

    [Fact]
    public void A_step_rings_its_cumulative_offset_after_its_routine_under_a_key_with_the_routines_time()
    {
        var routine = WithSteps(Routine(6, 30, [3], Instant.FromUtc(2026, 10, 1, 0, 0)), (Meds, "Meds", 5), (Floss, "Floss", 10));

        var rings = Plan(routine, Amman, Instant.FromUtc(2026, 10, 28, 0, 0), Instant.FromUtc(2026, 10, 29, 0, 0));

        Assert.Equal(
            [
                ("routine:0d8c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10:2026-10-28T06:30", "Wake up", Instant.FromUtc(2026, 10, 28, 3, 30), (Guid?)null),
                ("routine:0d8c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10:2026-10-28T06:30:step:5a1e0000-0000-4000-8000-000000000001", "Meds",
                    Instant.FromUtc(2026, 10, 28, 3, 35), Meds),
                ("routine:0d8c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10:2026-10-28T06:30:step:5a1e0000-0000-4000-8000-000000000002", "Floss",
                    Instant.FromUtc(2026, 10, 28, 3, 45), Floss),
            ],
            rings.Select(ring => (ring.Key, ring.Label, ring.AlertAt, ring.StepId)));
        Assert.All(rings, ring => Assert.Equal(ring.AlertAt + ThreeHours, ring.StartsAt));
        Assert.All(rings, ring => Assert.True(RoutineRings.IsKey(ring.Key)));
    }

    [Fact]
    public void A_step_key_reads_back_as_its_routine_cycle_and_step()
    {
        var key = RoutineRings.StepKey(Id, "2026-10-28T06:30", Meds);

        Assert.Equal((Id, "2026-10-28T06:30", (Guid?)Meds), RoutineRings.ParseKey(key));
        Assert.Equal((Id, "2026-10-28T06:30", (Guid?)null), RoutineRings.ParseKey($"routine:{Id:D}:2026-10-28T06:30"));
        Assert.Null(RoutineRings.ParseKey($"routine:{Id:D}:2026-10-28T06:30:step:nope"));
        Assert.Null(RoutineRings.ParseKey($"routine:{Id:D}:yesterday"));
        Assert.Null(RoutineRings.ParseKey("standup@google.com|20261028T130000Z"));
    }

    [Fact]
    public void A_step_of_a_ring_before_the_window_rings_inside_it()
    {
        // 23:50 on Tuesday, and its step 20 minutes later, on Wednesday.
        var routine = WithSteps(Routine(23, 50, [2], Instant.FromUtc(2026, 10, 1, 0, 0)), (Meds, "Meds", 20));
        var wednesday = Instant.FromUtc(2026, 10, 27, 21, 0);

        var ring = Assert.Single(Plan(routine, Amman, wednesday, wednesday + Duration.FromHours(1)));

        Assert.Equal(Meds, ring.StepId);
        Assert.Equal(Instant.FromUtc(2026, 10, 27, 21, 10), ring.AlertAt);
        Assert.EndsWith(":2026-10-27T23:50:step:5a1e0000-0000-4000-8000-000000000001", ring.Key);
    }

    [Fact]
    public void A_step_whose_ring_falls_in_an_hour_the_clock_skips_rings_by_elapsed_time()
    {
        // New York, Sunday 8 March 2026: 01:50 EST, and a step 15 minutes
        // later. 02:05 does not exist that night: it rings at 03:05 EDT.
        var routine = WithSteps(Routine(1, 50, [7], Instant.FromUtc(2026, 3, 1, 0, 0)), (Meds, "Meds", 15));

        var rings = Plan(routine, NewYork, Instant.FromUtc(2026, 3, 8, 0, 0), Instant.FromUtc(2026, 3, 9, 0, 0));

        Assert.Equal([Instant.FromUtc(2026, 3, 8, 6, 50), Instant.FromUtc(2026, 3, 8, 7, 5)], rings.Select(ring => ring.AlertAt));
        Assert.Equal(new LocalDateTime(2026, 3, 8, 3, 5), rings[1].AlertAt.InZone(NewYork).LocalDateTime);
        Assert.EndsWith(":2026-03-08T01:50:step:5a1e0000-0000-4000-8000-000000000001", rings[1].Key);
    }

    [Fact]
    public void A_step_of_a_routine_in_the_skipped_hour_follows_the_ring_the_clock_moved()
    {
        // 02:30 does not exist on 8 March 2026 in New York: the routine rings
        // at 03:00 EDT and its step 10 minutes after that.
        var routine = WithSteps(Routine(2, 30, [7], Instant.FromUtc(2026, 3, 1, 0, 0)), (Meds, "Meds", 10));

        var rings = Plan(routine, NewYork, Instant.FromUtc(2026, 3, 8, 0, 0), Instant.FromUtc(2026, 3, 9, 0, 0));

        Assert.Equal([Instant.FromUtc(2026, 3, 8, 7, 0), Instant.FromUtc(2026, 3, 8, 7, 10)], rings.Select(ring => ring.AlertAt));
        Assert.EndsWith(":2026-03-08T02:30:step:5a1e0000-0000-4000-8000-000000000001", rings[1].Key);
    }

    [Fact]
    public void A_step_across_the_repeated_hour_counts_elapsed_minutes()
    {
        // New York, Sunday 1 November 2026: 01:30 EDT, then 45 minutes later
        // is 01:15 EST, not 02:15.
        var routine = WithSteps(Routine(1, 30, [7], Instant.FromUtc(2026, 10, 1, 0, 0)), (Meds, "Meds", 45));

        var rings = Plan(routine, NewYork, Instant.FromUtc(2026, 11, 1, 0, 0), Instant.FromUtc(2026, 11, 2, 0, 0));

        Assert.Equal(Instant.FromUtc(2026, 11, 1, 6, 15), rings[1].AlertAt);
        Assert.Equal(new LocalDateTime(2026, 11, 1, 1, 15), rings[1].AlertAt.InZone(NewYork).LocalDateTime);
    }

    [Fact]
    public void A_completed_step_is_not_planned_for_its_cycle_and_is_for_the_next()
    {
        var routine = WithSteps(Routine(6, 30, [1, 2, 3, 4, 5, 6, 7], Instant.FromUtc(2026, 10, 1, 0, 0)), (Meds, "Meds", 5));
        var done = new HashSet<string>(StringComparer.Ordinal) { RoutineRings.StepKey(Id, "2026-10-28T06:30", Meds) };

        var rings = RoutineRings.Plan([routine], Amman, Instant.FromUtc(2026, 10, 28, 0, 0), Instant.FromUtc(2026, 10, 29, 23, 0), ThreeHours, done);

        Assert.Equal(
            [":2026-10-28T06:30", ":2026-10-29T06:30", ":2026-10-29T06:30:step:5a1e0000-0000-4000-8000-000000000001"],
            rings.Select(ring => ring.Key[(ring.Key.IndexOf(':', RoutineRings.KeyPrefix.Length))..]));
    }

    [Fact]
    public void A_step_ring_at_or_before_the_last_save_does_not_ring()
    {
        // Saved at 06:33 in Amman: the 06:30 ring has passed, its step at 06:35 has not.
        var routine = WithSteps(Routine(6, 30, [3], Instant.FromUtc(2026, 10, 28, 3, 33)), (Meds, "Meds", 5));

        var rings = Plan(routine, Amman, Instant.FromUtc(2026, 10, 28, 0, 0), Instant.FromUtc(2026, 10, 28, 12, 0));

        Assert.Equal([Meds], rings.Select(ring => ring.StepId));
    }

    [Fact]
    public void The_steps_of_a_one_time_routine_follow_its_single_ring()
    {
        var routine = WithSteps(Routine(6, 30, [], Instant.FromUtc(2026, 10, 28, 4, 0)), (Meds, "Meds", 5));

        var rings = Plan(routine, Amman, Instant.FromUtc(2026, 10, 27, 0, 0), Instant.FromUtc(2026, 11, 2, 0, 0));

        Assert.Equal([Instant.FromUtc(2026, 10, 29, 3, 30), Instant.FromUtc(2026, 10, 29, 3, 35)], rings.Select(ring => ring.AlertAt));
    }

    [Fact]
    public void A_step_ring_is_planned_as_an_alarm_titled_with_the_steps_label()
    {
        var routine = WithSteps(Routine(6, 30, [3], Instant.FromUtc(2026, 10, 1, 0, 0)), (Meds, "Take medication", 5));
        var ring = Plan(routine, Amman, Instant.FromUtc(2026, 10, 28, 0, 0), Instant.FromUtc(2026, 10, 29, 0, 0))[1];

        var planned = ring.ToPlanned();

        Assert.Equal("Take medication", planned.Title);
        Assert.True(planned.Routine);
        Assert.Equal(ring.Key, planned.Key);
    }
}
