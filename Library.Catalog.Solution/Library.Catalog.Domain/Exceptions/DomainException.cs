namespace Library.Catalog.Domain.Exceptions;

/// <summary>Порушення бізнес-правила сутності (ціна ≤ 0, недостатній залишок тощо). → HTTP 409.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
