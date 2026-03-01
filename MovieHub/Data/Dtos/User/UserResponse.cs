namespace MovieHub.Data.Dtos.User;


//Fora de Funcionamento, Será implementado no futuro, onde o usuário poderá ver seus dados 
public class UserResponseDto
{
    public string Id { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string? FullName { get; set; }
}