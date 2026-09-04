using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Infrastructure.Common.Settings;

namespace OmniDoc.Infrastructure.Services.Email;

public sealed class EmailVerificationFeatureOptions : IEmailVerificationFeatureOptions
{
    public EmailVerificationFeatureOptions(
        IHostEnvironment hostEnvironment,
        IOptions<EmailSettings> emailSettings)
    {
        ShowDemoOtp = hostEnvironment.IsDevelopment() ||
                      emailSettings.Value.ShowDemoOtp;
    }

    public bool ShowDemoOtp { get; }
}
