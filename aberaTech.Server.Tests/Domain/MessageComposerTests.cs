using aberaTech.Scheduling.Domain;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Domain;

public class MessageComposerTests
{
    // 2027-06-01 19:40 UTC is 14:40 in Chicago and 15:40 in New York.
    private static readonly Instant Start = Instant.FromUtc(2027, 6, 1, 19, 40);

    private static DateTimeZone Zone(string id) => DateTimeZoneProviders.Tzdb[id];

    [Fact]
    public void A_time_is_rendered_in_the_recipients_own_zone()
    {
        var chicago = MessageComposer.Compose(NotificationKind.Joined, "Neb", Start, Zone("America/Chicago"));
        var newYork = MessageComposer.Compose(NotificationKind.Joined, "Neb", Start, Zone("America/New_York"));

        Assert.Contains("2:40 PM", chicago);
        Assert.Contains("3:40 PM", newYork);
    }

    [Fact]
    public void A_time_always_carries_its_zone_so_it_cannot_be_read_as_another_one()
    {
        // The abbreviation, not a numeric offset: "3:40 PM CDT" is unambiguous
        // to a person, which a bare "3:40 PM" from a scheduling tool is not.
        var chicago = MessageComposer.Compose(NotificationKind.Imminent, "Neb", Start, Zone("America/Chicago"));
        var newYork = MessageComposer.Compose(NotificationKind.Imminent, "Neb", Start, Zone("America/New_York"));

        Assert.Contains("CDT", chicago);
        Assert.Contains("EDT", newYork);
    }

    [Theory]
    [InlineData(NotificationKind.Joined)]
    [InlineData(NotificationKind.TimeChanged)]
    [InlineData(NotificationKind.Imminent)]
    [InlineData(NotificationKind.YourTurn)]
    [InlineData(NotificationKind.Booked)]
    [InlineData(NotificationKind.Reminder)]
    public void Every_message_fits_in_one_billable_segment(NotificationKind kind)
    {
        // A body that spills past 160 characters silently doubles both the cost
        // and the consumption of the daily carrier cap.
        var message = MessageComposer.Compose(kind, "Neb Abera", Start, Zone("America/Chicago"));

        Assert.True(
            message.Length <= MessageComposer.SingleSegment,
            $"{kind} is {message.Length} characters: {message}");
    }

    [Fact]
    public void Every_message_names_the_host_so_it_is_not_read_as_spam()
    {
        foreach (var kind in Enum.GetValues<NotificationKind>())
        {
            var message = MessageComposer.Compose(kind, "Neb", Start, Zone("America/Chicago"));
            Assert.Contains("Neb", message);
        }
    }

    [Fact]
    public void An_unrecognised_kind_is_refused_rather_than_sent_blank()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MessageComposer.Compose((NotificationKind)99, "Neb", Start, Zone("America/Chicago")));
    }
}
