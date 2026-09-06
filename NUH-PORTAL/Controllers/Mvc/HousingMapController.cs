using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NUH_PORTAL.Controllers.Mvc
{
    // صفحة «خريطة مباني الإسكان الجامعي».
    // ⚠️ صلاحية مستقلّة (housing.occupancyMap) لا housing.view: الشاشة بتعرض
    //    أسماء الساكنين في كل غرفة - معلومة المشرف الميداني محتاجها ومحدش
    //    تاني، والشرح كامل في ApplicationPermissions.ViewOccupancyMap.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "housing.occupancyMap")]
    [Route("HousingMap")]
    public class HousingMapController : Controller
    {
        // GET /HousingMap
        [HttpGet("")]
        public IActionResult Index() => View();
    }
}
