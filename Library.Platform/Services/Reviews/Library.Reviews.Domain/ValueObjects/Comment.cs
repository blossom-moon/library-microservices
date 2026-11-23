using Library.Reviews.Domain.Common;
using Library.Reviews.Domain.Exceptions;

namespace Library.Reviews.Domain.ValueObjects;

/// <summary>Коментар до відгуку — незмінний Value Object, що зберігається вкладено в документ.</summary>
public sealed class Comment : ValueObject
{
    public Author Author { get; }
    public string Text { get; }
    public DateTime CreatedAt { get; }

    public Comment(Author author, string text, DateTime createdAt)
    {
        if (author is null) throw new DomainException("Comment author is required.");
        if (string.IsNullOrWhiteSpace(text)) throw new DomainException("Comment text is required.");
        if (text.Length > 1000) throw new DomainException("Comment text must not exceed 1000 characters.");
        Author = author;
        Text = text.Trim();
        CreatedAt = createdAt;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Author;
        yield return Text;
        yield return CreatedAt;
    }
}
