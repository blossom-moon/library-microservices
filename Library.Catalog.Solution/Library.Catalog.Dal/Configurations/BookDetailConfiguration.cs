using Library.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Catalog.Dal.Configurations;

public class BookDetailConfiguration : IEntityTypeConfiguration<BookDetail>
{
    public void Configure(EntityTypeBuilder<BookDetail> builder)
    {
        builder.ToTable("BookDetails", t => t.HasCheckConstraint("CK_BookDetails_PageCount", "[PageCount] > 0"));

        builder.HasKey(d => d.BookId);   // 1:1 — PK є водночас FK
        builder.Property(d => d.Annotation).IsRequired().HasMaxLength(2000);
        builder.Property(d => d.Language).IsRequired().HasMaxLength(10);

        builder.HasOne(d => d.Book).WithOne(b => b.Detail)
            .HasForeignKey<BookDetail>(d => d.BookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
