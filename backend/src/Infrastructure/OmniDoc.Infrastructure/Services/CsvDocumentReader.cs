using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace OmniDoc.Infrastructure.Services;

public sealed record CsvDocumentInfo(string ContentType, char Delimiter, int ColumnCount, int DataRows);

/// <summary>Bounded, forward-only record parsing; source bytes are never rewritten.</summary>
public sealed class CsvDocumentReader
{
    public const int MaxDataRows = 5000;
    public const int MaxColumns = 128;
    public const int MaxCellCharacters = 16384;
    private const int MaxDecodedCharacters = 8 * 1024 * 1024;

    // rowIndex zero is the header; data records are numbered from one.
    public async Task<CsvDocumentInfo> ReadAsync(Stream source, Action<string[], int>? onRow, CancellationToken ct)
    {
        if (!source.CanSeek) throw new InvalidDataException("CSV requires a seekable stream.");
        var start = source.Position;
        try
        {
            var encoding = await DetectEncodingAsync(source, ct);
            source.Position = start;
            var delimiter = DetectDelimiter(source, encoding, ct);
            source.Position = start;
            using var parser = new TextFieldParser(source, encoding, detectEncoding: true, leaveOpen: true)
            {
                TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false
            };
            parser.SetDelimiters(delimiter.ToString());
            var index = 0;
            var columns = 0;
            while (!parser.EndOfData)
            {
                ct.ThrowIfCancellationRequested();
                if (index > MaxDataRows) throw new InvalidDataException("CSV is limited to 5,000 data rows; split the file before uploading.");
                var fields = parser.ReadFields() ?? [];
                if (fields.Length is 0 or > MaxColumns || fields.Any(f => f.Length > MaxCellCharacters))
                    throw new InvalidDataException("CSV exceeds the column or cell-size limit.");
                if (index == 0)
                {
                    columns = fields.Length;
                    if (fields.All(string.IsNullOrWhiteSpace)) throw new InvalidDataException("CSV must have a non-empty header.");
                }
                else if (fields.Length != columns) throw new InvalidDataException($"CSV data row {index} does not match the header column count.");
                onRow?.Invoke(fields, index);
                index++;
            }
            if (index == 0) throw new InvalidDataException("CSV contains no header.");
            return new(encoding.CodePage == Encoding.Latin1.CodePage ? "text/csv; charset=iso-8859-1" : "text/csv; charset=utf-8", delimiter, columns, index - 1);
        }
        catch (MalformedLineException ex) { throw new InvalidDataException("CSV contains malformed quoted fields.", ex); }
        finally { source.Position = start; }
    }

    private static async Task<Encoding> DetectEncodingAsync(Stream source, CancellationToken ct)
    {
        var start = source.Position;
        var prefix = new byte[8];
        var count = await source.ReadAtLeastAsync(prefix, prefix.Length, throwOnEndOfStream: false, cancellationToken: ct);
        source.Position = start;
        var utf8Bom = count >= 3 && prefix.AsSpan(0, 3).SequenceEqual(new byte[] { 0xef, 0xbb, 0xbf });
        if ((count >= 2 && (prefix[0] == 0xff && prefix[1] == 0xfe || prefix[0] == 0xfe && prefix[1] == 0xff)) ||
            (count >= 4 && prefix.AsSpan(0, 4).SequenceEqual("PK\x03\x04"u8)) ||
            (count >= 5 && prefix.AsSpan(0, 5).SequenceEqual("%PDF-"u8)) ||
            (count >= 8 && prefix.AsSpan(0, 8).SequenceEqual(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 })))
            throw new InvalidDataException("CSV must contain UTF-8, ASCII or Latin1 text, not a binary document.");
        var utf8 = new UTF8Encoding(false, true);
        try
        {
            await ValidateTextAsync(source, utf8, ct);
            return utf8;
        }
        catch (DecoderFallbackException) when (!utf8Bom)
        {
            source.Position = start;
            await ValidateTextAsync(source, Encoding.Latin1, ct);
            return Encoding.Latin1;
        }
        catch (DecoderFallbackException ex) { throw new InvalidDataException("CSV has a UTF-8 BOM but invalid UTF-8 content.", ex); }
    }

    private static async Task ValidateTextAsync(Stream source, Encoding encoding, CancellationToken ct)
    {
        using var reader = new StreamReader(source, encoding, false, 4096, leaveOpen: true);
        var buffer = new char[4096];
        var total = 0;
        int count;
        while ((count = await reader.ReadAsync(buffer, ct)) > 0)
        {
            total += count;
            if (total > MaxDecodedCharacters) throw new InvalidDataException("CSV exceeds the 8 Mi-character text limit.");
            foreach (var c in buffer.AsSpan(0, count))
                if (char.IsControl(c) && c is not ('\t' or '\r' or '\n'))
                    throw new InvalidDataException("CSV contains binary control characters.");
        }
    }

    private static char DetectDelimiter(Stream source, Encoding encoding, CancellationToken ct)
    {
        using var reader = new StreamReader(source, encoding, true, 4096, leaveOpen: true);
        var counts = new Dictionary<char, int> { [','] = 0, [';'] = 0, ['\t'] = 0 };
        var quoted = false;
        var length = 0;
        var hasContent = false;
        int value;
        while ((value = reader.Read()) != -1)
        {
            ct.ThrowIfCancellationRequested();
            var c = (char)value;
            if (!quoted && c is '\r' or '\n')
            {
                if (!hasContent)
                {
                    length = 0;
                    foreach (var key in counts.Keys) counts[key] = 0;
                    continue;
                }
                break;
            }
            if (++length > 65536) throw new InvalidDataException("CSV header is too large.");
            hasContent |= !char.IsWhiteSpace(c);
            if (c == '"') quoted = !quoted;
            else if (!quoted && counts.ContainsKey(c)) counts[c]++;
        }
        var maximum = counts.Values.Max();
        if (maximum == 0) return ','; // A single-column CSV is valid.
        var best = counts.Where(p => p.Value == maximum).ToList();
        if (best.Count != 1) throw new InvalidDataException("CSV delimiter is ambiguous; use a consistent comma, semicolon or tab header.");
        return best[0].Key;
    }
}
