namespace NUH_PORTAL.Models.Enums
{
    // نوع الإجراء اللي بيتعمل على الوحدة — بييجي من تذكرة إنجاز.
    public enum FacultyRequestType
    {
        NewService = 1,   // خدمة جديدة: الوحدة مالهاش حساب في الدومين ولازم يتعمل
        ChangeOccupant = 2, // تعديل: ساكن جديد مكان واحد — الحساب موجود
        StopService = 3   // إيقاف: الوحدة بقت فاضية والحساب يتعطّل
    }
}
