namespace NUH_PORTAL.DTOs.Workflow
{
    public class WorkflowActionRequest
    {
        public string? Notes { get; set; }

        // مفاتيح الخانات اللي المراجع طالب من الطالب يصلّحها.
        // فاضية = كل الخانات مفتوحة له.
        public List<string>? Fields { get; set; }
    }
}
