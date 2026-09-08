using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class HousingHistoryConfiguration : IEntityTypeConfiguration<HousingHistory>
    {
        public void Configure(EntityTypeBuilder<HousingHistory> builder)
        {
            builder.ToTable("HousingHistory");
            builder.HasKey(h => h.Id);

            builder.Property(h => h.StudentId).HasColumnName("student_id");
            builder.Property(h => h.StudentNumber).HasColumnName("student_number").HasMaxLength(50);
            builder.Property(h => h.Action).HasColumnName("action").HasMaxLength(20).IsRequired();
            builder.Property(h => h.Source).HasColumnName("source").HasMaxLength(24);

            builder.Property(h => h.FromBuilding).HasColumnName("from_building").HasMaxLength(50);
            builder.Property(h => h.FromFloor).HasColumnName("from_floor").HasMaxLength(10);
            builder.Property(h => h.FromApartment).HasColumnName("from_apartment").HasMaxLength(10);
            builder.Property(h => h.FromRoom).HasColumnName("from_room").HasMaxLength(10);

            builder.Property(h => h.ToBuilding).HasColumnName("to_building").HasMaxLength(50);
            builder.Property(h => h.ToFloor).HasColumnName("to_floor").HasMaxLength(10);
            builder.Property(h => h.ToApartment).HasColumnName("to_apartment").HasMaxLength(10);
            builder.Property(h => h.ToRoom).HasColumnName("to_room").HasMaxLength(10);

            builder.Property(h => h.Reason).HasColumnName("reason").HasMaxLength(400);
            builder.Property(h => h.TransferId).HasColumnName("transfer_id");
            builder.Property(h => h.CreatedBy).HasColumnName("created_by");

            // نفس معالجة التواريخ في بقية الجداول: القيمة المخزَّنة UTC،
            // والتحويل هنا يمنع قراءتها بوقت محلّي مجهول المنطقة.
            builder.Property(h => h.CreatedAt).HasColumnName("created_at")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            // ⚠️ Restrict لا Cascade على الطالب - بخلاف HousingTransfers:
            //    حذف صفّ الطالب يجب ألّا يمحو تاريخ سكنه. ولهذا كذلك الرقم
            //    الجامعي منسوخ نصًّا في الصفّ.
            builder.HasOne(h => h.Student)
                .WithMany()
                .HasForeignKey(h => h.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(h => h.CreatedByUser)
                .WithMany()
                .HasForeignKey(h => h.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(h => h.Transfer)
                .WithMany()
                .HasForeignKey(h => h.TransferId)
                .OnDelete(DeleteBehavior.SetNull);

            // ⚠️ الفهرسان هما سببا وجود الجدول: الأول لتاريخ الطالب، والثاني
            //    لتاريخ الغرفة - وهو الاستعلام الذي يُفتح من الخريطة مع كل
            //    غرفة، فبغير فهرسه يمسح الجدول كلّه في كل نقرة.
            builder.HasIndex(h => new { h.StudentId, h.CreatedAt })
                   .HasDatabaseName("IX_HousingHistory_Student");

            builder.HasIndex(h => new { h.ToBuilding, h.ToFloor, h.ToApartment, h.ToRoom })
                   .HasDatabaseName("IX_HousingHistory_Room");
        }
    }
}
