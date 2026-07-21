using AutoMapper;
using NUH_PORTAL.DTOs.Bulk;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Mappings
{
    public class BulkProfile : Profile
    {
        public BulkProfile()
        {
            CreateMap<BulkRequest, BulkRequestDto>();
            CreateMap<BulkRequest, BulkRequestDetailsDto>();
            CreateMap<BulkRequestStudent, BulkRequestStudentDto>();
        }
    }
}
