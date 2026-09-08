export type DocumentApiStatus = "Pending" | "Processing" | "Indexed" | "Failed";

export type DocumentStage =
  | "Validating"
  | "Normalizing"
  | "Pending"
  | "Processing"
  | "Extracting"
  | "Chunking"
  | "Embedding"
  | "Completed"
  | "Indexed"
  | "Failed";

export interface DocumentDto {
  id: string;
  workspaceId: string;
  title: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  status: DocumentApiStatus;
  errorMessage: string | null;
  chunkCount: number;
  createdAtUtc: string;
  detectedFormat: "Pdf" | "Txt" | "Markdown" | "Docx" | "Pptx" | "Xlsx" | "Csv";
  processingStage: DocumentStage;
  progressPercentage: number;
  failureCode: string | null;
}

export interface WorkspaceDocument extends DocumentDto {
  stage: DocumentStage;
  progress: number;
}
