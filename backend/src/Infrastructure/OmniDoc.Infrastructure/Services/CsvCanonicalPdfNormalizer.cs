using System.Net;
using System.Text;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Infrastructure.Services;

public sealed class CsvCanonicalPdfNormalizer(GotenbergChromiumNormalizer chromium) : IDocumentNormalizer
{
    public async Task<CanonicalPdfResult> NormalizeAsync(Stream sourceStream, DocumentFormat format, CancellationToken ct)
    {
        if (format != DocumentFormat.Csv) throw new NotSupportedException("Expected CSV.");
        var table = new StringBuilder("<table>");
        var info = await new CsvDocumentReader().ReadAsync(sourceStream, (fields, row) =>
        {
            if (row == 0) table.Append("<thead><tr><th scope=\"col\">STT</th>");
            else table.Append("<tr><th scope=\"row\">").Append(row).Append("</th>");
            foreach (var field in fields)
            {
                table.Append(row == 0 ? "<th scope=\"col\">" : "<td>");
                table.Append(WebUtility.HtmlEncode(field));
                table.Append(row == 0 ? "</th>" : "</td>");
            }
            table.Append(row == 0 ? "</tr></thead><tbody>" : "</tr>");
            if (table.Length > 32 * 1024 * 1024) throw new InvalidDataException("CSV print HTML exceeds the 32 Mi-character limit.");
        }, ct);
        table.Append("</tbody></table>");
        var pageSize = info.ColumnCount > 6 ? "A4 landscape" : "A4 portrait";
        var html = """
            <!doctype html><html lang="vi"><head><meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; img-src 'none'; script-src 'none'; base-uri 'none'">
            <style>
            """ + $"@page {{ size: {pageSize}; margin: 15mm; }}" + """
            body { font-family: Arial, sans-serif; font-size: 9pt; line-height: 1.4; color: #111; }
            table { width: 100%; border-collapse: collapse; table-layout: fixed; }
            thead { display: table-header-group; }
            th, td { border: 1px solid #999; padding: 5px; text-align: left; vertical-align: top; white-space: pre-wrap; overflow-wrap: anywhere; }
            thead th { background: #e8f5ee; font-weight: bold; }
            tr { break-inside: avoid; }
            th:first-child { width: 34px; }
            </style></head><body>
            """ + table + "</body></html>";
        return await chromium.ConvertHtmlAsync(html, ct);
    }
}
