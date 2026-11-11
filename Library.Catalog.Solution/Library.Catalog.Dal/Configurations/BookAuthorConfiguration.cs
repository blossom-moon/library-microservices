using Library.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Catalog.Dal.Configurations;

public class BookAuthorConfiguration : IEntityTypeConfiguration<BookAuthor>
{
    public void Configure(EntityTypeBuilder<BookAuthor> builder)
    {
        builder.ToTable("BookAuthors");

        builder.HasKey(ba => new { ba.BookId, ba.AuthorId });   // складений ключ
        builder.Property(ba => ba.Role).IsRequired().HasMaxLength(50);
        builder.Property(ba => ba.AddedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(ba => ba.Book).WithMany(b => b.BookAuthors)
            .HasForeignKey(ba => ba.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ba => ba.Author).WithMany(a => a.BookAuthors)
            .HasForeignKey(ba => ba.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ba => ba.AuthorId);                       // «усі книги автора»
        builder.HasIndex(ba => new { ba.AuthorId, ba.AddedAt });   // «книги автора, додані після дати»
    }
}
