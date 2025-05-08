namespace APBDWebApp.Exceptions;

public class RegistrationNotFoundException : Exception
{
    public RegistrationNotFoundException(string? message) : base(message) { }
}