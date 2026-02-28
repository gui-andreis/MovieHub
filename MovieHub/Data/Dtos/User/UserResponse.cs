namespace MovieHub.Data.Dtos.User;



// só se for retornar dados pro user, talvez fique legal se colocar dos favoritos e tal
public class UserResponseDto
{
    public string Id { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string? FullName { get; set; }
}