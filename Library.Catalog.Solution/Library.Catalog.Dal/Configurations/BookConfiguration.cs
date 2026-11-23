using Library.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Catalog.Dal.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("Books", t =>
        {
            t.HasCheckConstraint("CK_Books_Price", "[Price] > 0");
            t.HasCheckConstraint("CK_Books_AvailableCopies", "[AvailableCopies] >= 0");
        });

        builder.HasKey(b => b.BookId);
        builder.Property(b => b.Isbn).IsRequired().HasMaxLength(20);
        builder.Property(b => b.Title).IsRequired().HasMaxLength(300);
        builder.Property(b => b.Price).HasColumnType("decimal(18,2)");
        builder.Property(b => b.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(b => b.Isbn).IsUnique();   // природний ключ книги
        builder.HasIndex(b => b.Title);             // пошук і сортування за назвою

        // N:1 — видавець і категорія; без каскадного видалення
        builder.HasOne(b => b.Publisher).WithMany(p => p.Books)
            .HasForeignKey(b => b.PublisherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Category).WithMany(c => c.Books)
            .HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
