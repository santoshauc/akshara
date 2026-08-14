namespace SchoolErp.Application.Abstractions;

/// <summary>
/// Delivers email. Implemented in Infrastructure, activated by configuration
/// exactly like the SMS and push channels: without <c>Email:Provider=smtp</c>
/// the development sender logs and nothing leaves the machine.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends <paramref name="body"/> to <paramref name="to"/>.
    /// <para>
    /// Plain text, deliberately. Every message the outbox carries is a short
    /// factual notice already written for SMS, and sending the same sentence as
    /// HTML would buy nothing but a spam-score problem and a second copy of
    /// every template to keep in step.
    /// </para>
    /// </summary>
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
