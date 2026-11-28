using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Core.Entities;

namespace MyApp.DAL.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Orders_Subtotal", "[Subtotal] >= 0");
            t.HasCheckConstraint("CK_Orders_DiscountAmount", "[DiscountAmount] >= 0");
            t.HasCheckConstraint("CK_Orders_Total", "[Total] >= 0");
        });

        builder.Property(o => o.OrderNumber).HasMaxLength(30).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsConcurrencyToken();
        builder.Property(o => o.PaymentStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.CouponCode).HasMaxLength(50);

        builder.ComplexProperty(o => o.ShippingAddress, sa =>
        {
            sa.Property(a => a.RecipientName).HasMaxLength(100).IsRequired();
            sa.Property(a => a.PhoneNumber).HasMaxLength(30).IsRequired();
            sa.Property(a => a.Line1).HasMaxLength(200).IsRequired();
            sa.Property(a => a.Line2).HasMaxLength(200);
            sa.Property(a => a.City).HasMaxLength(100).IsRequired();
            sa.Property(a => a.State).HasMaxLength(100);
            sa.Property(a => a.PostalCode).HasMaxLength(20).IsRequired();
            sa.Property(a => a.Country).HasMaxLength(100).IsRequired();
        });

        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.CreatedAt);

        builder.HasOne(o => o.User)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Coupon)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CouponId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
