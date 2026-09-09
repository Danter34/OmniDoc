namespace OmniDoc.Domain.Exceptions;

public enum DocumentFailureCode { PasswordRequired, ConversionTimeout, ConverterUnavailable, ConversionFailed }

public class DocumentProcessingException(DocumentFailureCode code, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public DocumentFailureCode Code { get; } = code;
}

public sealed class PasswordRequiredException()
    : DocumentProcessingException(DocumentFailureCode.PasswordRequired,
        "Tệp Office được mã hóa hoặc đóng gói OLE. Vui lòng tải lên tệp DOCX, PPTX hoặc XLSX không được mã hóa.");
