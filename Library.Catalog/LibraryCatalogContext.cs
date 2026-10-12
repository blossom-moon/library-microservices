using Microsoft.EntityFrameworkCore;

namespace Library.Catalog;

public class LibraryCatalogContext : DbContext
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Publisher> Publishers => Set<Publisher>();
    public DbSet<BookDetail> BookDetails => Set<BookDetail>();
    public DbSet<BookAuthor> BookAuthors => Set<BookAuthor>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlServer(
            @"Server=localhost\SQLEXPRESS;Database=Library_Catalog;Trusted_Connection=True;TrustServerCertificate=True;");
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Book>(e =>
        {
            e.HasKey(b => b.BookId);
            e.HasIndex(b => b.Isbn).IsUnique();
            e.HasIndex(b => b.Title);
            e.Property(b => b.Price).HasPrecision(10, 2);
            e.ToTable(t => t.HasCheckConstraint("CK_Book_Price", "[Price] >= 0"));

            e.HasOne(b => b.Publisher).WithMany(p => p.Books)
                .HasForeignKey(b => b.PublisherId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(b => b.Category).WithMany(c => c.Books)
                .HasForeignKey(b => b.CategoryId).OnDelete(DeleteBehavior.Restrict);

            // Аудит
            e.Property(b => b.IsDeleted).HasDefaultValue(false);
            e.Property(b => b.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(b => b.CreatedBy).HasDefaultValue("system");
            e.Property(b => b.RowVersion).IsRowVersion();

            // Глобальний query filter — soft-deleted книги не потрапляють у звичайні вибірки
            e.HasQueryFilter(b => !b.IsDeleted);
        });
        mb.Entity<BookDetail>(e =>
        {
            e.HasKey(d => d.BookId);
            e.HasOne(d => d.Book).WithOne(b => b.Detail)
             .HasForeignKey<BookDetail>(d => d.BookId);
        });

        mb.Entity<BookAuthor>(e =>
        {
            e.HasKey(ba => new { ba.BookId, ba.AuthorId });
            e.HasOne(ba => ba.Book).WithMany(b => b.BookAuthors).HasForeignKey(ba => ba.BookId);
            e.HasOne(ba => ba.Author).WithMany(a => a.BookAuthors).HasForeignKey(ba => ba.AuthorId);
        });

        mb.Entity<Category>().HasIndex(c => c.Name).IsUnique();

        mb.Entity<Publisher>().HasData(new Publisher
        {
            PublisherId = 1,
            Name = "Видавництво Старого Лева",
            Country = "Ukraine"
        });

        mb.Entity<Category>().HasData(new Category
        {
            CategoryId = 1,
            Name = "Наукова фантастика"
        });
    }
}