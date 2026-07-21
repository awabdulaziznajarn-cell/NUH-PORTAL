namespace NUH_PORTAL.DTOs.Housing
{
    // تفاصيل حساب الـ AD (نفس الحقول اللي كانت بترجع من الكنترولر القديم)
    public class AdAccountDetailsDto
    {
        public string? DistinguishedName { get; set; }
        public string? SamAccountName { get; set; }
        public string? UserPrincipalName { get; set; }
        public string? DisplayName { get; set; }
        public bool AccountEnabled { get; set; }
        public int UserAccountControl { get; set; }
        public string? Department { get; set; }
        public string? Description { get; set; }
        public List<string>? MemberOf { get; set; }
        public string? ExtensionAttribute1 { get; set; }
    }
}
