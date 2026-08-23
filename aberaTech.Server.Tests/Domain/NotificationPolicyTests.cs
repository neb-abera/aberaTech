using aberaTech.Scheduling.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Domain;

/// <summary>
/// Every assertion here is also an assertion about the phone bill and the daily
/// carrier cap, which is why the "does not send" cases outnumber the others.
/// </summary>
public class NotificationPolicyTests
{
    private static readonly Instant Now = Instant.FromUtc(2027, 6, 1, 14, 0);

    [Fact]
    public void Somebody_who_has_heard_nothing_is_welcomed_once()
    {
        var due = NotificationPolicy.Decide(
            new NotificationState(),
            Now + Duration.FromHours(2),
            Now,
            isFront: false);

        Assert.Equal([NotificationKind.Joined], due);
    }

    [Fact]
    public void A_small_drift_is_not_worth_a_text_message()
    {
        var announced = Now + Duration.FromHours(2);
        var state = new NotificationState(LastAnnouncedStart: announced);

        var due = NotificationPolicy.Decide(state, announced + Duration.FromMinutes(4), Now, isFront: false);

        Assert.Empty(due);
    }

    [Fact]
    public void A_drift_past_the_tolerance_is_announced()
    {
        var announced = Now + Duration.FromHours(2);
        var state = new NotificationState(LastAnnouncedStart: announced);

        var due = NotificationPolicy.Decide(state, announced + Duration.FromMinutes(11), Now, isFront: false);

        Assert.Equal([NotificationKind.TimeChanged], due);
    }

    [Fact]
    public void Moving_earlier_is_announced_too()
    {
        // Being told you are on sooner matters more than being told you are on
        // later; an absolute comparison catches both.
        var announced = Now + Duration.FromHours(2);
        var state = new NotificationState(LastAnnouncedStart: announced);

        var due = NotificationPolicy.Decide(state, announced - Duration.FromMinutes(25), Now, isFront: false);

        Assert.Equal([NotificationKind.TimeChanged], due);
    }

    [Fact]
    public void The_tolerance_boundary_counts_as_worth_announcing()
    {
        var announced = Now + Duration.FromHours(2);
        var state = new NotificationState(LastAnnouncedStart: announced);

        var due = NotificationPolicy.Decide(
            state,
            announced + NotificationPolicy.DefaultTolerance,
            Now,
            isFront: false);

        Assert.Contains(NotificationKind.TimeChanged, due);
    }

    [Fact]
    public void The_imminent_warning_fires_once_and_then_stays_quiet()
    {
        var soon = Now + Duration.FromMinutes(8);

        var first = NotificationPolicy.Decide(
            new NotificationState(LastAnnouncedStart: soon),
            soon,
            Now,
            isFront: false);
        var second = NotificationPolicy.Decide(
            new NotificationState(LastAnnouncedStart: soon, ImminentSent: true),
            soon,
            Now,
            isFront: false);

        Assert.Equal([NotificationKind.Imminent], first);
        Assert.Empty(second);
    }

    [Fact]
    public void Being_called_fires_once_and_then_stays_quiet()
    {
        var state = new NotificationState(LastAnnouncedStart: Now, ImminentSent: true);

        var first = NotificationPolicy.Decide(state, Now, Now, isFront: true);
        var second = NotificationPolicy.Decide(state with { TurnSent = true }, Now, Now, isFront: true);

        Assert.Equal([NotificationKind.YourTurn], first);
        Assert.Empty(second);
    }

    [Fact]
    public void Joining_an_empty_queue_says_hello_before_it_says_you_are_up()
    {
        var due = NotificationPolicy.Decide(new NotificationState(), Now, Now, isFront: true);

        Assert.Equal(
            [NotificationKind.Joined, NotificationKind.Imminent, NotificationKind.YourTurn],
            due);
    }

    [Fact]
    public void A_queue_that_jitters_around_the_tolerance_does_not_send_on_every_recalculation()
    {
        // The realistic failure: appointments end a few minutes either side of
        // their estimate all afternoon. Nothing here should reach a handset.
        var announced = Now + Duration.FromHours(3);
        var state = new NotificationState(LastAnnouncedStart: announced);

        foreach (var drift in new[] { 2, -3, 5, -1, 4, -4 })
        {
            var due = NotificationPolicy.Decide(
                state,
                announced + Duration.FromMinutes(drift),
                Now,
                isFront: false);

            Assert.Empty(due);
        }
    }
}
