using NUH_PORTAL.DTOs.Lookups;

namespace NUH_PORTAL.Services.Interfaces
{
    // إدارة القوائم المرجعية (CRUD). category ∈ college | department | building | academiclevel
    public interface ILookupAdminService
    {
        Task<List<LookupDto>> ListAsync(string category);
        Task<LookupDto> SaveAsync(string category, int? id, LookupSaveDto dto);
        Task DeleteAsync(string category, int id);

        Task<List<TermDto>> ListTermsAsync();
        Task<TermDto> SaveTermAsync(int? id, TermSaveDto dto);
        Task DeleteTermAsync(int id);
    }
}
