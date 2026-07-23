using Microsoft.IdentityModel.Tokens;
using NUH_PORTAL.Core;
using NUH_PORTAL.Models;
using NUH_PORTAL.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace NUH_PORTAL.Services
{
    // توليد الـ JWT في مكان واحد. الأدوار بتتمرّر من برّه (بتتحمّل عبر UserManager).
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _config;

        public TokenService(IConfiguration config) => _config = config;

        public string GenerateToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
            };

            // دور لكل ما ينتمي له المستخدم (multi-role زي الـ permit). لو مفيش، "user" افتراضي.
            var roleList = (roles ?? Enumerable.Empty<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.ToLowerInvariant())
                .ToList();
            if (roleList.Count == 0) roleList.Add("user");
            foreach (var r in roleList)
                claims.Add(new Claim(ClaimTypes.Role, r));

            // صلاحيات المستخدم (permission claims) — الـ policies بتتحقّق منها
            foreach (var p in (permissions ?? Enumerable.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct())
                claims.Add(new Claim(ClaimConstants.Permission, p));

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
