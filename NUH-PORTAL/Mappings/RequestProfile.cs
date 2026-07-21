using AutoMapper;
using NUH_PORTAL.DTOs.Requests;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Mappings
{
    public class RequestProfile : Profile
    {
        public RequestProfile()
        {
            CreateMap<Request, RequestDto>();           // Student بيتحول تلقائيًا عبر Student→StudentDto
            CreateMap<RequestCreateDto, Request>();
        }
    }
}
