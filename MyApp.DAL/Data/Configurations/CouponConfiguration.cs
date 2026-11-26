using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Core.Entities;

namespace MyApp.DAL.Data.Configurations;

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Coupons_Value", "[Value] > 0");
            t.HasCheckConstraint("CK_Coupons_UsedCount", "[UsedCount] >= 0");
        });

        builder.Property(c => c.Code).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(c => c.Code).IsUnique();
    }
}
