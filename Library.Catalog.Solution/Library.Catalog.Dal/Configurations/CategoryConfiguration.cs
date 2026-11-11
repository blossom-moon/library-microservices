using Library.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Catalog.Dal.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.CategoryId);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(c => c.Name).IsUnique();

        builder.HasData(
            new { CategoryId = 1L, Name = "Наукова фантастика" },
            new { CategoryId = 2L, Name = "Історичний роман" },
            new { CategoryId = 3L, Name = "Поезія" },
            new { CategoryId = 4L, Name = "Класична проза" });
    }
}
