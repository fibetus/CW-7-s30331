namespace APBDWebApp.Exceptions;

public class ClientNotFoundException : Exception
{
    public ClientNotFoundException(string? message) : base(message) { }
}