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
    [InlineData(NotificationKind.ReminderDayBefore)]
    [InlineData(NotificationKind.Reminder)]
    [InlineData(NotificationKind.Cancelled)]
    [InlineData(NotificationKind.HostBooked)]
    [InlineData(NotificationKind.HostCancelled)]
    public void Every_message_fits_in_one_billable_segment(NotificationKind kind)
    {
        // A body that spills past 160 characters silently doubles both the cost
        // and the consumption of the daily carrier cap.
        // Measured with the brand prefix, since that is what actually goes out.
        var message = MessageComposer.Compose(kind, "Neb Abera", Start, Zone("America/Chicago"), "aberaTech");

        Assert.True(
            message.Length <= MessageComposer.SingleSegment,
            $"{kind} is {message.Length} characters: {message}");
    }

    /// <summary>The kinds that go to a visitor rather than to the host.</summary>
    private static readonly NotificationKind[] ToVisitor =
    [
        NotificationKind.Joined,
        NotificationKind.TimeChanged,
        NotificationKind.Imminent,
        NotificationKind.YourTurn,
        NotificationKind.Booked,
        NotificationKind.ReminderDayBefore,
        NotificationKind.Reminder,
        NotificationKind.Cancelled
    ];

    private static readonly NotificationKind[] ToHost =
    [
        NotificationKind.HostBooked,
        NotificationKind.HostCancelled
    ];

    [Fact]
    public void Every_message_to_a_visitor_names_the_host_so_it_is_not_read_as_spam()
    {
        // A text from an unknown number about an appointment is indistinguishable
        // from spam unless it says who it is from.
        foreach (var kind in ToVisitor)
        {
            var message = MessageComposer.Compose(kind, "Neb", Start, Zone("America/Chicago"));
            Assert.Contains("Neb", message);
        }
    }

    [Fact]
    public void Every_message_to_a_visitor_carries_the_registered_brand()
    {
        // Carriers require the brand the campaign was registered under to appear
        // in the message. Registering one name and sending another is how
        // traffic gets flagged.
        foreach (var kind in ToVisitor)
        {
            var message = MessageComposer.Compose(kind, "Neb Abera", Start, Zone("America/Chicago"), "aberaTech");
            Assert.StartsWith("aberaTech: ", message);
        }
    }

    [Fact]
    public void Messages_to_the_host_are_not_prefixed_with_the_brand()
    {
        // He does not need telling which brand is texting him about his own
        // calendar, and the characters are better spent elsewhere.
        foreach (var kind in ToHost)
        {
            var message = MessageComposer.Compose(kind, "Neb Abera", Start, Zone("America/Chicago"), "aberaTech");
            Assert.DoesNotContain("aberaTech", message);
        }
    }

    [Fact]
    public void Messages_to_the_host_do_not_need_to_name_the_host()
    {
        // The reason for the rule above does not apply here: the recipient is
        // the host, who does not need telling who they are. They still carry a
        // time, which is the whole content.
        foreach (var kind in ToHost)
        {
            var message = MessageComposer.Compose(kind, "Neb", Start, Zone("America/Chicago"));
            Assert.Contains("2:40", message);
        }
    }

    [Fact]
    public void Every_kind_is_covered_by_one_of_those_two_lists()
    {
        // So a kind added later cannot quietly escape both checks.
        var covered = ToVisitor.Concat(ToHost).ToHashSet();

        Assert.Equal(Enum.GetValues<NotificationKind>().ToHashSet(), covered);
    }

    [Fact]
    public void The_first_message_a_visitor_gets_carries_the_opt_out()
    {
        // Convention, and what keeps a sending number in good standing. Only the
        // first message in a thread needs it; repeating it on every reminder
        // wastes characters that push a message into a second segment.
        foreach (var kind in new[] { NotificationKind.Booked, NotificationKind.Joined })
        {
            Assert.Contains("STOP", MessageComposer.Compose(kind, "Neb", Start, Zone("America/Chicago")));
        }
    }

    [Fact]
    public void An_unrecognised_kind_is_refused_rather_than_sent_blank()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MessageComposer.Compose((NotificationKind)99, "Neb", Start, Zone("America/Chicago")));
    }
}
