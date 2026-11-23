using MongoDB.Bson;

namespace Library.Reviews.Domain.Common;

public abstract class BaseEntity
{
    public ObjectId Id { get; protected set; } = ObjectId.GenerateNewId();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }

    /// <summary>Версія документа для оптимістичної конкурентності.</summary>
    public int Version { get; protected set; }

    protected void Touch() => UpdatedAt = DateTime.UtcNow;

    /// <summary>Збільшує версію; викликається репозиторієм перед ReplaceOne.</summary>
    public void BumpVersion() => Version++;
}
