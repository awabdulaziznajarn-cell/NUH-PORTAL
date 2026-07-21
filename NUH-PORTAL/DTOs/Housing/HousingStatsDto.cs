namespace NUH_PORTAL.DTOs.Housing
{
    // نفس مفاتيح الإحصائيات القديمة بالحرف
    public class HousingStatsDto
    {
        public int with_accounts { get; set; }
        public int enabled { get; set; }
        public int disabled { get; set; }
        public int unknown_status { get; set; }
        public int synced_last_24h { get; set; }
        public int not_synced { get; set; }
        public int total_students { get; set; }
        public int with_username { get; set; }
        public int without_username { get; set; }
    }
}
