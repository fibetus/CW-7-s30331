namespace APBDWebApp.Exceptions;

public class TripNotFoundException : Exception
{
    public TripNotFoundException(string? message) : base(message) { }
}