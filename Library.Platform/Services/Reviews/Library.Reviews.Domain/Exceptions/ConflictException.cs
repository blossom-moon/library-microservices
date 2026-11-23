namespace Library.Reviews.Domain.Exceptions;

/// <summary>Конфлікт версій (оптимістична конкурентність) або дублювання.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
