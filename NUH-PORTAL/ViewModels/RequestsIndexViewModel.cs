using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.DTOs.Requests;

namespace NUH_PORTAL.ViewModels
{
    // موديل صفحة إدارة الطلبات (MVC) — العدادات + أول صفحة جاهزين من السيرفر
    public class RequestsIndexViewModel
    {
        public RequestStatsDto Stats { get; set; } = new();
        public QueryResult<RequestDto> Page { get; set; } = new();
    }
}
