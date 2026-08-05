using Microsoft.EntityFrameworkCore.Storage;
using NUH_PORTAL.Data.Interfaces;

namespace NUH_PORTAL.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        protected readonly AppDbContext Context;
        protected int CurrentUserId;
        protected string? CurrentUserRole;
        protected HashSet<string> CurrentPermissions = new(StringComparer.OrdinalIgnoreCase);

        public UnitOfWork(AppDbContext context) => Context = context;

        public async Task<bool> SaveAsync() => await Context.SaveChangesAsync() > 0;
        public int GetCurrentUserId() => CurrentUserId;
        public string? GetCurrentUserRole() => CurrentUserRole;
        public bool HasPermission(string permission) => CurrentPermissions.Contains(permission);
        public Task<IDbContextTransaction> BeginTransactionAsync() => Context.Database.BeginTransactionAsync();
    }
}
