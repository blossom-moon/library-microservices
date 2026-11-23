using Library.Reviews.Domain.Common;
using Library.Reviews.Domain.Exceptions;
using Library.Reviews.Domain.ValueObjects;

namespace Library.Reviews.Domain.Entities;

public class Review : BaseEntity
{
    private List<Comment> _comments = new();

    public long BookId { get; private set; }
    public string BookTitle { get; private set; } = "";

    /// <summary>Лаб. 08: OccurredAt останньої застосованої BookRenamedEvent (захист від out-of-order доставки).</summary>
    public DateTime? BookTitleSyncedAt { get; private set; }
    public Author Author { get; private set; } = null!;
    public int Rating { get; private set; }
    public string Text { get; private set; } = "";
    public IReadOnlyCollection<Comment> Comments => _comments.AsReadOnly();

    // для десеріалізації драйвером
    private Review() { }

    public Review(long bookId, string bookTitle, Author author, int rating, string text)
    {
        if (bookId <= 0) throw new DomainException("BookId must be positive.");
        if (string.IsNullOrWhiteSpace(bookTitle)) throw new DomainException("Book title is required.");
        ArgumentNullException.ThrowIfNull(author);
        ValidateRating(rating);
        ValidateText(text);

        BookId = bookId;
        BookTitle = bookTitle.Trim();
        Author = author;
        Rating = rating;
        Text = text.Trim();
    }

    public void Update(int rating, string text)
    {
        ValidateRating(rating);
        ValidateText(text);
        Rating = rating;
        Text = text.Trim();
        Touch();
    }

    public void AddComment(Author author, string text)
    {
        _comments.Add(new Comment(author, text, DateTime.UtcNow));
        Touch();
    }

    private static void ValidateRating(int rating)
    {
        if (rating is < 1 or > 5) throw new DomainException("Rating must be between 1 and 5.");
    }

    private static void ValidateText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new DomainException("Review text is required.");
        if (text.Length > 2000) throw new DomainException("Review text must not exceed 2000 characters.");
    }
}
