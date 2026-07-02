

using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using ObservabilityLab.EmailWorker.Options;
using System.Diagnostics;

namespace ObservabilityLab.EmailWorker.Services
{
    internal class EmailSender(IOptionsMonitor<SmtpOptions> optionsMonitor, ILogger<EmailSender> logger, ActivitySource activitySource)
    {
        public async Task<bool> SendEmailAsync(string to, string subject, string fileName, byte[] fileBytes, CancellationToken cancellationToken)
        {
            var email = new MimeMessage();

            email.From.Add(new MailboxAddress("Sender", optionsMonitor.CurrentValue.From));
            email.To.Add(new MailboxAddress("Recipient", to));
            email.Subject = subject;

            BodyBuilder body = new BodyBuilder();

            body.Attachments.Add(fileName, fileBytes);
            email.Body = body.ToMessageBody();

            var options = optionsMonitor.CurrentValue;

            // MailKit talks raw SMTP over a socket — it doesn't go through HttpClient, so
            // OpenTelemetry.Instrumentation.Http never sees it. This is the case for a hand-written
            // Client span: an outbound call to a dependency the auto-instrumentation can't reach.
            // net.peer.* are the OTel semantic-convention names for "what remote host/port did we call".
            using var smtpSpan = activitySource.StartActivity("SMTP send", ActivityKind.Client);
            smtpSpan?.SetTag("net.peer.name", options.SmtpServer);
            smtpSpan?.SetTag("net.peer.port", options.Port);
            smtpSpan?.SetTag("messaging.destination", to);

            try
            {
                using SmtpClient smtpClient = new SmtpClient();
                smtpClient.CheckCertificateRevocation = false;
                await smtpClient.ConnectAsync(options.SmtpServer, options.Port, false, cancellationToken);

                if (smtpClient.Capabilities.HasFlag(SmtpCapabilities.Authentication)
                    && !string.IsNullOrWhiteSpace(options.UserName))
                {
                    await smtpClient.AuthenticateAsync(options.UserName, options.Password, cancellationToken);
                }

                await smtpClient.SendAsync(email, cancellationToken);
                await smtpClient.DisconnectAsync(true, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send email to {Destination} via {SmtpServer}:{Port}.", to, options.SmtpServer, options.Port);
                smtpSpan?.AddException(ex);
                smtpSpan?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }

            logger.LogInformation("Email sent successfully to {Destination}", to);
            smtpSpan?.SetStatus(ActivityStatusCode.Ok);

            return true;
        }
    }
}
