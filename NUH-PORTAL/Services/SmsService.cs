using NUH_PORTAL.Data;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Services
{
    public class SmsService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SmsService> _logger;

        public SmsService(AppDbContext context, ILogger<SmsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> SendOtpAsync(string mobile, string code)
        {
            var message = $"رمز التحقق الخاص بك للتسجيل في الإسكان الجامعي: {code}. صالح لمدة 5 دقائق.";

            try
            {
                _logger.LogInformation("Sending OTP to {Mobile}", mobile);

                var log = new SMSLog
                {
                    Mobile = mobile,
                    Provider = "Simulated",
                    Message = message,
                    Status = "Sent",
                    SentDate = DateTime.UtcNow
                };

                _context.SMSLogs.Add(log);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send SMS to {Mobile}", mobile);

                var log = new SMSLog
                {
                    Mobile = mobile,
                    Provider = "Simulated",
                    Message = message,
                    Status = "Failed",
                    SentDate = DateTime.UtcNow
                };

                _context.SMSLogs.Add(log);
                await _context.SaveChangesAsync();

                return false;
            }
        }
    }
}
