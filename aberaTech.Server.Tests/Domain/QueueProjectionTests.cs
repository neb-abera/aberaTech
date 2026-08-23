using aberaTech.Scheduling.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Domain;

/// <summary>
/// The behaviour that makes a queue a queue: when somebody leaves or finishes
/// early, everybody behind them moves up by exactly that much.
/// </summary>
public class QueueProjectionTests
{
    private static readonly Instant Now = Instant.FromUtc(2027, 6, 1, 14, 0);

    private static QueueEntry Waiting(int position, int minutes) =>
        new(Guid.NewGuid(), position, Duration.FromMinutes(minutes), QueueEntryState.Waiting);

    [Fact]
    public void An_empty_queue_projects_nothing()
    {
        Assert.Empty(QueueProjection.Project([], Now));
    }

    [Fact]
    public void The_front_of_an_idle_queue_is_seen_now()
    {
        var entries = new[] { Waiting(1, 20), Waiting(2, 20) };

        var projection = QueueProjection.Project(entries, Now);

        Assert.Equal(Now, projection[0].ProjectedStart);
        Assert.Equal(Now + Duration.FromMinutes(20), projection[1].ProjectedStart);
    }

    [Fact]
    public void Each_entry_waits_for_everyone_ahead_of_it()
    {
        var entries = new[] { Waiting(1, 15), Waiting(2, 30), Waiting(3, 10) };

        var projection = QueueProjection.Project(entries, Now);

        Assert.Equal(Now, projection[0].ProjectedStart);
        Assert.Equal(Now + Duration.FromMinutes(15), projection[1].ProjectedStart);
        Assert.Equal(Now + Duration.FromMinutes(45), projection[2].ProjectedStart);
    }

    [Fact]
    public void Cancelling_someone_moves_everyone_behind_them_up_by_exactly_their_time()
    {
        var first = Waiting(1, 15);
        var second = Waiting(2, 30);
        var third = Waiting(3, 10);

        var before = QueueProjection.Project([first, second, third], Now);
        var after = QueueProjection.Project(
            [first, second with { State = QueueEntryState.Cancelled }, third],
            Now);

        var thirdBefore = before.Single(entry => entry.Id == third.Id).ProjectedStart;
        var thirdAfter = after.Single(entry => entry.Id == third.Id).ProjectedStart;

        Assert.Equal(Duration.FromMinutes(30), thirdBefore - thirdAfter);
        Assert.DoesNotContain(after, entry => entry.Id == second.Id);
    }

    [Fact]
    public void A_no_show_moves_the_queue_up_just_like_a_cancellation()
    {
        var first = Waiting(1, 20);
        var second = Waiting(2, 20);

        var after = QueueProjection.Project([first with { State = QueueEntryState.NoShow }, second], Now);

        Assert.Equal(Now, after.Single().ProjectedStart);
    }

    [Fact]
    public void An_appointment_in_progress_holds_the_queue_until_its_expected_end()
    {
        var serving = new QueueEntry(
            Guid.NewGuid(),
            1,
            Duration.FromMinutes(30),
            QueueEntryState.Serving,
            StartedAt: Now - Duration.FromMinutes(10));
        var next = Waiting(2, 15);

        var projection = QueueProjection.Project([serving, next], Now);

        // Ten minutes in on a thirty minute appointment: twenty to go.
        Assert.Equal(Now + Duration.FromMinutes(20), Assert.Single(projection).ProjectedStart);
    }

    [Fact]
    public void Finishing_early_pulls_the_next_person_forward()
    {
        var serving = new QueueEntry(
            Guid.NewGuid(),
            1,
            Duration.FromMinutes(30),
            QueueEntryState.Serving,
            StartedAt: Now - Duration.FromMinutes(10));
        var next = Waiting(2, 15);

        var stillGoing = QueueProjection.Project([serving, next], Now);
        var finishedEarly = QueueProjection.Project([serving with { State = QueueEntryState.Done }, next], Now);

        Assert.Equal(Now + Duration.FromMinutes(20), stillGoing.Single().ProjectedStart);
        Assert.Equal(Now, finishedEarly.Single().ProjectedStart);
    }

    [Fact]
    public void An_overrunning_appointment_does_not_project_the_queue_into_the_past()
    {
        // Forty minutes into a thirty minute appointment. The estimate is spent;
        // the honest answer for the next person is "as soon as this ends", not a
        // time ten minutes ago.
        var serving = new QueueEntry(
            Guid.NewGuid(),
            1,
            Duration.FromMinutes(30),
            QueueEntryState.Serving,
            StartedAt: Now - Duration.FromMinutes(40));
        var next = Waiting(2, 15);

        var projection = QueueProjection.Project([serving, next], Now);

        Assert.Equal(Now, projection.Single().ProjectedStart);
    }

