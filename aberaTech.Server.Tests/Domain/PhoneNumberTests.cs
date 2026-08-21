using aberaTech.Scheduling.Domain;
using Xunit;

namespace aberaTech.Server.Tests.Domain;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("9134999497")]
    [InlineData("913-499-9497")]
    [InlineData("(913) 499-9497")]
    [InlineData("+1 913 499 9497")]
    [InlineData("1-913-499-9497")]
    public void The_ways_a_us_number_gets_typed_all_normalise_to_one_identity(string input)
    {
        Assert.True(PhoneNumber.TryParse(input, out var number));
        Assert.Equal("+19134999497", number!.Value.E164);
    }

    [Theory]
    [InlineData("+44 20 7946 0958")]  // toll risk: not North American
    [InlineData("+61 2 5550 1234")]
    [InlineData("013-499-9497")]      // area code may not start with 0
    [InlineData("113-499-9497")]      // ...or 1
    [InlineData("913-099-9497")]      // exchange may not start with 0
    [InlineData("12345")]
    [InlineData("")]
    [InlineData(null)]
    public void Everything_else_is_refused(string? input)
    {
        Assert.False(PhoneNumber.TryParse(input, out _));
    }

    [Fact]
    public void An_absurdly_long_input_is_refused_before_any_work_is_done()
    {
        Assert.False(PhoneNumber.TryParse(new string('9', 5000), out _));
    }

    [Fact]
    public void Last_four_is_enough_to_confirm_a_number_without_printing_it()
    {
        Assert.True(PhoneNumber.TryParse("913-499-9497", out var number));
        Assert.Equal("9497", number!.Value.Last4);
    }
}
