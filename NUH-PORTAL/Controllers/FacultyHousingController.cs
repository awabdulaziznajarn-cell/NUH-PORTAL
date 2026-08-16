using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.DTOs.FacultyHousing;
using NUH_PORTAL.Services;

namespace NUH_PORTAL.Controllers
{
    // سكن أعضاء هيئة التدريس — الوحدات وسجل الإشغال والاستيراد من الدومين.
    //
    // ⚠️ كل نقطة محميّة بصلاحيتها المنفصلة. الكتابة في الدومين والاستيراد
    //    ليهم صلاحيات لوحدهم مش داخلين تحت «إدارة»، لأن أثرهم بره النظام:
    //    مين يقدر يسجّل تسليم وحدة مش بالضرورة هو اللي يعدّل الدومين.
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class FacultyHousingController : ControllerBase
    {
        private readonly FacultyHousingService _service;

        public FacultyHousingController(FacultyHousingService service) => _service = service;

        // GET api/FacultyHousing/units
        [HttpGet("units")]
        [Authorize(Policy = "facultyHousing.view")]
        public async Task<IActionResult> GetUnits(
            [FromQuery] string? type = null,
            [FromQuery] string? status = null,
            [FromQuery] string? search = null,
            [FromQuery] bool onlyDeviations = false,
            [FromQuery] bool onlyNeedsConfirm = false,
            [FromQuery] int? tower = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
            => Ok(await _service.GetUnitsAsync(type, status, search, onlyDeviations, onlyNeedsConfirm, tower, page, pageSize));

        // GET api/FacultyHousing/units/12 — بيانات الوحدة + كل سجل الإشغال
        [HttpGet("units/{id:int}")]
        [Authorize(Policy = "facultyHousing.view")]
        public async Task<IActionResult> GetUnit(int id)
            => Ok(await _service.GetUnitDetailAsync(id));

        // GET api/FacultyHousing/units/12/ad-diff
        // ⚠️ بيقرا الحساب من الدومين ويقارنه بالمسجّل عندنا. المستخدم لازم يشوف
        //    اللي هيتغيّر قبل ما يوافق - الكتابة في الدومين مالهاش زرار تراجع.
        [HttpGet("units/{id:int}/ad-diff")]
        [Authorize(Policy = "facultyHousing.view")]
        public async Task<IActionResult> AdDiff(int id)
            => Ok(await _service.BuildAdDiffAsync(id));

        // POST api/FacultyHousing/units/12/handover
        [HttpPost("units/{id:int}/handover")]
        [Authorize(Policy = "facultyHousing.manage")]
        public async Task<IActionResult> Handover(int id, [FromBody] HandoverRequestDto dto)
        {
            // ⚠️ رقم الوحدة من المسار هو المعتمد، مش اللي في جسم الطلب. لو
            //    اتنينهم موجودين ومختلفين، اللي في الجسم يقدر ينفّذ الإجراء على
            //    وحدة تانية غير اللي الصلاحية اتفحصت عليها.
            dto.UnitId = id;
            return Ok(await _service.HandoverAsync(dto));
        }

        // PUT api/FacultyHousing/units/12/occupant — تصحيح بيانات الساكن الحالي
        // ⚠️ PUT لا POST: تعديل مورد قائم في مكانه، لا إنشاء سجل جديد. والفرق
        //    ليس شكليًا هنا - هو نفسه الفرق بين التصحيح والتسليم في السجل.
        [HttpPut("units/{id:int}/occupant")]
        [Authorize(Policy = "facultyHousing.manage")]
        public async Task<IActionResult> UpdateOccupant(int id, [FromBody] HandoverRequestDto dto)
        {
            dto.UnitId = id;
            return Ok(await _service.UpdateOccupantAsync(id, dto));
        }

        // POST api/FacultyHousing/units/12/push-ad — إعادة محاولة الكتابة
        // ⚠️ صلاحية منفصلة: مين يسجّل التسليم مش بالضرورة هو اللي يعدّل الدومين.
        [HttpPost("units/{id:int}/push-ad")]
        [Authorize(Policy = "facultyHousing.syncAd")]
        public async Task<IActionResult> PushAd(int id)
            => Ok(await _service.PushToAdAsync(id));

        // GET api/FacultyHousing/access-check
        // ⚠️ فحص من غير أي كتابة: بيقرا allowedAttributesEffective اللي الدومين
        //    بيحسبها لحساب الخدمة. بيتنادى قبل أول استيراد عشان الفشل يبان
        //    كرسالة واضحة بدل ما الاستيراد يقف في النص.
        [HttpGet("access-check")]
        [Authorize(Policy = "facultyHousing.import")]
        public async Task<IActionResult> AccessCheck()
            => Ok(await _service.CheckAccessAsync());

        // GET api/FacultyHousing/import/preview — قراءة بس، مابتغيّرش حاجة
        [HttpGet("import/preview")]
        [Authorize(Policy = "facultyHousing.import")]
        public async Task<IActionResult> ImportPreview()
            => Ok(await _service.PreviewImportAsync());

        // POST api/FacultyHousing/import/apply
        // ⚠️ POST مش GET: بيكتب في قاعدة البيانات. GET بيتعمله cache وبيتنادى
        //    من prefetch المتصفح، فاستيراد على GET ممكن يتنفّذ من غير ما حد يضغط.
        [HttpPost("import/apply")]
        [Authorize(Policy = "facultyHousing.import")]
        public async Task<IActionResult> ImportApply()
            => Ok(await _service.ApplyImportAsync());
    }
}
