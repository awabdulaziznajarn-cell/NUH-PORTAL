namespace NUH_PORTAL.Core
{
    // صلاحية واحدة: الاسم المعروض + القيمة (اللي بتتخزّن كـ claim وتتحقّق منها الـ policy) + المجموعة + الوصف.
    public class ApplicationPermission
    {
        public string Name { get; }
        public string Value { get; }
        public string GroupName { get; }
        public string Description { get; }

        public ApplicationPermission(string name, string value, string groupName, string description)
        {
            Name = name;
            Value = value;
            GroupName = groupName;
            Description = description;
        }

        public override string ToString() => Value;
    }
}
