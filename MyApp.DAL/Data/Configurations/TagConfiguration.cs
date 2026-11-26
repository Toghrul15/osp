using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Core.Entities;

namespace MyApp.DAL.Data.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Slug).HasMaxLength(60).IsRequired();

        builder.HasIndex(t => t.Slug).IsUnique();
    }
}
