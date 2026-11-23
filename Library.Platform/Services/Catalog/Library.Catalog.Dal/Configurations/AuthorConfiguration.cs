using Library.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Catalog.Dal.Configurations;

public class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder)
    {
        builder.ToTable("Authors");
        builder.HasKey(a => a.AuthorId);
        builder.Property(a => a.FullName).IsRequired().HasMaxLength(200);
        builder.HasIndex(a => a.FullName);
    }
}
