using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class StudentDeclarationConfiguration : IEntityTypeConfiguration<StudentDeclaration>
    {
        public void Configure(EntityTypeBuilder<StudentDeclaration> builder)
        {
            builder.ToTable("StudentDeclarations");
            builder.HasKey(d => d.Id);

            builder.Property(d => d.RequestId).HasColumnName("request_id");
            builder.Property(d => d.DeclarationAccepted).HasColumnName("declaration_accepted");
            builder.Property(d => d.PolicyAccepted).HasColumnName("policy_accepted");
            builder.Property(d => d.PolicyVersion).HasColumnName("policy_version");

            // ================================================================
            //  توثيق التعهّد — الشرح الكامل في Core/PledgeRules.cs.
            //
            //  ⚠️ التلاتة أعمدة **إضافة** بس: مفيش تعديل ولا حذف لعمود قايم.
            //     الجدول ده على قاعدة شغّالة، والإضافة وحدها هي اللي بتنفّذ
            //     على جدول فيه بيانات من غير ما تلمسها.
            // ================================================================

            // ⚠️ nvarchar(max) للنصّ عن قصد: عدد البنود بيتحدّد من شاشة القوائم
            //    المرجعية والبند نفسه لحد ألف حرف. أي سقف هنا معناه إن المدير
            //    يقدر يخلّي التعهّد يفشل بمجرّد إضافة بند.
            builder.Property(d => d.TermsText).HasColumnName("terms_text");

            // SHA-256 بالـ hex = ٦٤ خانة بالظبط، ثابتة مهما طال النصّ.
            builder.Property(d => d.TermsHash).HasColumnName("terms_hash").HasMaxLength(64);

            // الجملة المطلوبة أقصر من كده بكتير — السقف عشان خانة نصّ حرّة
            // جاية من العميل ما تتحوّلش لمكان تخزين.
            builder.Property(d => d.TypedConfirmation).HasColumnName("typed_confirmation").HasMaxLength(300);

            builder.Property(d => d.AcceptedDate).HasColumnName("accepted_date")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            builder.Property(d => d.IPAddress).HasColumnName("ip_address");
            builder.Property(d => d.UserAgent).HasColumnName("user_agent");

            builder.HasOne(d => d.Request)
                .WithMany()
                .HasForeignKey(d => d.RequestId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
