namespace APBDWebApp.Exceptions;

public class ClientAlreadyRegisteredException : Exception
{
    public ClientAlreadyRegisteredException(string? message) : base(message) { }
}