using AutoMapper;
using NUH_PORTAL.DTOs.StudentStatus;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Mappings
{
    public class StudentStatusProfile : Profile
    {
        public StudentStatusProfile()
        {
            CreateMap<StudentStatusAction, StudentStatusActionDto>();
        }
    }
}
