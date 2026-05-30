using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using PostmarkDotNet;
using LendLedgerApi.Application.Interfaces;

namespace LendLedgerApi.Email
{
    public class EmailClient : IEmailClient
    {
        private readonly IConfiguration _configuration;
        private readonly string _apiKey;
        private readonly string _ccEmail;
        private readonly string _fromEmail;
        private readonly string _emailProvider;
        private readonly string _replyTo;

        public EmailClient(IConfiguration configuration)
        {
            _configuration = configuration;
            _apiKey = _configuration["EmailSettings:PostmarkKey"]?.ToString() ?? string.Empty;
            _ccEmail = _configuration["EmailSettings:CcEmail"]?.ToString() ?? string.Empty;
            _fromEmail = _configuration["EmailSettings:FromEmail"]?.ToString() ?? "onboarding@resend.dev";
            _emailProvider = _configuration["EmailSettings:EmailProvider"]?.ToString() ?? "Postmark";
            _replyTo = _configuration["EmailSettings:ReplyTo"]?.ToString() ?? string.Empty;
        }

        public async Task<bool> SendEmail(string toEmail, string emailSubject, string htmlEmailBody, string fallbackEmailBody, bool trackOpens = false, string emailCategory = "")
        {
            if (_emailProvider.Equals("Postmark", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(_apiKey))
                {
                    return false;
                }

                var message = new PostmarkMessage()
                {
                    To = toEmail,
                    From = _fromEmail,
                    TrackOpens = trackOpens,
                    Subject = emailSubject,
                    HtmlBody = htmlEmailBody
                };

                if (!string.IsNullOrWhiteSpace(_ccEmail))
                {
                    message.Cc = _ccEmail;
                }
                if (!string.IsNullOrWhiteSpace(emailCategory))
                {
                    message.Tag = emailCategory;
                }
                if (!string.IsNullOrWhiteSpace(fallbackEmailBody))
                {
                    message.TextBody = fallbackEmailBody;
                }
                if (!string.IsNullOrWhiteSpace(_replyTo))
                {
                    message.ReplyTo = _replyTo;
                }

                try
                {
                    var client = new PostmarkClient(_apiKey);
                    var sendResult = await client.SendMessageAsync(message);
                    return sendResult.Status == PostmarkStatus.Success;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return false;
        }
    }
}
