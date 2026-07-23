using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Data.Converters
{
    // يخزّن RequestType كنص (نفس القيم الحالية). دفاعي عند القراءة: أي قيمة غير معروفة → self_registration بدل ما يكسر.
    public class RequestTypeConverter : ValueConverter<RequestType, string>
    {
        public RequestTypeConverter() : base(
            v => v.ToString(),
            s => s == "bulk_req" ? RequestType.bulk_req
               : s == "housing" ? RequestType.housing
               : RequestType.self_registration)
        { }
    }
}
