using System.IO.Compression;
using System.Text;
using OmniDoc.Domain.Enums;

namespace OmniDoc.UnitTests.Features.Documents;

internal static class OfficeFixture
{
    public const string DocxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string PptxMime = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
    public const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Create(DocumentFormat format, Action<ZipArchive>? change = null)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            var word = format == DocumentFormat.Docx;
            var path = format switch { DocumentFormat.Docx => "word/document.xml", DocumentFormat.Pptx => "ppt/presentation.xml", _ => "xl/workbook.xml" };
            var mime = format switch
            {
                DocumentFormat.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
                DocumentFormat.Pptx => "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml",
                _ => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"
            };
            Add(zip, "[Content_Types].xml", $"<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Override PartName=\"/{path}\" ContentType=\"{mime}\"/></Types>");
            Add(zip, "_rels/.rels", $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"{path}\"/></Relationships>");
            Add(zip, path, word
                ? "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>Office evidence</w:t></w:r></w:p></w:body></w:document>"
                : format == DocumentFormat.Pptx
                    ? "<p:presentation xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"><p:sldIdLst/><p:sldSz cx=\"12192000\" cy=\"6858000\"/></p:presentation>"
                    : "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheets/></workbook>");
            change?.Invoke(zip);
        }
        return buffer.ToArray();
    }

    public static void Add(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(text);
    }
}
