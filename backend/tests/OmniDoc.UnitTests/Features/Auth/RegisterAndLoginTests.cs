using OmniDoc.Application.Features.Auth.Commands.LoginUser;
using OmniDoc.Application.Features.Auth.Commands.RegisterUser;
using OmniDoc.Domain.Entities;
using OmniDoc.UnitTests.Features.Documents;

namespace OmniDoc.UnitTests.Features.Auth;

public sealed class RegisterAndLoginTests
{
    [Fact]
    public async Task Register_CreatesUserWithNormalizedEmailAndHashedPassword()
    {
        await using var context = new TestApplicationDbContext();
        var handler = new RegisterUserCommandHandler(
            context,
            new FakePasswordHasher(),
            new FakeJwtTokenGenerator());

        var result = await handler.Handle(
            new RegisterUserCommand(
                "  Owner@Example.COM ",
                "StrongPassword123!",
                "  Workspace Owner  "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.StatusCode);

        var user = Assert.Single(context.Users);
        Assert.Equal("owner@example.com", user.Email);
        Assert.Equal("Workspace Owner", user.FullName);
        Assert.Equal("hashed::StrongPassword123!", user.PasswordHash);
        Assert.Equal($"token::{user.Id}", result.Data!.Token);
    }

    [Fact]
    public async Task Register_RejectsDuplicateNormalizedEmail()
    {
        await using var context = new TestApplicationDbContext();
        context.Users.Add(new User
        {
            Email = "member@example.com",
            FullName = "Existing Member",
            PasswordHash = "hashed::ExistingPassword"
        });
        await context.SaveChangesAsync();

        var handler = new RegisterUserCommandHandler(
            context,
            new FakePasswordHasher(),
            new FakeJwtTokenGenerator());

        var result = await handler.Handle(
            new RegisterUserCommand(
                "MEMBER@example.com",
                "AnotherPassword123!",
                "Duplicate Member"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Single(context.Users);
    }

    [Fact]
    public async Task Login_ReturnsTokenForValidCredentials()
    {
        await using var context = new TestApplicationDbContext();
        var user = new User
        {
            Email = "member@example.com",
            FullName = "Workspace Member",
            PasswordHash = "hashed::CorrectPassword123!"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = new LoginUserCommandHandler(
            context,
            new FakePasswordHasher(),
            new FakeJwtTokenGenerator());

        var result = await handler.Handle(
            new LoginUserCommand(" MEMBER@example.com ", "CorrectPassword123!"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Data!.Id);
        Assert.Equal($"token::{user.Id}", result.Data.Token);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorizedForInvalidCredentials()
    {
        await using var context = new TestApplicationDbContext();
        context.Users.Add(new User
        {
            Email = "member@example.com",
            FullName = "Workspace Member",
            PasswordHash = "hashed::CorrectPassword123!"
        });
        await context.SaveChangesAsync();

        var handler = new LoginUserCommandHandler(
            context,
            new FakePasswordHasher(),
            new FakeJwtTokenGenerator());

        var result = await handler.Handle(
            new LoginUserCommand("member@example.com", "WrongPassword"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("Invalid email or password.", result.Error);
    }
}
