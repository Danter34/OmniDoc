namespace OmniDoc.Domain.Exceptions;

public enum DocumentFailureCode { PasswordRequired, ConversionTimeout, ConverterUnavailable, ConversionFailed }

public class DocumentProcessingException(DocumentFailureCode code, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public DocumentFailureCode Code { get; } = code;
}

public sealed class PasswordRequiredException()
    : DocumentProcessingException(DocumentFailureCode.PasswordRequired,
        "This Office file is encrypted or OLE-packaged. Upload an unencrypted DOCX, PPTX or XLSX file.");
