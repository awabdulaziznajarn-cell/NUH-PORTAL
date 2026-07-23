namespace NUH_PORTAL.Models.Enums
{
    // حالة الإشعار — بتتخزّن كنص زي ما هي (pending/read) بدون تغيير عمود.
    // الجرس بيعدّ pending كغير مقروء، وMarkAsRead بيحوّلها read.
    public enum NotificationStatus
    {
        pending,
        read
    }
}
