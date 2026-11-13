namespace MovieHub.Services.Auth;

public class JwtOptions
{
    public const string Section = "Jwt";

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "MovieHubAPI";
    public string Audience { get; set; } = "MovieHubAPI";
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 30;
}
