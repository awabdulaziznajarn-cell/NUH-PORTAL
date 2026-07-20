namespace NUH_PORTAL.Models
{
    public class ADConfiguration
    {
        public int Id { get; set; }
        public string? ConfigKey { get; set; }
        public string? ConfigValue { get; set; }
        public string? Description { get; set; }
        public int? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public User? UpdatedByUser { get; set; }
    }

    public static class ADConfigurationKeys
    {
        public const string StudentOuPath = "student_ou_path";
        public const string HousingOuPath = "housing_ou_path";
        public const string DisabledOuPath = "disabled_ou_path";
        public const string DefaultStudentGroupDn = "default_student_group_dn";
        public const string HousingUserGroupDn = "housing_user_group_dn";
        public const string ExtensionAttributePhone = "ext_attr_phone";
        public const string ExtensionAttributeBuilding = "ext_attr_building";
        public const string ExtensionAttributeRoom = "ext_attr_room";
        public const string ExtensionAttributeApartment = "ext_attr_apartment";
        public const string ExtensionAttributeCollege = "ext_attr_college";
        public const string ExtensionAttributeDepartment = "ext_attr_department";
        public const string ExtensionAttributeAcademicLevel = "ext_attr_academic_level";
    }
}
