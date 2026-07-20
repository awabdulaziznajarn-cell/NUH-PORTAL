namespace NUH_PORTAL.Core.Exceptions
{
    // استثناء برسالة صالحة للعرض للمستخدم + status code
    public class UserFriendlyException : Exception
    {
        public int StatusCode { get; }
        public UserFriendlyException(string message, int statusCode = 400) : base(message) => StatusCode = statusCode;
        public static UserFriendlyException NotFound(string message = "السجل غير موجود") => new(message, 404);
        public static UserFriendlyException Forbidden(string message = "غير مصرّح لك بهذا الإجراء") => new(message, 403);
    }
}
