export type DocumentApiStatus = "Pending" | "Processing" | "Indexed" | "Failed";

export type DocumentStage =
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
}

export interface WorkspaceDocument extends DocumentDto {
  stage: DocumentStage;
  progress: number;
}
