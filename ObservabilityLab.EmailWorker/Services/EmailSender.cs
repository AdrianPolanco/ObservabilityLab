

using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using ObservabilityLab.EmailWorker.Options;

namespace ObservabilityLab.EmailWorker.Services
{
    internal class EmailSender(IOptionsMonitor<SmtpOptions> optionsMonitor, ILogger<EmailSender> logger)
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

            using(SmtpClient smtpClient = new SmtpClient()) {
                smtpClient.CheckCertificateRevocation = false;
                var options = optionsMonitor.CurrentValue;
                await smtpClient.ConnectAsync(options.SmtpServer, options.Port, false, cancellationToken);

                if (smtpClient.Capabilities.HasFlag(SmtpCapabilities.Authentication)
                    && !string.IsNullOrWhiteSpace(options.UserName))
                {
                    await smtpClient.AuthenticateAsync(options.UserName, options.Password, cancellationToken);
                }

                await smtpClient.SendAsync(email, cancellationToken);
                await smtpClient.DisconnectAsync(true, cancellationToken);
            }

            logger.LogInformation("Email sent successfully to {Destination}", to);

            return true;
        }
    }
}
