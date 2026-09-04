using System.Net;
using Microsoft.Extensions.Configuration;
using OmniDoc.Application.Common.Interfaces;

namespace OmniDoc.Infrastructure.Services.Email;

public sealed class OmniDocEmailTemplateBuilder : IEmailTemplateBuilder
{
    private readonly string _verificationUrl;

    public OmniDocEmailTemplateBuilder(IConfiguration configuration)
    {
        var frontendBaseUrl = (configuration["Frontend:BaseUrl"] ??
                               "http://localhost:3000").TrimEnd('/');
        _verificationUrl = $"{frontendBaseUrl}/verify-email";
    }

    public EmailContent BuildEmailVerificationOtp(
        string recipientName,
        string otp,
        DateTime expiresAtUtc)
    {
        var safeName = WebUtility.HtmlEncode(recipientName);
        var safeOtp = WebUtility.HtmlEncode(otp);
        var safeVerificationUrl = WebUtility.HtmlEncode(_verificationUrl);

        var html = $$"""
            <!doctype html>
            <html lang="vi">
              <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Xác minh email OmniDoc</title>
              </head>
              <body style="margin:0;background:#f8fafc;font-family:Arial,Helvetica,sans-serif;color:#0f172a;">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f8fafc;padding:32px 16px;">
                  <tr>
                    <td align="center">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background:#ffffff;border:1px solid #e2e8f0;border-radius:20px;overflow:hidden;box-shadow:0 12px 32px rgba(15,23,42,.08);">
                        <tr>
                          <td style="padding:30px 32px 12px;text-align:center;">
                            <div style="font-size:22px;font-weight:700;letter-spacing:-.4px;">Omni<span style="color:#2563eb;">Doc</span></div>
                          </td>
                        </tr>
                        <tr>
                          <td style="padding:14px 32px 32px;text-align:center;">
                            <h1 style="margin:0;font-size:24px;line-height:1.3;">Xác minh địa chỉ email</h1>
                            <p style="margin:14px 0 0;color:#64748b;font-size:15px;line-height:1.7;">Xin chào {{safeName}}, hãy dùng mã bên dưới để hoàn tất xác minh tài khoản OmniDoc.</p>
                            <div style="margin:26px auto 20px;padding:18px 20px;max-width:300px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:14px;color:#1d4ed8;font-size:34px;font-weight:800;letter-spacing:12px;text-indent:12px;">{{safeOtp}}</div>
                            <p style="margin:0;color:#64748b;font-size:13px;line-height:1.6;">Mã có hiệu lực trong 10 phút và hết hạn lúc {{expiresAtUtc:HH:mm}} UTC.</p>
                            <a href="{{safeVerificationUrl}}" style="display:inline-block;margin-top:22px;padding:12px 22px;background:#2563eb;color:#ffffff;text-decoration:none;border-radius:10px;font-size:14px;font-weight:700;">Xác minh trên OmniDoc</a>
                            <div style="margin-top:24px;padding:14px 16px;background:#fff7ed;border-radius:12px;color:#9a3412;font-size:13px;line-height:1.6;">Không chia sẻ mã này với bất kỳ ai. Nhân viên OmniDoc sẽ không bao giờ yêu cầu bạn cung cấp OTP.</div>
                          </td>
                        </tr>
                        <tr>
                          <td style="background:#f8fafc;padding:18px 32px;text-align:center;color:#94a3b8;font-size:12px;">Nếu bạn không tạo tài khoản, bạn có thể bỏ qua email này.</td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                </table>
              </body>
            </html>
            """;

        return new EmailContent("Mã xác minh email OmniDoc", html);
    }
}
