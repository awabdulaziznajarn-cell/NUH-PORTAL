using Microsoft.EntityFrameworkCore.Storage;
using NUH_PORTAL.Core;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        protected readonly AppDbContext Context;
        protected int CurrentUserId;
        protected string? CurrentUserRole;
        protected HashSet<string> CurrentPermissions = new(StringComparer.OrdinalIgnoreCase);
        protected Gender? CurrentScopeGender;

        public UnitOfWork(AppDbContext context) => Context = context;

        public async Task<bool> SaveAsync() => await Context.SaveChangesAsync() > 0;
        public int GetCurrentUserId() => CurrentUserId;
        public string? GetCurrentUserRole() => CurrentUserRole;
        public bool HasPermission(string permission) => CurrentPermissions.Contains(permission);

        // صلاحية «الطلاب والطالبات معًا» بتتخطّى قسم الحساب تمامًا — فمدير النظام
        // والأمن السيبراني بياخدوها على أدوارهم ومايتقيّدوش بقسم.
        public Gender? GetGenderScope()
            => HasPermission(ApplicationPermissions.AllGenders.Value) ? null : CurrentScopeGender;
        public Task<IDbContextTransaction> BeginTransactionAsync() => Context.Database.BeginTransactionAsync();
    }
}
