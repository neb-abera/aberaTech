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
}
