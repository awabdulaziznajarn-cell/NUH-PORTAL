using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Notifications;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    public class NotificationService : AppServiceBase, INotificationService
    {
        private readonly IRepository<Notification> _notifications;

        public NotificationService(IRepository<Notification> notifications, IUnitOfWork unitOfWork, IMapper mapper)
            : base(unitOfWork, mapper)
        {
            _notifications = notifications;
        }

        // ⚠️ تقسيم الطلاب/الطالبات بيتقرا من الطلب المرتبط، مش من عمود جديد على
        //    الإشعار. الإشعار مربوط بالطلب بـ FK، والطلب عليه جنس الطالب أصلًا.
        //    لو خزّنّاه مرتين، أول ما جنس الطالب يتصلّح يبقى عندنا إشعار بيقول
        //    حاجة والطلب بيقول حاجة تانية — وده باق مايبانش غير بعد شهور.
        //    الإشعار اللي مش مربوط بطلب بيفضل ظاهر للكل: مانخفيش حاجة من الكل.
        private IQueryable<Notification> ScopeToGender(IQueryable<Notification> query)
        {
            var scope = UnitOfWork.GetGenderScope();
            return scope == null
                ? query
                : query.Where(n => n.Request == null || n.Request.StudentGender == null || n.Request.StudentGender == scope);
        }

        public async Task<List<NotificationDto>> GetNotificationsAsync(string? role)
        {
            var query = ScopeToGender(_notifications.Query().AsNoTracking());
            if (!string.IsNullOrEmpty(role))
                query = query.Where(n => n.recipient_role == role);

            var list = await query
                .OrderByDescending(n => n.sent_at)
                .Take(50)
                .ToListAsync();

            return Mapper.Map<List<NotificationDto>>(list);
        }

        public async Task<int> GetUnreadCountAsync(string? role)
        {
            // العدّاد لازم يطابق القائمة بالظبط، وإلا الجرس يقول ٣ والقائمة تفتح فاضية
            var query = ScopeToGender(_notifications.Query().AsNoTracking());
            if (!string.IsNullOrEmpty(role))
                query = query.Where(n => n.recipient_role == role);

            return await query.CountAsync(n => n.status == NotificationStatus.pending);
        }

        // ⚠️ القراءة فوق بتفلتر بـ recipient_role، لكن الكتابة كانت بتعلّم أي إشعار
        //    برقمه مهما كان صاحبه. يعني أي مستخدم داخل يقدر يبعت [1,2,3...]
        //    ويقفل إشعارات أدوار تانية فتختفي من عدّاد الغير مقروء عندهم.
        //    الدور بيجي من هوية المستخدم مش من الطلب — زي القراءة بالظبط.
        public async Task MarkAsReadAsync(List<int> ids, string? role)
        {
            if (ids == null || ids.Count == 0)
                throw new UserFriendlyException("قائمة الإشعارات مطلوبة", 400);

            var scope = UnitOfWork.GetGenderScope();
            var notifications = (string.IsNullOrEmpty(role)
                ? await _notifications.FindAllAsync(n => ids.Contains(n.Id))
                : await _notifications.FindAllAsync(n => ids.Contains(n.Id) && n.recipient_role == role))
                .ToList();

            // نفس منطق القراءة: مايعلّمش كمقروء إلا اللي هو أصلاً بيشوفه
            if (scope != null)
            {
                var visible = await ScopeToGender(_notifications.Query().AsNoTracking())
                    .Where(n => ids.Contains(n.Id)).Select(n => n.Id).ToListAsync();
                notifications = notifications.Where(n => visible.Contains(n.Id)).ToList();
            }

            foreach (var n in notifications)
                n.status = NotificationStatus.read;

            await UnitOfWork.SaveAsync();
        }
    }
}
