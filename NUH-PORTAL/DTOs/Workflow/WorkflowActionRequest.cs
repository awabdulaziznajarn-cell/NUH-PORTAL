namespace NUH_PORTAL.DTOs.Workflow
{
    public class WorkflowActionRequest
    {
        public string? Notes { get; set; }

        // مفاتيح الخانات اللي المراجع طالب من الطالب يصلّحها.
        // فاضية = كل الخانات مفتوحة له.
        public List<string>? Fields { get; set; }

        // ====================================================================
        //  تسكين الطالب - بيتبعت مع اعتماد **إدارة الإسكان وحدها**.
        //
        //  ⚠️ الطالب مابقاش يختار سكنه: هو مايعرفش هيتسكّن فين أصلًا، والمشرف
        //     هو صاحب القرار. فالخانات دي فاضية في كل المراحل التانية،
        //     ومطلوبة في مرحلة الإسكان - والاعتماد مابيعديش من غيرها.
        // ====================================================================
        public string? HousingBuilding { get; set; }
        public string? FloorNumber { get; set; }
        public string? ApartmentNumber { get; set; }
        public string? RoomNumber { get; set; }
    }
}
