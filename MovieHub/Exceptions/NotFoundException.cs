namespace MovieHub.Exceptions;

/// Exceção lançada quando um recurso não é encontrado.
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
