using Library.Reviews.Domain.Common;
using Library.Reviews.Domain.Exceptions;

namespace Library.Reviews.Domain.ValueObjects;

public sealed class Author : ValueObject
{
    public string UserId { get; }
    public string DisplayName { get; }

    public Author(string userId, string displayName)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("Author user id is required.");
        if (string.IsNullOrWhiteSpace(displayName)) throw new DomainException("Author display name is required.");
        UserId = userId.Trim();
        DisplayName = displayName.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return UserId;
        yield return DisplayName;
    }
}
