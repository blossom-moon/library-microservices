namespace Library.Reviews.Domain.Exceptions;

/// <summary>Порушення інваріанта предметної області.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
