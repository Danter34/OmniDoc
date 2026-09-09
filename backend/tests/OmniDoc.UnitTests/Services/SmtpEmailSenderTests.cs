using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using Moq;
using OmniDoc.Infrastructure;
using OmniDoc.Infrastructure.Common.Settings;
using OmniDoc.Infrastructure.Services.Email;

namespace OmniDoc.UnitTests.Services;

public sealed class SmtpEmailSenderTests
{
    [Theory]
    [InlineData(587, false, SecureSocketOptions.StartTls)]
    [InlineData(587, true, SecureSocketOptions.StartTls)]
    [InlineData(465, false, SecureSocketOptions.SslOnConnect)]
    [InlineData(465, true, SecureSocketOptions.SslOnConnect)]
    [InlineData(1025, false, SecureSocketOptions.None)]
    [InlineData(1025, true, SecureSocketOptions.None)]
    [InlineData(25, false, SecureSocketOptions.None)]
    [InlineData(2525, true, SecureSocketOptions.StartTls)]
    public async Task SendsHtmlWithExpectedSecurity(int port, bool ssl, SecureSocketOptions security)
    {
        var settings = new SmtpOptions { Port = port, EnableSsl = ssl };
        var client = new Mock<ISmtpClient>();
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        string? body = null;
        client.Setup(c => c.SendAsync(It.IsAny<MimeMessage>(), token, null))
            .Callback<MimeMessage, CancellationToken, ITransferProgress?>((message, _, _) =>
            {
                Assert.Equal(settings.SenderEmail, Assert.Single(message.From.Mailboxes).Address);
                Assert.Equal(settings.SenderName, Assert.Single(message.From.Mailboxes).Name);
                Assert.Equal("invitee@example.com", Assert.Single(message.To.Mailboxes).Address);
                Assert.Equal("Invitation", message.Subject);
                body = message.HtmlBody;
            })
            .ReturnsAsync("queued");

        await Sender(settings, client).SendEmailAsync("invitee@example.com", "Invitation", "<p>Join</p>", token);

        Assert.Equal("<p>Join</p>", body);
        client.Verify(c => c.ConnectAsync(settings.Host, port, security, token), Times.Once);
        client.Verify(c => c.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        client.Verify(c => c.DisconnectAsync(true, token), Times.Once);
        client.Verify(c => c.Dispose(), Times.Once);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("user", " ")]
    [InlineData(" ", "test-only-password")]
    [InlineData("user", "test-only-password")]
    public async Task AuthenticatesOnlyWithBothCredentials(string username, string password)
    {
        var settings = new SmtpOptions { Port = 587, UserName = username, Password = password };
        var client = new Mock<ISmtpClient>(MockBehavior.Strict);
        var sequence = new MockSequence();
        client.InSequence(sequence).Setup(c => c.ConnectAsync(settings.Host, 587, SecureSocketOptions.StartTls, default))
            .Returns(Task.CompletedTask);
        var authenticate = !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password);
        if (authenticate)
        {
            client.InSequence(sequence).Setup(c => c.AuthenticateAsync(username, password, default))
                .Returns(Task.CompletedTask);
        }
        client.InSequence(sequence).Setup(c => c.SendAsync(It.IsAny<MimeMessage>(), default, null)).ReturnsAsync("queued");
        client.InSequence(sequence).Setup(c => c.DisconnectAsync(true, default)).Returns(Task.CompletedTask);
        client.Setup(c => c.Dispose());

        await Sender(settings, client).SendEmailAsync("invitee@example.com", "Invitation", "Join");

        client.Verify(c => c.AuthenticateAsync(username, password, default), authenticate ? Times.Once() : Times.Never());
        client.VerifyAll();
    }

    [Theory]
    [InlineData("connect")]
    [InlineData("authenticate")]
    [InlineData("send")]
    public async Task LogsFailureWithoutCredentialsAndRethrows(string stage)
    {
        var settings = new SmtpOptions { UserName = "user", Password = Guid.NewGuid().ToString() };
        var client = new Mock<ISmtpClient>();
        var logger = new Mock<ILogger<SmtpEmailSender>>();
        var failure = new IOException(settings.Password);
        if (stage == "connect")
            client.Setup(c => c.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.None, default)).ThrowsAsync(failure);
        else if (stage == "authenticate")
            client.Setup(c => c.AuthenticateAsync(settings.UserName, settings.Password, default)).ThrowsAsync(failure);
        else
            client.Setup(c => c.SendAsync(It.IsAny<MimeMessage>(), default, null)).ThrowsAsync(failure);

        var thrown = await Assert.ThrowsAsync<IOException>(() =>
            new SmtpEmailSender(Options.Create(settings), () => client.Object, logger.Object)
                .SendEmailAsync("invitee@example.com", "Invitation", "Join"));

        Assert.Same(failure, thrown);
        logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("IOException") &&
                !state.ToString()!.Contains(settings.Password)), null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        client.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public async Task CallerCancellationIsPropagatedWithoutErrorLog()
    {
        var client = new Mock<ISmtpClient>();
        var logger = new Mock<ILogger<SmtpEmailSender>>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        client.Setup(c => c.ConnectAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<SecureSocketOptions>(), cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SmtpEmailSender(Options.Create(new SmtpOptions()), () => client.Object, logger.Object)
                .SendEmailAsync("invitee@example.com", "Invitation", "Join", cancellation.Token));

        Assert.Empty(logger.Invocations);
        client.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public void SmtpEnvironmentVariablesOverrideLegacyConfiguration()
    {
        var prefix = $"OMNIDOC_SMTP_TEST_{Guid.NewGuid():N}_";
        var values = new Dictionary<string, string>
        {
            ["Host"] = "smtp.example.com", ["Port"] = "587", ["UserName"] = "smtp-user",
            ["Password"] = Guid.NewGuid().ToString(), ["SenderEmail"] = "sender@example.com",
            ["SenderName"] = "Test Sender", ["EnableSsl"] = "true"
        };
        try
        {
            foreach (var (key, value) in values)
                Environment.SetEnvironmentVariable($"{prefix}Smtp__{key}", value);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
                ["EmailSettings:Host"] = "mailpit", ["EmailSettings:Port"] = "1025"
            }).AddEnvironmentVariables(prefix).Build();
            using var provider = new ServiceCollection().AddInfrastructureServices(configuration).BuildServiceProvider();

            var settings = provider.GetRequiredService<IOptions<SmtpOptions>>().Value;

            Assert.Equal(values["Host"], settings.Host);
            Assert.Equal(587, settings.Port);
            Assert.Equal(values["UserName"], settings.UserName);
            Assert.Equal(values["Password"], settings.Password);
            Assert.Equal(values["SenderEmail"], settings.SenderEmail);
            Assert.Equal(values["SenderName"], settings.SenderName);
            Assert.True(settings.EnableSsl);
        }
        finally
        {
            foreach (var key in values.Keys)
                Environment.SetEnvironmentVariable($"{prefix}Smtp__{key}", null);
        }
    }

    private static SmtpEmailSender Sender(SmtpOptions settings, Mock<ISmtpClient> client) =>
        new(Options.Create(settings), () => client.Object, NullLogger<SmtpEmailSender>.Instance);
}
