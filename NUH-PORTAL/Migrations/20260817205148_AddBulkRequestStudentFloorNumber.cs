using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NUH_PORTAL.Migrations
{
    /// <inheritdoc />
    public partial class AddBulkRequestStudentFloorNumber : Migration
    {
        // ====================================================================
        //  الترحيل ده اتولّد أوتوماتيك وكان فيه ٢٣ عملية، مش واحدة:
        //  خمسة أعمدة في Users، عمودين في Requests، جدولَي سكن أعضاء هيئة
        //  التدريس، و ١٣ فهرس — كلهم موجودين في قاعدة البيانات فعلًا وشغّالين.
        //
        //  ⚠️ السبب إن التغييرات دي اتضافت لقاعدة البيانات بـ SQL بالإيد بدل
        //     ما تتعمل بترحيل، فصورة الموديل عند EF فضلت ورا الواقع. وأول
        //     ترحيل جديد قارن الكود بالصورة القديمة فاعتبر كل ده «ناقص».
        //     أول أمر ‎database update‎ وقع على:
        //         Column name 'auth_source' in table 'Users' is specified more than once
        //
        //  ⚠️ اتأكدنا من كل بند بـ COL_LENGTH و OBJECT_ID على قاعدة الإنتاج قبل
        //     الحذف: الكل موجود ما عدا FloorNumber. فالباقي اتشال من هنا،
        //     والعمود الوحيد الناقص فضل.
        //
        //     وملف الـ Designer واللقطة (Snapshot) اتسابوا زي ما اتولّدوا عن قصد:
        //     دول بقوا بيوصفوا الواقع صح لأول مرة، فالفارق بين الكود وقاعدة
        //     البيانات اتقفل من هنا ورايح.
        // ====================================================================

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FloorNumber",
                table: "BulkRequestStudents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FloorNumber",
                table: "BulkRequestStudents");
        }
    }
}