    [Fact]
    public void An_overrunning_appointment_does_not_push_the_estimate_further_out_on_every_recalculation()
    {
        // The bug this guards: anchoring on "now" instead of on when the
        // appointment actually started makes the queue recede as you watch it,
        // so somebody refreshing the page is told a later time every time.
        var serving = new QueueEntry(
            Guid.NewGuid(),
            1,
            Duration.FromMinutes(30),
            QueueEntryState.Serving,
            StartedAt: Now - Duration.FromMinutes(10));
        var next = Waiting(2, 15);

        var first = QueueProjection.Project([serving, next], Now);
        var fiveMinutesLater = QueueProjection.Project([serving, next], Now + Duration.FromMinutes(5));

        Assert.Equal(first.Single().ProjectedStart, fiveMinutesLater.Single().ProjectedStart);
    }

    [Fact]
    public void Position_not_insertion_order_decides_the_line()
    {
        var third = Waiting(3, 10);
        var first = Waiting(1, 10);
        var second = Waiting(2, 10);

        var projection = QueueProjection.Project([third, first, second], Now);

        Assert.Equal([1, 2, 3], projection.Select(entry => entry.Position));
    }

    [Fact]
    public void Finished_entries_are_not_projected()
    {
        var done = Waiting(1, 20) with { State = QueueEntryState.Done };
        var waiting = Waiting(2, 20);

        var projection = QueueProjection.Project([done, waiting], Now);

        Assert.Equal(waiting.Id, Assert.Single(projection).Id);
    }

    [Fact]
    public void Wait_from_now_is_never_negative()
    {
        var projection = QueueProjection.Project([Waiting(1, 20)], Now);

        Assert.Equal(Duration.Zero, projection.Single().WaitFrom(Now + Duration.FromHours(1)));
    }

    // ---------------------------------------------------------------- bounds

    private static Interval Busy(int fromMinutes, int toMinutes) =>
        new(Now + Duration.FromMinutes(fromMinutes), Now + Duration.FromMinutes(toMinutes));

    [Fact]
    public void The_line_does_not_run_through_a_meeting_already_on_the_calendar()
    {
        // The defect this fixes: the queue would happily project somebody into
        // an existing commitment and text them to arrive during it.
        var projection = QueueProjection.Project(
            [Waiting(1, 20)],
            Now,
            [Busy(0, 30)]);

        Assert.Equal(Now + Duration.FromMinutes(30), projection.Single().ProjectedStart);
    }

    [Fact]
    public void An_appointment_that_merely_overlaps_a_meeting_is_pushed_past_it()
    {
        // Starting at +25 is free, but a twenty minute conversation would run
        // into a meeting beginning at +30. Fitting the whole appointment, not
        // just its first instant, is the point.
        var projection = QueueProjection.Project(
            [Waiting(1, 20)],
            Now + Duration.FromMinutes(25),
            [Busy(30, 60)]);

        Assert.Equal(Now + Duration.FromMinutes(60), projection.Single().ProjectedStart);
    }

    [Fact]
    public void Clearing_one_meeting_onto_another_keeps_moving()
    {
        // Back to back commitments: escaping the first lands inside the second,
        // so a single pass would leave the answer wrong.
        var projection = QueueProjection.Project(
            [Waiting(1, 20)],
            Now,
            [Busy(0, 30), Busy(30, 90)]);

        Assert.Equal(Now + Duration.FromMinutes(90), projection.Single().ProjectedStart);
    }

    [Fact]
    public void Everyone_behind_moves_with_the_person_pushed_past_a_meeting()
    {
        var projection = QueueProjection.Project(
            [Waiting(1, 20), Waiting(2, 20)],
            Now,
            [Busy(0, 30)]);

        Assert.Equal(Now + Duration.FromMinutes(30), projection[0].ProjectedStart);
        Assert.Equal(Now + Duration.FromMinutes(50), projection[1].ProjectedStart);
    }

    [Fact]
    public void Free_time_between_meetings_is_used_rather_than_skipped()
    {
        // A gap big enough for the appointment should be filled, not stepped
        // over: leaving it empty would push the whole queue later for nothing.
        var projection = QueueProjection.Project(
            [Waiting(1, 20)],
            Now,
            [Busy(-60, 0), Busy(30, 90)]);

        Assert.Equal(Now, projection.Single().ProjectedStart);
    }

