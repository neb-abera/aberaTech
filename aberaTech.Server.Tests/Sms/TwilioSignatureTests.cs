using aberaTech.Scheduling.Sms;
using Xunit;

namespace aberaTech.Server.Tests.Sms;

public class TwilioSignatureTests
{
    // The worked example from Twilio's own security documentation. Pinning the
    // published vector is the only way to know this implementation agrees with
    // the service rather than merely being self-consistent.
    private const string DocsUrl = "https://example.com/myapp.php?foo=1&bar=2";
    private const string DocsToken = "12345";
    private const string DocsSignature = "L/OH5YylLD5NRKLltdqwSvS0BnU=";

    private static KeyValuePair<string, string>[] DocsParameters() =>
    [
        new("CallSid", "CA1234567890ABCDE"),
        new("Caller", "+14158675310"),
        new("Digits", "1234"),
        new("From", "+14158675310"),
        new("To", "+18005551212")
    ];

    [Fact]
    public void It_reproduces_the_published_example()
    {
        Assert.Equal(DocsSignature, TwilioSignature.Compute(DocsToken, DocsUrl, DocsParameters()));
        Assert.True(TwilioSignature.IsValid(DocsToken, DocsUrl, DocsParameters(), DocsSignature));
    }

    [Fact]
    public void Parameter_order_on_the_wire_does_not_matter()
    {
        // Form fields arrive in whatever order the sender chose; the signature
        // is defined over them sorted, so shuffling must not change the answer.
        var shuffled = DocsParameters().Reverse().ToArray();

        Assert.Equal(DocsSignature, TwilioSignature.Compute(DocsToken, DocsUrl, shuffled));
    }

    [Fact]
    public void A_tampered_parameter_invalidates_the_signature()
    {
        // The attack this exists to stop: claiming a message was delivered when
        // it was not, which would switch off the retry for exactly the messages
        // that needed it.
        var tampered = DocsParameters();
        tampered[4] = new KeyValuePair<string, string>("To", "+15550000000");

        Assert.False(TwilioSignature.IsValid(DocsToken, DocsUrl, tampered, DocsSignature));
    }

    [Fact]
    public void A_different_url_invalidates_the_signature()
    {
        Assert.False(TwilioSignature.IsValid(
            DocsToken,
            "https://example.com/myapp.php?foo=1&bar=3",
            DocsParameters(),
            DocsSignature));
    }

    [Fact]
    public void The_wrong_auth_token_invalidates_the_signature()
    {
        Assert.False(TwilioSignature.IsValid("54321", DocsUrl, DocsParameters(), DocsSignature));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64")]
    public void A_missing_or_malformed_signature_is_refused(string? provided)
    {
        // Notably it must refuse rather than throw: an unsigned request is a
        // routine event on a public endpoint, not an exceptional one.
        Assert.False(TwilioSignature.IsValid(DocsToken, DocsUrl, DocsParameters(), provided));
    }

    [Fact]
    public void A_request_with_no_parameters_signs_the_url_alone()
    {
        var signature = TwilioSignature.Compute(DocsToken, DocsUrl, []);

        Assert.NotEmpty(signature);
        Assert.True(TwilioSignature.IsValid(DocsToken, DocsUrl, [], signature));
    }

    [Fact]
    public void Sorting_is_ordinal_so_case_orders_the_way_twilio_orders_it()
    {
        // Uppercase sorts before lowercase in an ordinal comparison and after it
        // in some culture-aware ones. Getting this wrong produces a signature
        // that is correct on one machine's locale and wrong on another's.
        KeyValuePair<string, string>[] mixed = [new("a", "1"), new("B", "2")];

        var expected = TwilioSignature.Compute(DocsToken, "https://x/", [new("B", "2"), new("a", "1")]);

        Assert.Equal(expected, TwilioSignature.Compute(DocsToken, "https://x/", mixed));
    }
}
