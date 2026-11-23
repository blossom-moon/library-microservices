using Library.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Catalog.Dal.Configurations;

public class PublisherConfiguration : IEntityTypeConfiguration<Publisher>
{
    public void Configure(EntityTypeBuilder<Publisher> builder)
    {
        builder.ToTable("Publishers");
        builder.HasKey(p => p.PublisherId);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Country).IsRequired().HasMaxLength(100);

        // Статичний сідінг довідника (фіксовані Id) — модельний режим HasData
        builder.HasData(
            new { PublisherId = 1L, Name = "Видавництво Старого Лева", Country = "Ukraine" },
            new { PublisherId = 2L, Name = "А-БА-БА-ГА-ЛА-МА-ГА", Country = "Ukraine" },
            new { PublisherId = 3L, Name = "Фоліо", Country = "Ukraine" });
    }
}
