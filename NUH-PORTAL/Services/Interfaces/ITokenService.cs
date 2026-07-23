using NUH_PORTAL.Models;

namespace NUH_PORTAL.Services.Interfaces
{
    // توليد JWT موحّد (بيستخدمه OTP و Auth)
    public interface ITokenService
    {
        string GenerateToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions);
    }
}
