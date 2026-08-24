namespace aberaTech.Scheduling.Compliance;

/// <summary>
/// The text messaging terms and privacy policy, served as real HTML.
/// </summary>
/// <remarks>
/// Rendered on the server rather than by the client on purpose. A carrier
/// reviewing a 10DLC campaign may fetch these URLs programmatically, and the
/// rest of this site is a single page application: a plain fetch returns an
/// empty shell with the content arriving later from JavaScript. A human opening
/// it in a browser sees everything, an automated check sees nothing, and the
/// registration is refused for pages that look blank.
///
/// These are static legal text with no interactivity, so there was never a
/// reason for them to need JavaScript. They also now render with it switched
/// off, and instantly.
/// </remarks>
public static class CompliancePages
{
    public static IEndpointRouteBuilder MapCompliancePages(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/sms-terms", () => Results.Content(Terms, "text/html; charset=utf-8"));
        routes.MapGet("/sms-privacy", () => Results.Content(Privacy, "text/html; charset=utf-8"));

        return routes;
    }

    /// <summary>
    /// Enough style to be readable and to look like the rest of the site,
    /// inline so the page is one request and cannot render unstyled.
    /// </summary>
    private const string Style = """
        <style>
          :root { color-scheme: dark; }
          body {
            margin: 0 auto; padding: 3rem 1.25rem 5rem; max-width: 46rem;
            background: hsl(220, 35%, 3%); color: hsl(0, 0%, 100%);
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
            line-height: 1.65;
          }
          h1 { font-size: 2rem; margin-bottom: .25rem; }
          h2 { font-size: 1.1rem; margin-top: 2.25rem; margin-bottom: .5rem; }
          p, li { color: hsl(220, 20%, 80%); }
          .lede { color: hsl(220, 20%, 65%); margin-top: 0; }
          hr { border: 0; border-top: 1px solid hsl(220, 20%, 25%); margin: 2rem 0; }
          a { color: hsl(210, 100%, 70%); }
          strong { color: hsl(0, 0%, 100%); }
          footer { margin-top: 3rem; font-size: .875rem; color: hsl(220, 20%, 65%); }
        </style>
        """;

    // Internal rather than private so the tests can pin the phrases campaign
    // vetting greps for. Twilio error 30908 requires the *privacy policy* — not
    // just the terms — to carry the non-sharing statement, message frequency
    // and "message and data rates may apply".
    internal const string Terms = $"""
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Text message terms — aberaTech</title>
        <meta name="description" content="Terms for appointment text messages sent by aberaTech (abera.tech).">
        {Style}
        </head>
        <body>
        <h1>Text message terms</h1>
        <p class="lede">For text messages sent by <strong>aberaTech</strong>, operated by Neb Abera, about appointments booked on abera.tech.</p>
        <hr>

        <h2>What you are agreeing to</h2>
        <p>If you tick the box asking for text updates when you book a time or join a queue at
        <a href="https://abera.tech/schedule">abera.tech/schedule</a>, Neb Abera will send you text messages about that
        appointment and nothing else. Messages are sent under the brand name <strong>aberaTech</strong>. You are never signed up automatically: the box is unticked until you tick it, and
        leaving it alone means you get no texts at all.</p>

        <h2>What you will receive</h2>
        <p>A confirmation when you book, a reminder the day before, a reminder about an hour before, and a message if the
        appointment is cancelled. If you join a queue: a message confirming your place, a message if your estimated time
        moves by more than about ten minutes, one shortly before your turn, and one when it is your turn.</p>

        <h2>How often</h2>
        <p>Message frequency varies, and depends entirely on what you book. A single appointment is usually four
        messages. There is no marketing, no newsletter, and nothing is sent to you for any reason other than an
        appointment you asked for.</p>

        <h2>Cost</h2>
        <p>There is no charge from aberaTech for these messages. Message and data rates may apply, depending on your
        plan with your mobile carrier.</p>

        <h2>Stopping messages</h2>
        <p>Reply <strong>STOP</strong> to any message to stop all of them. You may also reply QUIT, END, CANCEL, REVOKE,
        OPT OUT or UNSUBSCRIBE. You will get one confirmation and then nothing further. Reply <strong>START</strong> to
        begin again.</p>
        <p>Stopping texts does not cancel your appointment. It only stops the messages.</p>

        <h2>Help</h2>
        <p>Reply <strong>HELP</strong> to any message, or email
        <a href="mailto:support@alias.abera.tech">support@alias.abera.tech</a>.</p>

        <h2>Carriers</h2>
        <p>Mobile carriers are not liable for delayed or undelivered messages.</p>

        <footer>
        See the <a href="/sms-privacy">text message privacy policy</a> for what happens to your phone number, or return to
        <a href="/schedule">booking</a>.
        </footer>
        </body>
        </html>
        """;

    internal const string Privacy = $"""
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Text message privacy — aberaTech</title>
        <meta name="description" content="What happens to a phone number given to aberaTech (abera.tech) for appointment text messages.">
        {Style}
        </head>
        <body>
        <h1>Text message privacy</h1>
        <p class="lede">What happens to a phone number given to <strong>aberaTech</strong> at abera.tech.</p>
        <hr>

        <h2>The short version</h2>
        <p>Your phone number is used to text you about your own appointment and for nothing else. It is not sold, not
        rented, and not shared with anybody for marketing. If you do not ask for text updates, you are not asked for a
        number at all.</p>

        <h2>What is collected</h2>
        <p>The name you type, the time you booked, the time zone your browser reports, and, only if you ask for text
        updates, your mobile number. Nothing else. There is no account, no password, and no tracking of you across other
        sites.</p>

        <h2>Who it is shared with</h2>
        <p>Only the messaging provider that carries the text, and only so that it can be delivered. Your mobile phone
        number and messaging consent data are not shared, sold, or provided to third parties or affiliates for marketing
        or promotional purposes. Text messaging originator opt-in data and consent are never shared with anyone.</p>

        <h2>The messages themselves</h2>
        <p>Texts are sent only if you tick the box asking for them when you book or join the queue, and they are only
        about that appointment: a confirmation, reminders, and queue updates. Message frequency varies with what you
        book — a single appointment is usually four messages. Message and data rates may apply, depending on your plan
        with your mobile carrier. Reply <strong>HELP</strong> to any message for help.</p>

        <h2>Where it is kept</h2>
        <p>In a database in Microsoft Azure, encrypted at rest, reachable only by this site. Phone numbers are
        deliberately kept out of application logs.</p>

        <h2>How long it is kept</h2>
        <p>For as long as needed to run the appointment and keep a record that it happened. Ask and it will be
        deleted.</p>

        <h2>Stopping messages, and removing your number</h2>
        <p>Reply <strong>STOP</strong> to any message to stop all of them. To have your number removed entirely, email
        <a href="mailto:support@alias.abera.tech">support@alias.abera.tech</a>.</p>

        <footer>
        See the <a href="/sms-terms">text message terms</a> for what gets sent and how often, or return to
        <a href="/schedule">booking</a>.
        </footer>
        </body>
        </html>
        """;
}
