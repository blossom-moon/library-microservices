namespace Library.Loans.Domain;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public class BusinessConflictException : Exception
{
    public BusinessConflictException(string message) : base(message) { }
}

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}