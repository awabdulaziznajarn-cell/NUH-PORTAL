using AutoMapper;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Users;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    public class UserService : AppServiceBase, IUserService
    {
        private readonly IRepository<User> _users;

        public UserService(IRepository<User> users, IUnitOfWork unitOfWork, IMapper mapper)
            : base(unitOfWork, mapper)
        {
            _users = users;
        }

        public async Task<List<UserListItemDto>> GetUsersAsync()
        {
            // Projection مباشر في الاستعلام (أخف من تحميل الـ entity كله وفيه أمان: مفيش password_hash)
            return await _users.Query().AsNoTracking()
                .Select(u => new UserListItemDto
                {
                    Id = u.Id,
                    username = u.username,
                    full_name = u.full_name,
                    email = u.email,
                    role = u.role,
                    created_at = u.created_at,
                    is_active = u.is_active
                })
                .ToListAsync();
        }

        public async Task<UserListItemDto> GetUserAsync(int id)
        {
            var user = await _users.Query().AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new UserListItemDto
                {
                    Id = u.Id,
                    username = u.username,
                    full_name = u.full_name,
                    email = u.email,
                    role = u.role,
                    created_at = u.created_at,
                    is_active = u.is_active
                })
                .FirstOrDefaultAsync();

            return user ?? throw UserFriendlyException.NotFound("المستخدم غير موجود");
        }
    }
}
