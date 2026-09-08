namespace OmniDoc.Domain.Enums;

public enum DocumentFormat { Pdf, Txt, Markdown, Docx, Pptx, Xlsx, Csv }

public enum ArtifactKind { Source, CanonicalPdf }

public enum ProcessingStage { Validating, Normalizing, Extracting, Chunking, Embedding, Completed, Failed }
