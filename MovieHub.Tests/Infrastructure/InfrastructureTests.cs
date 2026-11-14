using FluentAssertions;
using MovieHub.Common;
using MovieHub.Extensions;

namespace MovieHub.Tests.Infrastructure;

public class InfrastructureTests
{
    [Fact]
    public void DatabaseUrl_IsConvertedToNpgsqlFormat()
    {
        var cs = InfrastructureExtensions.ConvertDatabaseUrl(
            "postgresql://neon_user:p%40ss@ep-cool-123.sa-east-1.aws.neon.tech/moviehub?sslmode=require");

        cs.Should().Contain("Host=ep-cool-123.sa-east-1.aws.neon.tech;")
            .And.Contain("Port=5432;")
            .And.Contain("Database=moviehub;")
            .And.Contain("Username=neon_user;")
            .And.Contain("Password=p@ss;")
            .And.Contain("SSL Mode=Require;");
    }

    [Theory]
    [InlineData("postgres://u:p@db.internal:5432/app", "SSL Mode=Prefer;")]
    [InlineData("postgres://u:p@db.internal:5432/app?sslmode=disable", "SSL Mode=Disable;")]
    public void DatabaseUrl_RespectsSslMode(string url, string expected)
        => InfrastructureExtensions.ConvertDatabaseUrl(url).Should().Contain(expected);

    [Fact]
    public void NpgsqlConnectionString_IsKeptAsIs()
    {
        const string cs = "Host=localhost;Port=5438;Database=db;Username=u;Password=p";
        InfrastructureExtensions.ConvertDatabaseUrl(cs).Should().Be(cs);
    }

    [Fact]
    public void UpstashUrl_EnablesSslAndPassword()
    {
        var options = InfrastructureExtensions.ParseRedis("rediss://default:segredo@us1-fine-cat-123.upstash.io:6379");

        options.Ssl.Should().BeTrue();
        options.Password.Should().Be("segredo");
        options.EndPoints.Should().ContainSingle();
        options.AbortOnConnectFail.Should().BeFalse();
    }

    [Fact]
    public void PlainRedisHost_IsParsed()
    {
        var options = InfrastructureExtensions.ParseRedis("localhost:6379");
        options.Ssl.Should().BeFalse();
        options.EndPoints.Should().ContainSingle();
    }

    [Theory]
    [InlineData("Coração Valente", "coracao valente")]
    [InlineData("  AÇÃO   e   Aventura ", "acao e aventura")]
    [InlineData("Pokémon", "pokemon")]
    public void Normalize_RemovesAccentsAndCase(string input, string expected)
        => TextNormalizer.Normalize(input).Should().Be(expected);

    [Theory]
    [InlineData("Ficção científica", "ficcao-cientifica")]
    [InlineData("Ação & Aventura", "acao-aventura")]
    public void Slugify_Works(string input, string expected)
        => TextNormalizer.Slugify(input).Should().Be(expected);

    [Theory]
    [InlineData("L", "L")]
    [InlineData("AL", "L")]
    [InlineData("PG-13", "12")]
    [InlineData("TV-MA", "18")]
    [InlineData("R", "16")]
    [InlineData("", null)]
    [InlineData("XYZ", null)]
    public void AgeRating_FromCertification(string input, string? expected)
        => AgeRatings.FromCertification(input).Should().Be(expected);

    [Theory]
    [InlineData("18", false, null)]
    [InlineData("14", false, 14)]
    [InlineData("18", true, 10)]
    [InlineData("L", true, 0)]
    public void EffectiveLimit_CombinesMaturityAndKids(string limit, bool kids, int? expected)
        => AgeRatings.EffectiveLimit(limit, kids).Should().Be(expected);
}
