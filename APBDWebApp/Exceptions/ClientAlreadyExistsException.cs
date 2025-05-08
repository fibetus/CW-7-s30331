namespace APBDWebApp.Exceptions;

public class ClientAlreadyExistsException : Exception
{
    public ClientAlreadyExistsException(string? message) : base(message) { }
}