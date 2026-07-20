using NUH_PORTAL.Data.Interfaces;

namespace NUH_PORTAL.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        protected readonly AppDbContext Context;
        protected int CurrentUserId;

        public UnitOfWork(AppDbContext context) => Context = context;

        public async Task<bool> SaveAsync() => await Context.SaveChangesAsync() > 0;
        public int GetCurrentUserId() => CurrentUserId;
    }
}
