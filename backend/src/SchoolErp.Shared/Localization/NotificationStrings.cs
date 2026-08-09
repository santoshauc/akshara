using System.Globalization;

namespace SchoolErp.Shared.Localization;

/// <summary>Language codes a guardian can be written to in.</summary>
public static class NotificationLanguages
{
    public const string English = "en";

    public const string Telugu = "te";

    public static readonly IReadOnlyList<string> Supported = [English, Telugu];

    /// <summary>
    /// Maps anything a client sends ("TE", "te-IN", null, "fr") onto a language
    /// we actually have templates for. Unknown codes become English rather than
    /// failing — a parent still gets the message, just not in their language.
    /// </summary>
    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return English;
        }

        var primary = code.Trim().Split('-')[0].ToLowerInvariant();
        return Supported.Contains(primary) ? primary : English;
    }

    public static bool IsSupported(string? code) =>
        !string.IsNullOrWhiteSpace(code) && Supported.Contains(code.Trim().ToLowerInvariant());
}

/// <summary>Template keys for guardian-facing notifications.</summary>
public static class NotificationTemplates
{
    /// <summary>Args: student first name, date, school name.</summary>
    public const string Absence = "notify.absence";

    /// <summary>Args: exam name, student first name, school name.</summary>
    public const string ResultsPublished = "notify.results";

    /// <summary>Args: amount, student first name, school name, receipt number.</summary>
    public const string PaymentReceived = "notify.payment";

    /// <summary>Args: student name, school name.</summary>
    public const string BusBoarded = "notify.busBoarded";

    /// <summary>Args: student name, school name.</summary>
    public const string BusDropped = "notify.busDropped";

    /// <summary>Args: student first name, school name, local time, released to, pass number.</summary>
    public const string GatePass = "notify.gatePass";

    /// <summary>Args: overdue amount, student name, school name.</summary>
    public const string FeeReminder = "notify.feeReminder";
}

/// <summary>
/// Guardian-facing SMS / WhatsApp / push text. English is the source of truth;
/// Telugu must cover every key (unit-enforced), exactly like
/// <see cref="PortalStrings"/>. Each template has a <c>.title</c> (push title,
/// unused by SMS) and a <c>.body</c>.
/// <para>
/// Only school-authored data (names, receipt numbers) is interpolated — those
/// stay as entered, same rule the apps follow.
/// </para>
/// </summary>
public static class NotificationStrings
{
    public static readonly IReadOnlyDictionary<string, string> En = new Dictionary<string, string>
    {
        ["notify.absence.title"] = "Absence noted",
        ["notify.absence.body"] =
            "{0} was marked absent on {1:dd MMM yyyy} at {2}. " +
            "Please contact the school if this is unexpected.",

        ["notify.results.title"] = "Results published",
        ["notify.results.body"] =
            "Results for {0} are now available for {1} at {2}. " +
            "Open the parent app to view the report card.",

        ["notify.payment.title"] = "Payment received",
        ["notify.payment.body"] =
            "Payment of Rs.{0:0.##} received for {1} at {2}. Receipt {3}. Thank you.",

        ["notify.busBoarded.title"] = "On the bus",
        ["notify.busBoarded.body"] = "{0} boarded the school bus just now. — {1}",

        ["notify.busDropped.title"] = "Dropped off",
        ["notify.busDropped.body"] = "{0} was dropped off by the school bus just now. — {1}",

        ["notify.gatePass.title"] = "Early release",
        ["notify.gatePass.body"] = "{0} left {1} at {2:HH:mm} with {3}. Pass {4}.",

        ["notify.feeReminder.title"] = "Fee reminder",
        ["notify.feeReminder.body"] =
            "Fee reminder: ₹{0:N0} is overdue for {1} at {2}. " +
            "Please pay at the school office or in the parent app.",
    };

    public static readonly IReadOnlyDictionary<string, string> Te = new Dictionary<string, string>
    {
        ["notify.absence.title"] = "గైర్హాజరు నమోదైంది",
        ["notify.absence.body"] =
            "{2} లో {1:dd MMM yyyy} నాడు {0} గైర్హాజరుగా నమోదయ్యారు. " +
            "ఇది ఊహించనిది అయితే పాఠశాలను సంప్రదించండి.",

        ["notify.results.title"] = "ఫలితాలు విడుదలయ్యాయి",
        ["notify.results.body"] =
            "{2} లో {1} కోసం {0} ఫలితాలు ఇప్పుడు అందుబాటులో ఉన్నాయి. " +
            "రిపోర్ట్ కార్డ్ చూడటానికి పేరెంట్ యాప్ తెరవండి.",

        ["notify.payment.title"] = "చెల్లింపు అందింది",
        ["notify.payment.body"] =
            "{2} లో {1} కోసం రూ.{0:0.##} చెల్లింపు అందింది. రసీదు {3}. ధన్యవాదాలు.",

        ["notify.busBoarded.title"] = "బస్సు ఎక్కారు",
        ["notify.busBoarded.body"] = "{0} ఇప్పుడే స్కూల్ బస్సు ఎక్కారు. — {1}",

        ["notify.busDropped.title"] = "బస్సు దిగారు",
        ["notify.busDropped.body"] = "{0} ఇప్పుడే స్కూల్ బస్సు నుండి దిగారు. — {1}",

        ["notify.gatePass.title"] = "ముందుగా పంపివేత",
        ["notify.gatePass.body"] = "{0} {2:HH:mm} గంటలకు {3} తో కలిసి {1} నుండి వెళ్లారు. పాస్ {4}.",

        ["notify.feeReminder.title"] = "ఫీజు రిమైండర్",
        ["notify.feeReminder.body"] =
            "ఫీజు రిమైండర్: {2} లో {1} కోసం ₹{0:N0} బకాయి ఉంది. " +
            "పాఠశాల కార్యాలయంలో లేదా పేరెంట్ యాప్‌లో చెల్లించండి.",
    };

    /// <summary>
    /// Dates and amounts are formatted in the reader's own culture, so a Telugu
    /// guardian gets Telugu month names. Falls back to the invariant culture if
    /// the host has no ICU data for the language.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, CultureInfo> Cultures =
        new Dictionary<string, CultureInfo>
        {
            [NotificationLanguages.English] = CultureFor("en-IN"),
            [NotificationLanguages.Telugu] = CultureFor("te-IN"),
        };

    /// <summary>Renders one template in the guardian's language.</summary>
    public static string Render(string? language, string key, params object?[] args)
    {
        var lang = NotificationLanguages.Normalize(language);
        var table = lang == NotificationLanguages.Telugu ? Te : En;
        if (!table.TryGetValue(key, out var format) && !En.TryGetValue(key, out format))
        {
            throw new ArgumentException($"Unknown notification template '{key}'.", nameof(key));
        }

        return string.Format(Cultures[lang], format, args);
    }

    /// <summary>Title and body of a template, both in the guardian's language.</summary>
    public static (string Title, string Body) RenderMessage(
        string? language, string templateKey, params object?[] args) =>
        (Render(language, $"{templateKey}.title", args),
         Render(language, $"{templateKey}.body", args));

    private static CultureInfo CultureFor(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
