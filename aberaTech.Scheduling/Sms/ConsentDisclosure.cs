namespace aberaTech.Scheduling.Sms;

/// <summary>
/// The exact wording shown beside the opt-in box.
/// </summary>
/// <remarks>
/// Held on the server and sent to the page, rather than written into the page,
/// so there is one copy. Two copies drift, and the one that drifts is the one
/// nobody is looking at.
///
/// It matters more than tidiness. If somebody ever disputes having agreed to be
/// texted, the question is not whether a flag was set but what they were shown
/// when they set it. Storing the server's own copy alongside the consent answers
/// that; storing a copy the browser sent back would only prove what the browser
/// claimed.
/// </remarks>
public static class ConsentDisclosure
{
    /// <summary>
    /// Every element a carrier looks for on a web opt-in: what is sent, that
    /// frequency varies, that rates may apply, HELP and STOP, and where the
    /// terms and privacy policy are.
    /// </summary>
    public const string Current =
        "You will get a confirmation, a reminder the day before, a reminder about an hour before, "
        + "and a message if it is cancelled. Message frequency varies. Message and data rates may apply. "
        + "Reply HELP for help, STOP to stop. See the text message terms at /sms-terms and the privacy "
        + "policy at /sms-privacy. Your number is used only for this and is never shared for marketing.";
}
