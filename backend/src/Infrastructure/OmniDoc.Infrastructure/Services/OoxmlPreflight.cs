using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Enums;
using OmniDoc.Domain.Exceptions;

namespace OmniDoc.Infrastructure.Services;

internal static class OoxmlPreflight
{
    // Bound both declared sizes and the bytes actually decompressed. Never extract to disk.
    private const int MaxEntries = 2048;
    private const long MaxEntryBytes = 64L * 1024 * 1024;
    private const long MaxTotalBytes = 256L * 1024 * 1024;
    private const long MaxXmlBytes = 8L * 1024 * 1024;
    private const int MaxCompressionRatio = 200;
    internal const string DocxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    internal const string PptxMime = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
    internal const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    internal static async Task<DetectedDocumentFormat> InspectAsync(Stream source, string extension, CancellationToken ct)
    {
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count > MaxEntries) throw new InvalidDataException("Office package has too many ZIP entries.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long declaredTotal = 0;
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            var name = entry.FullName;
            if (!names.Add(name) || name.StartsWith('/') || name.Contains('\\') || name.Split('/').Any(p => p is ".." or "."))
                throw new InvalidDataException("Office package contains duplicate or unsafe entry paths.");
            if (name.Equals("EncryptedPackage", StringComparison.OrdinalIgnoreCase) || name.Equals("EncryptionInfo", StringComparison.OrdinalIgnoreCase))
                throw new PasswordRequiredException();
            if (name.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Macro-enabled Office files are not supported.");
            if (entry.Length > MaxEntryBytes || entry.Length > MaxTotalBytes - declaredTotal ||
                entry.Length / Math.Max(1, entry.CompressedLength) > MaxCompressionRatio)
                throw new InvalidDataException("Office package exceeds ZIP expansion limits.");
            declaredTotal += entry.Length;
        }

        var word = archive.GetEntry("word/document.xml");
        var slides = archive.GetEntry("ppt/presentation.xml");
        var workbook = archive.GetEntry("xl/workbook.xml");
        var candidates = new[] { word, slides, workbook }.Where(e => e is not null).ToList();
        var expectedPath = extension switch { ".docx" => "word/document.xml", ".pptx" => "ppt/presentation.xml", ".xlsx" => "xl/workbook.xml", _ => "" };
        if (candidates.Count != 1 || candidates[0]!.FullName != expectedPath)
            throw new InvalidDataException("Office package content does not match its extension.");
        var main = candidates[0]!;
        var types = archive.GetEntry("[Content_Types].xml") ?? throw new InvalidDataException("Missing OOXML content types.");
        var relationships = archive.GetEntry("_rels/.rels") ?? throw new InvalidDataException("Missing OOXML package relationships.");
        var xml = new Dictionary<string, XDocument>();
        var buffer = new byte[81920];
        long actualTotal = 0;
        foreach (var entry in archive.Entries)
        {
            await using var input = entry.Open();
            var inspectXml = entry == main || entry == types || entry == relationships;
            if (inspectXml && entry.Length > MaxXmlBytes) throw new InvalidDataException("Office metadata XML is too large.");
            using var metadata = inspectXml ? new MemoryStream() : null;
            long actual = 0;
            int count;
            while ((count = await input.ReadAsync(buffer, ct)) > 0)
            {
                actual += count;
                actualTotal += count;
                if (actual > entry.Length || actual > MaxEntryBytes || actualTotal > MaxTotalBytes)
                    throw new InvalidDataException("Office ZIP expansion exceeds its declared size or limits.");
                metadata?.Write(buffer, 0, count);
            }
            if (actual != entry.Length) throw new InvalidDataException("Truncated Office ZIP entry.");
            if (metadata is not null)
            {
                metadata.Position = 0;
                using var reader = XmlReader.Create(metadata, new XmlReaderSettings
                { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxXmlBytes });
                try { xml[entry.FullName] = XDocument.Load(reader); }
                catch (XmlException ex) { throw new InvalidDataException("Invalid OOXML metadata.", ex); }
            }
        }

        XNamespace contentNs = "http://schemas.openxmlformats.org/package/2006/content-types";
        var contentTypes = xml[types.FullName];
        if (contentTypes.Root?.Name != contentNs + "Types" || contentTypes.Descendants().Attributes("ContentType").Any(a =>
            a.Value.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase) || a.Value.Contains("vbaProject", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Invalid or macro-enabled Office content types.");
        var expectedType = extension switch
        {
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml",
            _ => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"
        };
        if (!contentTypes.Descendants(contentNs + "Override").Any(e => (string?)e.Attribute("PartName") == "/" + main.FullName && (string?)e.Attribute("ContentType") == expectedType))
            throw new InvalidDataException("Missing or incorrect Office main content type.");
        var root = xml[main.FullName].Root;
        var namespaceName = extension switch { ".docx" => "wordprocessingml", ".pptx" => "presentationml", _ => "spreadsheetml" };
        var rootName = extension switch { ".docx" => "document", ".pptx" => "presentation", _ => "workbook" };
        if (root?.Name.LocalName != rootName ||
            (root.Name.NamespaceName != $"http://schemas.openxmlformats.org/{namespaceName}/2006/main" &&
             root.Name.NamespaceName != $"http://purl.oclc.org/ooxml/{namespaceName}/main"))
            throw new InvalidDataException("Invalid Office document root.");
        XNamespace relNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        if (xml[relationships.FullName].Root?.Name != relNs + "Relationships")
            throw new InvalidDataException("Invalid Office package relationships root.");
        var officeRelations = xml[relationships.FullName].Descendants(relNs + "Relationship")
            .Where(e => ((string?)e.Attribute("Type")) is "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" or "http://purl.oclc.org/ooxml/officeDocument/relationships/officeDocument").ToList();
        if (officeRelations.Count != 1 || ((string?)officeRelations[0].Attribute("Target"))?.TrimStart('/') != main.FullName ||
            string.Equals((string?)officeRelations[0].Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid Office package document relationship.");
        return extension switch
        {
            ".docx" => new(DocumentFormat.Docx, DocxMime),
            ".pptx" => new(DocumentFormat.Pptx, PptxMime),
            _ => new(DocumentFormat.Xlsx, XlsxMime)
        };
    }
}
