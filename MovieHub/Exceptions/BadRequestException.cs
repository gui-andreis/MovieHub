namespace MovieHub.Exceptions;

/// Exceção lançada quando a requisição é inválida.
public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message) { }
}