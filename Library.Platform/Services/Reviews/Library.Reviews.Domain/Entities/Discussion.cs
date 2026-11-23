using Library.Reviews.Domain.Common;
using Library.Reviews.Domain.Exceptions;
using Library.Reviews.Domain.ValueObjects;

namespace Library.Reviews.Domain.Entities;

/// <summary>Обговорення книги: заголовок і вкладений масив повідомлень.</summary>
public class Discussion : BaseEntity
{
    private List<Comment> _messages = new();

    public long BookId { get; private set; }
    public string Title { get; private set; } = "";
    public Author StartedBy { get; private set; } = null!;
    public IReadOnlyCollection<Comment> Messages => _messages.AsReadOnly();

    private Discussion() { }

    public Discussion(long bookId, string title, Author startedBy)
    {
        if (bookId <= 0) throw new DomainException("BookId must be positive.");
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Discussion title is required.");
        ArgumentNullException.ThrowIfNull(startedBy);
        BookId = bookId;
        Title = title.Trim();
        StartedBy = startedBy;
    }

    public void PostMessage(Author author, string text)
    {
        _messages.Add(new Comment(author, text, DateTime.UtcNow));
        Touch();
    }
}
