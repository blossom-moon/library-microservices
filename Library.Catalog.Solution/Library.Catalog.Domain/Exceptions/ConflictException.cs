namespace Library.Catalog.Domain.Exceptions;

/// <summary>Конфлікт з поточним станом даних (наприклад, дублікат ISBN). → HTTP 409.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
