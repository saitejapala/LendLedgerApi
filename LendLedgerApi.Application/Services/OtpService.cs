using System;
using System.Threading.Tasks;
using LendLedgerApi.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LendLedgerApi.Application.Services
{
    public class OtpService : IOtpService
    {
        private readonly IRedisCacheService _redisCacheService;
        private readonly IEmailClient _emailClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OtpService> _logger;

        public OtpService(
            IRedisCacheService redisCacheService,
            IEmailClient emailClient,
            IConfiguration configuration,
            ILogger<OtpService> _logger)
        {
            this._redisCacheService = redisCacheService;
            this._emailClient = emailClient;
            this._configuration = configuration;
            this._logger = _logger;
        }

        public async Task GenerateAndSendOtpAsync(string email)
        {
            var emailLower = email.ToLower();
            // 1. Generate 6-digit OTP
            var code = Random.Shared.Next(100000, 999999).ToString();
            
            // 2. Cache in Redis for 10 minutes (600 seconds)
            string cacheKey = $"otp:{emailLower}";
            _redisCacheService.SetString(cacheKey, code, 600);

            // 3. Dispatch OTP
            var otpSettings = _configuration.GetSection("OtpSettings");
            var pipeToConsole = otpSettings.GetValue<bool>("PipeToConsole", true);

            if (pipeToConsole)
            {
                Console.WriteLine($"\n==================================================");
                Console.WriteLine($"[DEVELOPMENT OTP] Verification code for {emailLower} is: {code}");
                Console.WriteLine($"Expires in 10 minutes");
                Console.WriteLine($"==================================================\n");
                
                _logger.LogInformation("[DEVELOPMENT OTP] Logged OTP verification code to console/stdout.");
            }
            else
            {
                string htmlBody = $"<p>Your LendLedger verification code is: <strong>{code}</strong>. It will expire in 10 minutes.</p>";
                string textBody = $"Your LendLedger verification code is: {code}. It will expire in 10 minutes.";
                
                bool sent = await _emailClient.SendEmail(
                    toEmail: emailLower,
                    emailSubject: "LendLedger Authentication Code",
                    htmlEmailBody: htmlBody,
                    fallbackEmailBody: textBody,
                    emailCategory: "Authentication"
                );

                if (sent)
                {
                    _logger.LogInformation("Successfully sent OTP email to {Email} via email client.", emailLower);
                }
                else
                {
                    _logger.LogError("Failed to send OTP email to {Email} via email client.", emailLower);
                }
            }
        }

        public async Task<bool> VerifyOtpAsync(string email, string code)
        {
            var emailLower = email.ToLower();
            string cacheKey = $"otp:{emailLower}";
            var cachedCode = _redisCacheService.GetString(cacheKey);

            if (cachedCode == null || cachedCode != code)
            {
                return false;
            }

            // OTP is valid, remove it to prevent reuse/replay attacks
            _redisCacheService.RemoveKey(cacheKey);
            return true;
        }
    }
}