    [Fact]
    public void Overlapping_busy_blocks_do_not_change_the_answer()
    {
        var scattered = QueueProjection.Project([Waiting(1, 20)], Now, [Busy(0, 30), Busy(10, 20), Busy(5, 25)]);
        var tidy = QueueProjection.Project([Waiting(1, 20)], Now, [Busy(0, 30)]);

        Assert.Equal(tidy.Single().ProjectedStart, scattered.Single().ProjectedStart);
    }

    [Fact]
    public void Somebody_who_will_not_be_reached_before_closing_is_flagged_not_hidden()
    {
        // Still in the queue, still owed an answer. Dropping them from the
        // projection would leave them staring at a page that says nothing.
        //
        // "Reached" means started, not finished. Somebody who begins at ten to
        // five is seen, even if the conversation runs past five; somebody who
        // would not begin until after five is not. Whether the host overruns is
        // a different question from whether they get their turn.
        var projection = QueueProjection.Project(
            [Waiting(1, 20), Waiting(2, 20)],
            Now,
            busy: null,
            closesAt: Now + Duration.FromMinutes(20));

        Assert.False(projection[0].BeyondClose);
        Assert.True(projection[1].BeyondClose);
        Assert.Equal(2, projection.Count);
    }

    [Fact]
    public void An_appointment_that_starts_before_closing_but_runs_past_it_is_not_flagged()
    {
        // The other half of that rule, asserted so it cannot drift: starting at
        // ten past a close of fifteen means being seen, not being turned away.
        var projection = QueueProjection.Project(
            [Waiting(1, 20)],
            Now + Duration.FromMinutes(10),
            busy: null,
            closesAt: Now + Duration.FromMinutes(15));

        Assert.False(projection.Single().BeyondClose);
    }

    [Fact]
    public void Nobody_is_flagged_when_the_queue_has_no_closing_time()
    {
        var projection = QueueProjection.Project([Waiting(1, 20), Waiting(2, 20)], Now);

        Assert.All(projection, entry => Assert.False(entry.BeyondClose));
    }

    [Fact]
    public void Starting_exactly_at_closing_time_counts_as_beyond_it()
    {
        var projection = QueueProjection.Project(
            [Waiting(1, 20)],
            Now,
            busy: null,
            closesAt: Now);

        Assert.True(projection.Single().BeyondClose);
    }

    [Fact]
    public void No_busy_time_behaves_exactly_as_before()
    {
        var entries = new[] { Waiting(1, 15), Waiting(2, 30) };

        var without = QueueProjection.Project(entries, Now);
        var withEmpty = QueueProjection.Project(entries, Now, []);

        Assert.Equal(
            without.Select(entry => entry.ProjectedStart),
            withEmpty.Select(entry => entry.ProjectedStart));
    }

    [Fact]
    public void Lengthening_one_appointment_moves_everyone_behind_by_the_difference()
    {
        // The reason a per-person length exists at all. A first counselling
        // session and a two minute signature are not the same conversation, and
        // charging everybody the same fifteen minutes makes every projection
        // behind them wrong in the same direction.
        var first = Waiting(1, 15);
        var second = Waiting(2, 15);
        var third = Waiting(3, 15);

        var before = QueueProjection.Project([first, second, third], Now);
        var after = QueueProjection.Project([first with { Expected = Duration.FromMinutes(60) }, second, third], Now);

        Assert.Equal(Duration.FromMinutes(45), after[1].ProjectedStart - before[1].ProjectedStart);
        Assert.Equal(Duration.FromMinutes(45), after[2].ProjectedStart - before[2].ProjectedStart);

        // And the person whose length changed is unaffected: they are already at
        // the front, and how long they need does not change when they start.
        Assert.Equal(before[0].ProjectedStart, after[0].ProjectedStart);
    }

    [Fact]
    public void Shortening_an_appointment_pulls_the_queue_forward()
    {
        var first = Waiting(1, 60);
        var second = Waiting(2, 15);

        var before = QueueProjection.Project([first, second], Now);
        var after = QueueProjection.Project([first with { Expected = Duration.FromMinutes(5) }, second], Now);

        Assert.Equal(Duration.FromMinutes(55), before[1].ProjectedStart - after[1].ProjectedStart);
    }

    [Fact]
    public void A_long_appointment_can_push_the_queue_past_closing()
    {
        // Worth asserting together: a length change is also a capacity change,
        // so the same edit that moves somebody can also mean they will not be
        // reached at all.
        var closesAt = Now + Duration.FromMinutes(30);

        var shortFirst = QueueProjection.Project([Waiting(1, 15), Waiting(2, 15)], Now, null, closesAt);
        var longFirst = QueueProjection.Project([Waiting(1, 45), Waiting(2, 15)], Now, null, closesAt);

        Assert.False(shortFirst[1].BeyondClose);
        Assert.True(longFirst[1].BeyondClose);
    }
}
