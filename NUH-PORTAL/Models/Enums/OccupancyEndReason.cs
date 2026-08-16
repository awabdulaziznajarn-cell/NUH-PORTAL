namespace NUH_PORTAL.Models.Enums
{
    // سبب انتهاء إشغال الوحدة. بيتسجّل مع صف الإشغال وقت قفله ومابيتغيّرش بعدها.
    public enum OccupancyEndReason
    {
        ContractEnded = 1,  // انتهاء التعاقد
        Transferred = 2,    // نقل لوحدة أخرى
        LeftPermanently = 3,// مغادرة نهائية
        Other = 4           // سبب آخر — بيتكتب في EndReasonNote
    }
}
