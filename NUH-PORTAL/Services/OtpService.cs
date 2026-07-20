using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;
using System.Security.Cryptography;
using System.Text;

namespace NUH_PORTAL.Services
{
    public class OtpService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger<OtpService> _logger;

        public OtpService(AppDbContext context, IConfiguration config, ILogger<OtpService> logger)
        {
            _context = context;
            _config = config;
            _logger = logger;
        }

        public async Task<(string otpCode, DateTime expiresAt)> GenerateOtpAsync(string mobile, string? ipAddress, string? codeOverride = null)
        {
            var recent = await _context.OTPVerifications
                .Where(o => o.Mobile == mobile && o.VerifiedAt == null && o.CreatedAt > DateTime.UtcNow.AddMinutes(-1))
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (recent != null)
                throw new InvalidOperationException("يرجى الانتظار دقيقة قبل طلب رمز تحقق جديد");

            var code = codeOverride ?? RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            var salt = _config["Otp:Salt"] ?? "NUH_OTP_SALT_2026";
            var hash = ComputeHash(code, salt);
            var expiresAt = DateTime.UtcNow.AddMinutes(5);

            var otp = new OTPVerification
            {
                Mobile = mobile,
                OTPHash = hash,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                Attempts = 0,
                IPAddress = ipAddress
            };

            _context.OTPVerifications.Add(otp);
            await _context.SaveChangesAsync();

            return (code, expiresAt);
        }

        public async Task<bool> VerifyOtpAsync(string mobile, string code)
        {
            var salt = _config["Otp:Salt"] ?? "NUH_OTP_SALT_2026";
            var hash = ComputeHash(code, salt);

            var otp = await _context.OTPVerifications
                .Where(o => o.Mobile == mobile && o.VerifiedAt == null && o.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otp == null)
                return false;

            if (otp.Attempts >= 3)
            {
                _logger.LogWarning("OTP locked for {Mobile} after {Attempts} attempts", mobile, otp.Attempts);
                return false;
            }

            otp.Attempts++;

            if (otp.OTPHash != hash)
            {
                await _context.SaveChangesAsync();
                return false;
            }

            otp.VerifiedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task CleanupExpiredOtpsAsync()
        {
            var expired = await _context.OTPVerifications
                .Where(o => o.ExpiresAt < DateTime.UtcNow && o.VerifiedAt == null)
                .Take(100)
                .ToListAsync();

            if (expired.Count > 0)
            {
                _context.OTPVerifications.RemoveRange(expired);
                await _context.SaveChangesAsync();
            }
        }

        private static string ComputeHash(string code, string salt)
        {
            var input = Encoding.UTF8.GetBytes(code + salt);
            var hash = SHA256.HashData(input);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
