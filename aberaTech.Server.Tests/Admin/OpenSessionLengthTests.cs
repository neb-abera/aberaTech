using aberaTech.Scheduling.Admin;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Admin;

public class OpenSessionLengthTests
{
    [Fact]
    public void Says_nothing_means_eight_hours()
    {
        Assert.Equal(Duration.FromHours(8), AdminEndpoints.OpenFor(null));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    [InlineData(24, 24)]
    public void An_asked_for_length_inside_the_bounds_is_kept(int asked, int kept)
    {
        Assert.Equal(Duration.FromHours(kept), AdminEndpoints.OpenFor(asked));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(100, 24)]
    public void A_length_outside_the_bounds_is_clamped_not_refused(int asked, int kept)
    {
        Assert.Equal(Duration.FromHours(kept), AdminEndpoints.OpenFor(asked));
    }
}
