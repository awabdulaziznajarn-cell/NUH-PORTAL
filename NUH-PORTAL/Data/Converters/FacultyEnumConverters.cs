using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Data.Converters
{
    // ⚠️ الـ enums بتتخزّن نص مش رقم — زي GenderConverter بالظبط. السبب إن
    //    الاستعلام المباشر على القاعدة (وهو بيحصل كتير وقت التشخيص) يبقى مقروء:
    //    'out_of_service' تقول كل حاجة، و 2 ما بتقولش حاجة من غير ما تفتح الكود.

    public class FacultyUnitTypeConverter : ValueConverter<FacultyUnitType, string>
    {
        public FacultyUnitTypeConverter() : base(
            v => v == FacultyUnitType.Villa ? "villa" : "tower",
            s => s == "villa" ? FacultyUnitType.Villa : FacultyUnitType.Tower)
        { }
    }

    public class FacultyUnitStatusConverter : ValueConverter<FacultyUnitStatus, string>
    {
        public FacultyUnitStatusConverter() : base(
            v => v == FacultyUnitStatus.OutOfService ? "out_of_service"
               : v == FacultyUnitStatus.NotExists ? "not_exists"
               : "active",
            s => s == "out_of_service" ? FacultyUnitStatus.OutOfService
               : s == "not_exists" ? FacultyUnitStatus.NotExists
               : FacultyUnitStatus.Active)
        { }
    }

    public class FacultyUnitSyncStateConverter : ValueConverter<FacultyUnitSyncState, string>
    {
        public FacultyUnitSyncStateConverter() : base(
            v => v == FacultyUnitSyncState.Pending ? "pending"
               : v == FacultyUnitSyncState.Failed ? "failed"
               : "synced",
            s => s == "pending" ? FacultyUnitSyncState.Pending
               : s == "failed" ? FacultyUnitSyncState.Failed
               : FacultyUnitSyncState.Synced)
        { }
    }

    public class OccupancyEndReasonConverter : ValueConverter<OccupancyEndReason, string>
    {
        public OccupancyEndReasonConverter() : base(
            v => v == OccupancyEndReason.Transferred ? "transferred"
               : v == OccupancyEndReason.LeftPermanently ? "left_permanently"
               : v == OccupancyEndReason.Other ? "other"
               : "contract_ended",
            s => s == "transferred" ? OccupancyEndReason.Transferred
               : s == "left_permanently" ? OccupancyEndReason.LeftPermanently
               : s == "other" ? OccupancyEndReason.Other
               : OccupancyEndReason.ContractEnded)
        { }
    }
}
