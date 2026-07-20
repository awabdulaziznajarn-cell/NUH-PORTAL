namespace NUH_PORTAL.Models
{
    public class ActiveDirectoryConfig
    {
        // لو false → بنتخطّى الـ AD ونروح للـ local fallback (مفيد في التطوير من غير AD)
        public bool Enabled { get; set; } = true;
        public string Domain { get; set; } = string.Empty;
        public string DomainController { get; set; } = string.Empty;
        public int Port { get; set; } = 636;
        public bool ValidateCertificate { get; set; } = true;
        public List<AdRoleMapping> RoleMappings { get; set; } = new();
    }

    public class AdRoleMapping
    {
        public string AdGroup { get; set; } = string.Empty;
        public string ApplicationRole { get; set; } = "User";
    }
}
