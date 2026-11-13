using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieHub.Dtos;
using MovieHub.Exceptions;
using MovieHub.Models;
using MovieHub.Services.Auth;
using MovieHub.Tests.Helpers;

namespace MovieHub.Tests.Services;

public class RefreshTokenServiceTests
{
    private static RefreshTokenService Create(MovieHub.Data.ApplicationDbContext db)
        => new(db, Options.Create(new JwtOptions { RefreshTokenDays = 30 }), NullLogger<RefreshTokenService>.Instance);

    [Fact]
    public async Task Rotate_IssuesNewToken_AndRevokesTheOldOne()
    {
        var db = TestDbContextFactory.Create();
        await Seed.UsersAsync(db);
        var service = Create(db);

        var first = await service.IssueAsync("user-1", default);
        var (userId, second) = await service.RotateAsync(first, default);

        userId.Should().Be("user-1");
        second.Should().NotBe(first);
        db.RefreshTokens.Single(r => r.TokenHash == RefreshTokenService.Hash(first)).RevokedAt.Should().NotBeNull();
        db.RefreshTokens.Should().NotContain(r => r.TokenHash == first, "o valor em claro nunca é persistido");

        // O novo token continua funcionando
        var (_, third) = await service.RotateAsync(second, default);
        third.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Rotate_ReusingRevokedToken_RevokesTheWholeFamily()
    {
        var db = TestDbContextFactory.Create();
        await Seed.UsersAsync(db);
        var service = Create(db);

        var first = await service.IssueAsync("user-1", default);
        var (_, second) = await service.RotateAsync(first, default);

        await service.Invoking(s => s.RotateAsync(first, default)).Should().ThrowAsync<UnauthorizedException>();

        // Após o reuso, nem o token legítimo mais recente funciona
        await service.Invoking(s => s.RotateAsync(second, default)).Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Rotate_UnknownOrExpiredToken_Throws()
    {
        var db = TestDbContextFactory.Create();
        await Seed.UsersAsync(db);
        var service = Create(db);

        await service.Invoking(s => s.RotateAsync("nao-existe", default)).Should().ThrowAsync<UnauthorizedException>();

        db.RefreshTokens.Add(new RefreshToken { UserId = "user-1", TokenHash = RefreshTokenService.Hash("velho"), ExpiresAt = DateTime.UtcNow.AddMinutes(-1) });
        await db.SaveChangesAsync();
        await service.Invoking(s => s.RotateAsync("velho", default)).Should().ThrowAsync<UnauthorizedException>();
    }
}

public class ProfileServiceTests
{
    [Fact]
    public async Task Create_KidsProfile_CapsMaturityAt10()
    {
        var db = TestDbContextFactory.Create();
        await Seed.UsersAsync(db);
        var service = new ProfileService(db, new FakeCurrentProfile { UserId = "user-1" });

        var profile = await service.CreateAsync(new ProfileRequest { Name = "Pequeno", IsKids = true, MaturityLimit = "16" }, default);

        profile.MaturityLimit.Should().Be("10");
        profile.AvatarKey.Should().StartWith("avatar-");
    }

    [Fact]
    public async Task Create_RespectsMaxProfilesPerAccount()
    {
        var db = TestDbContextFactory.Create();
        await Seed.UsersAsync(db); // já cria 2
        var service = new ProfileService(db, new FakeCurrentProfile { UserId = "user-1" });

        for (var i = 0; i < 3; i++)
            await service.CreateAsync(new ProfileRequest { Name = $"P{i}" }, default);

        await service.Invoking(s => s.CreateAsync(new ProfileRequest { Name = "Sexto" }, default))
            .Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task Update_ProfileOfAnotherUser_IsForbidden()
    {
        var db = TestDbContextFactory.Create();
        var (_, adult, _) = await Seed.UsersAsync(db);
        var service = new ProfileService(db, new FakeCurrentProfile { UserId = "intruso" });

        await service.Invoking(s => s.UpdateAsync(adult.Id, new ProfileRequest { Name = "x" }, default))
            .Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Delete_LastProfile_IsNotAllowed()
    {
        var db = TestDbContextFactory.Create();
        var (_, adult, kids) = await Seed.UsersAsync(db);
        var service = new ProfileService(db, new FakeCurrentProfile { UserId = "user-1" });

        await service.DeleteAsync(kids.Id, default);
        await service.Invoking(s => s.DeleteAsync(adult.Id, default)).Should().ThrowAsync<BadRequestException>();
    }
}
