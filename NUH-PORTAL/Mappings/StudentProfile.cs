using AutoMapper;
using NUH_PORTAL.DTOs.Students;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Mappings
{
    // خرائط التحويل بين Student والـ DTOs (بيتسكان تلقائيًا عبر AddAutoMapper)
    public class StudentProfile : Profile
    {
        public StudentProfile()
        {
            CreateMap<Student, StudentDto>();
            CreateMap<StudentCreateDto, Student>();
            // التعديل بيتعمل يدويًا (field-diff + audit change logs) فمش محتاج map
        }
    }
}
