namespace Library.Reviews.Domain.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string entity, string key)
        : base($"{entity} with id '{key}' was not found.") { }
}
