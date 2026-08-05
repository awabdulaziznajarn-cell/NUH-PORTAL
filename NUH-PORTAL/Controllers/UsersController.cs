using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUH_PORTAL.DTOs.Users;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Controllers
{
    // إدارة المستخدمين — كل نقطة محميّة بصلاحية (permission policy) بدل دور ثابت.
    // (الأدمن عنده كل الصلاحيات فبيشتغل عادي؛ ممكن ندي دور تاني صلاحية users.view بس مثلًا.)
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _service;

        public UsersController(IUserService service) => _service = service;

        // GET api/Users?studentsOnly=false
        // الشاشة تبويبين: الموظفون (الافتراضي) وحسابات دخول الطلاب.
        // حساب الطالب بيتولّد تلقائيًا مع كل تحقق برمز جوال، فعدده بيكبر مع كل
        // طالب بيقدّم — وخلطه بالموظفين بيضيّع الشاشة.
        [HttpGet]
        [Authorize(Policy = "users.view")]
        public async Task<IActionResult> GetUsers([FromQuery] bool studentsOnly = false)
            => Ok(await _service.GetUsersAsync(studentsOnly));

        // GET api/Users/counts — أرقام التبويبات
        [HttpGet("counts")]
        [Authorize(Policy = "users.view")]
        public async Task<IActionResult> GetCounts()
            => Ok(await _service.GetCountsAsync());

        // GET api/Users/roles — قائمة الأدوار (dropdown)
        [HttpGet("roles")]
        [Authorize(Policy = "users.view")]
        public async Task<IActionResult> GetRoles()
            => Ok(await _service.GetRolesAsync());

        // GET api/Users/{id}
        [HttpGet("{id:int}")]
        [Authorize(Policy = "users.view")]
        public async Task<IActionResult> GetUser(int id)
            => Ok(await _service.GetUserDetailAsync(id));

        // POST api/Users
        [HttpPost]
        [Authorize(Policy = "users.manage")]
        public async Task<IActionResult> Create([FromBody] UserCreateDto dto)
            => Ok(await _service.CreateAsync(dto));

        // PUT api/Users/{id}
        [HttpPut("{id:int}")]
        [Authorize(Policy = "users.manage")]
        public async Task<IActionResult> Update(int id, [FromBody] UserUpdateDto dto)
            => Ok(await _service.UpdateAsync(id, dto));

        // POST api/Users/{id}/activate
        [HttpPost("{id:int}/activate")]
        [Authorize(Policy = "users.manage")]
        public async Task<IActionResult> Activate(int id)
        {
            await _service.SetActiveAsync(id, true);
            return Ok(new { active = true });
        }

        // POST api/Users/{id}/deactivate
        [HttpPost("{id:int}/deactivate")]
        [Authorize(Policy = "users.manage")]
        public async Task<IActionResult> Deactivate(int id)
        {
            await _service.SetActiveAsync(id, false);
            return Ok(new { active = false });
        }

        // POST api/Users/{id}/role — إسناد دور (صلاحية منفصلة)
        [HttpPost("{id:int}/role")]
        [Authorize(Policy = "roles.assign")]
        public async Task<IActionResult> AssignRole(int id, [FromBody] AssignRoleDto dto)
        {
            await _service.AssignRoleAsync(id, dto.role ?? "");
            return Ok(new { assigned = true });
        }

        // GET api/Users/ldap-search?q= — بحث في الدليل
        [HttpGet("ldap-search")]
        [Authorize(Policy = "users.addFromLdap")]
        public async Task<IActionResult> LdapSearch([FromQuery] string q)
            => Ok(await _service.SearchLdapAsync(q ?? ""));

        // POST api/Users/from-ldap — إضافة مستخدم من الدليل
        [HttpPost("from-ldap")]
        [Authorize(Policy = "users.addFromLdap")]
        public async Task<IActionResult> AddFromLdap([FromBody] LdapAddUserDto dto)
            => Ok(await _service.AddFromLdapAsync(dto));
    }
}
