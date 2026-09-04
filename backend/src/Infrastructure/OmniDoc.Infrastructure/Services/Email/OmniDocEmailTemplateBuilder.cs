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

    public EmailContent BuildPasswordReset(
        string recipientName,
        string resetUrl,
        DateTime expiresAtUtc)
    {
        var safeName = WebUtility.HtmlEncode(recipientName);
        var safeResetUrl = WebUtility.HtmlEncode(resetUrl);

        var html = $$"""
            <!doctype html>
            <html lang="vi">
              <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Đặt lại mật khẩu OmniDoc</title>
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
                            <h1 style="margin:0;font-size:24px;line-height:1.3;">Đặt lại mật khẩu</h1>
                            <p style="margin:14px 0 0;color:#64748b;font-size:15px;line-height:1.7;">Xin chào {{safeName}}, chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản OmniDoc của bạn.</p>
                            <a href="{{safeResetUrl}}" style="display:inline-block;margin-top:26px;padding:13px 24px;background:#2563eb;color:#ffffff;text-decoration:none;border-radius:10px;font-size:15px;font-weight:700;">Đặt lại mật khẩu</a>
                            <p style="margin:20px 0 0;color:#64748b;font-size:13px;line-height:1.6;">Liên kết chỉ dùng được một lần, có hiệu lực trong 15 phút và hết hạn lúc {{expiresAtUtc:HH:mm}} UTC.</p>
                            <div style="margin-top:24px;padding:14px 16px;background:#fff7ed;border-radius:12px;color:#9a3412;font-size:13px;line-height:1.6;">Nếu bạn không yêu cầu thay đổi mật khẩu, hãy bỏ qua email này. Không chuyển tiếp liên kết cho bất kỳ ai.</div>
                          </td>
                        </tr>
                        <tr>
                          <td style="background:#f8fafc;padding:18px 32px;text-align:center;color:#94a3b8;font-size:12px;">OmniDoc sẽ không bao giờ yêu cầu bạn gửi mật khẩu qua email.</td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                </table>
              </body>
            </html>
            """;

        return new EmailContent("Đặt lại mật khẩu OmniDoc", html);
    }

    public EmailContent BuildWorkspaceInvitation(
        string recipientName,
        string workspaceName,
        string inviterName,
        string role,
        string invitationUrl,
        DateTime expiresAtUtc)
    {
        var safeName = WebUtility.HtmlEncode(recipientName);
        var safeWorkspaceName = WebUtility.HtmlEncode(workspaceName);
        var safeInviterName = WebUtility.HtmlEncode(inviterName);
        var safeRole = WebUtility.HtmlEncode(role);
        var safeInvitationUrl = WebUtility.HtmlEncode(invitationUrl);

        var html = $$"""
            <!doctype html>
            <html lang="vi">
              <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Lời mời tham gia Workspace OmniDoc</title>
              </head>
              <body style="margin:0;background:#f8fafc;font-family:Arial,Helvetica,sans-serif;color:#0f172a;">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f8fafc;padding:32px 16px;">
                  <tr>
                    <td align="center">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background:#ffffff;border:1px solid #e2e8f0;border-radius:20px;overflow:hidden;box-shadow:0 12px 32px rgba(15,23,42,.08);">
                        <tr><td style="padding:30px 32px 12px;text-align:center;"><div style="font-size:22px;font-weight:700;">Omni<span style="color:#2563eb;">Doc</span></div></td></tr>
                        <tr>
                          <td style="padding:14px 32px 32px;text-align:center;">
                            <h1 style="margin:0;font-size:24px;line-height:1.3;">Bạn được mời cộng tác</h1>
                            <p style="margin:14px 0 0;color:#64748b;font-size:15px;line-height:1.7;">Xin chào {{safeName}}, <strong>{{safeInviterName}}</strong> đã mời bạn tham gia Workspace <strong>{{safeWorkspaceName}}</strong>.</p>
                            <div style="margin:22px auto 0;padding:12px 16px;max-width:260px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:12px;color:#1d4ed8;font-size:14px;">Vai trò: <strong>{{safeRole}}</strong></div>
                            <a href="{{safeInvitationUrl}}" style="display:inline-block;margin-top:26px;padding:13px 24px;background:#2563eb;color:#ffffff;text-decoration:none;border-radius:10px;font-size:15px;font-weight:700;">Tham gia Workspace</a>
                            <p style="margin:20px 0 0;color:#64748b;font-size:13px;line-height:1.6;">Lời mời có hiệu lực trong 7 ngày và hết hạn lúc {{expiresAtUtc:HH:mm}} UTC ngày {{expiresAtUtc:dd/MM/yyyy}}.</p>
                          </td>
                        </tr>
                        <tr><td style="background:#f8fafc;padding:18px 32px;text-align:center;color:#94a3b8;font-size:12px;">Nếu bạn không mong đợi lời mời này, bạn có thể bỏ qua email.</td></tr>
                      </table>
                    </td>
                  </tr>
                </table>
              </body>
            </html>
            """;

        return new EmailContent(
            $"{inviterName} mời bạn tham gia {workspaceName} trên OmniDoc",
            html);
    }
}
