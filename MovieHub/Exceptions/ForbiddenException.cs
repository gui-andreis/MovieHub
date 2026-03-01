namespace MovieHub.Exceptions;

/// Exceção lançada quando o usuário não possui permissão para acessar o recurso.
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
