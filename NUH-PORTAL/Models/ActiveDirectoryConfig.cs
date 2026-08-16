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

        // ⚠️ RoleMappings و AdRoleMapping اتشالوا. كانوا بيربطوا مجموعات
        //    Housing_Admin / Housing_Cyber / Housing_Supervisor / Housing_Users
        //    بأدوار التطبيق، والمجموعات دي مش متعمولة على الدومين أصلًا فكانت
        //    النتيجة دايمًا "user". ومع كده كانت بتمسح الدور اللي المسؤول حدّده
        //    من شاشة إدارة المستخدمين مع كل تسجيل دخول.
        //    الدور دلوقتي مصدره الشاشة وبس — والدليل للمصادقة والبيانات الشخصية.
    }
}
