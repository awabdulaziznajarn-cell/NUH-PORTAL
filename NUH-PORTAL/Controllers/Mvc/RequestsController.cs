using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.Services.Interfaces;
using NUH_PORTAL.ViewModels;

namespace NUH_PORTAL.Controllers.Mvc
{
    // صفحة إدارة الطلبات (MVC) — العدادات وأول صفحة بيترندروا من السيرفر،
    // والتبويبات/الترقيم/الأكشنات بتشتغل على نفس الـ API القديم بالظبط
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    [Route("Requests")]
    public class RequestsController : Controller
    {
        private readonly IRequestService _requests;

        public RequestsController(IRequestService requests) => _requests = requests;

        // GET /Requests
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var vm = new RequestsIndexViewModel
            {
                Stats = await _requests.GetStatsAsync(),
                Page = await _requests.GetPagedAsync(
                    new QueryParams { Page = 1, PageSize = 20 }, status: null, requestType: null)
            };
            return View(vm);
        }
    }
}
