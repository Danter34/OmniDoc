using System.IO.Compression;
using System.Buffers.Binary;
using OmniDoc.Domain.Enums;
using OmniDoc.Domain.Exceptions;
using OmniDoc.Infrastructure.Services;

namespace OmniDoc.UnitTests.Features.Documents;

public sealed class OfficeFormatDetectorTests
{
    [Theory]
    [InlineData(DocumentFormat.Docx, "report.DOCX", OfficeFixture.DocxMime)]
    [InlineData(DocumentFormat.Pptx, "slides.pptx", OfficeFixture.PptxMime)]
    public async Task AcceptsMatchingOpenXmlPackage(DocumentFormat format, string name, string mime)
    {
        using var stream = new MemoryStream(OfficeFixture.Create(format));
        var result = await new DocumentFormatDetector().DetectAsync(stream, name, default);
        Assert.Equal(format, result.Format);
        Assert.Equal(mime, result.ContentType);
        Assert.Equal(0, stream.Position);
    }

    [Theory]
    [InlineData(DocumentFormat.Docx, "fake.pptx")]
    [InlineData(DocumentFormat.Pptx, "fake.docx")]
    [InlineData(DocumentFormat.Docx, "macro.docm")]
    [InlineData(DocumentFormat.Pptx, "macro.pptm")]
    public async Task RejectsMismatchAndMacroExtensions(DocumentFormat format, string name)
    {
        using var stream = new MemoryStream(OfficeFixture.Create(format));
        await Assert.ThrowsAsync<InvalidDataException>(() => new DocumentFormatDetector().DetectAsync(stream, name, default));
        Assert.Equal(0, stream.Position);
    }

    [Theory]
    [InlineData("protected.docx")]
    [InlineData("protected.pptx")]
    public async Task OleEncryptionReturnsTypedPasswordError(string name)
    {
        using var stream = new MemoryStream(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 });
        var ex = await Assert.ThrowsAsync<PasswordRequiredException>(() => new DocumentFormatDetector().DetectAsync(stream, name, default));
        Assert.Equal(DocumentFailureCode.PasswordRequired, ex.Code);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task RejectsEncryptedPackageEntry()
    {
        using var stream = new MemoryStream(OfficeFixture.Create(DocumentFormat.Docx, z => OfficeFixture.Add(z, "EncryptedPackage", "encrypted")));
        await Assert.ThrowsAsync<PasswordRequiredException>(() => new DocumentFormatDetector().DetectAsync(stream, "protected.docx", default));
    }

    [Theory]
    [InlineData("word/vbaProject.bin")]
    [InlineData("../escape.xml")]
    [InlineData("word/document.xml")]
    [InlineData("WORD/DOCUMENT.XML")]
    public async Task RejectsMacrosTraversalAndDuplicateEntries(string path)
    {
        using var stream = new MemoryStream(OfficeFixture.Create(DocumentFormat.Docx, z => OfficeFixture.Add(z, path, "invalid")));
        await Assert.ThrowsAsync<InvalidDataException>(() => new DocumentFormatDetector().DetectAsync(stream, "bad.docx", default));
    }

    [Fact]
    public async Task RejectsZipBombRatio()
    {
        using var stream = new MemoryStream(OfficeFixture.Create(DocumentFormat.Docx, z => OfficeFixture.Add(z, "word/bomb.txt", new string('x', 2 * 1024 * 1024))));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => new DocumentFormatDetector().DetectAsync(stream, "bomb.docx", default));
        Assert.Contains("expansion", error.Message);
    }

    [Fact]
    public async Task RejectsTooManyEntries()
    {
        using var stream = new MemoryStream(OfficeFixture.Create(DocumentFormat.Docx, z =>
        {
            for (var i = 0; i < 2048; i++) OfficeFixture.Add(z, $"extra/{i}", "");
        }));
        await Assert.ThrowsAsync<InvalidDataException>(() => new DocumentFormatDetector().DetectAsync(stream, "bomb.docx", default));
    }

    [Fact]
    public async Task RejectsZipWithOnlySpoofedDocumentPath()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) OfficeFixture.Add(zip, "word/document.xml", "not XML");
        stream.Position = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => new DocumentFormatDetector().DetectAsync(stream, "fake.docx", default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectsDeclaredEntryAndTotalExpansionBeforeReadingPayload(bool aggregate)
    {
        var bytes = OfficeFixture.Create(DocumentFormat.Docx, z =>
        {
            OfficeFixture.Add(z, "word/extra1.bin", "x");
            OfficeFixture.Add(z, "word/extra2.bin", "y");
        });
        // Forge large central-directory sizes without allocating/decompressing a bomb.
        for (var i = 0; i <= bytes.Length - 46; i++)
        {
            if (!bytes.AsSpan(i, 4).SequenceEqual("PK\x01\x02"u8)) continue;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i + 20, 4), 1024 * 1024);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i + 24, 4), (aggregate ? 60u : 65u) * 1024 * 1024);
        }
        using var stream = new MemoryStream(bytes);
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => new DocumentFormatDetector().DetectAsync(stream, "bomb.docx", default));
        Assert.Contains("expansion limits", ex.Message);
    }

    [Theory]
    [InlineData("[Content_Types].xml", "<Types/>")]
    [InlineData("word/document.xml", "<!DOCTYPE x [<!ENTITY bomb SYSTEM 'file:///etc/passwd'>]><x>&bomb;</x>")]
    [InlineData("word/document.xml", "<document>not OOXML</document>")]
    [InlineData("_rels/.rels", "<Relationships/>")]
    public async Task RejectsMalformedMetadata(string path, string replacement)
    {
        using var stream = new MemoryStream();
        stream.Write(OfficeFixture.Create(DocumentFormat.Docx));
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, true))
        {
            zip.GetEntry(path)!.Delete();
            OfficeFixture.Add(zip, path, replacement);
        }
        stream.Position = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => new DocumentFormatDetector().DetectAsync(stream, "invalid.docx", default));
    }
}
