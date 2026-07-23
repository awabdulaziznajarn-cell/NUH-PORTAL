namespace NUH_PORTAL.DTOs.Roles
{
    // صلاحية واحدة في الكتالوج (القيمة اللي بتتخزّن + الاسم المعروض + الوصف).
    public class PermissionItemDto
    {
        public string value { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
    }

    // مجموعة صلاحيات (زي: المستخدمون، السجلّات...) مع بنودها.
    public class PermissionGroupDto
    {
        public string groupName { get; set; } = string.Empty;
        public List<PermissionItemDto> permissions { get; set; } = new();
    }
}
