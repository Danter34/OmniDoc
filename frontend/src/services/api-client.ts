import { tokenStorage } from "@/services/token-storage";

const configuredApiUrl = process.env.NEXT_PUBLIC_API_URL?.trim();
export const API_BASE_URL = configuredApiUrl?.replace(/\/$/, "") ?? "";
export const UNAUTHORIZED_EVENT = "omnidoc:unauthorized";

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly errors: string[] = [],
    public readonly errorCode?: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

function getRequestUrl(path: string) {
  if (/^https?:\/\//i.test(path)) {
    return path;
  }

  return `${API_BASE_URL}${path.startsWith("/") ? path : `/${path}`}`;
}

export function getRealtimeUrl(path: string) {
  const requestUrl = getRequestUrl(path);

  if (/^https?:\/\//i.test(requestUrl)) {
    return requestUrl;
  }

  if (typeof window === "undefined") {
    return requestUrl;
  }

  return new URL(requestUrl, window.location.origin).toString();
}

async function readResponseBody(response: Response) {
  if (response.status === 204) {
    return null;
  }

  const contentType = response.headers.get("content-type") ?? "";

  if (contentType.includes("application/json")) {
    return response.json();
  }

  const text = await response.text();
  return text || null;
}

function createApiError(response: Response, body: unknown) {
  const errors =
    body &&
    typeof body === "object" &&
    "errors" in body &&
    Array.isArray(body.errors)
      ? body.errors.filter(
          (item: unknown): item is string => typeof item === "string",
        )
      : [];
  const fallbackMessage =
    typeof body === "string" && body.trim()
      ? body
      : `Yêu cầu thất bại (${response.status}).`;
  const errorCode =
    body &&
    typeof body === "object" &&
    "errorCode" in body &&
    typeof body.errorCode === "string"
      ? body.errorCode
      : undefined;

  if (response.status === 401 && typeof window !== "undefined") {
    window.dispatchEvent(new Event(UNAUTHORIZED_EVENT));
  }

  return new ApiError(
    errors[0] ?? fallbackMessage,
    response.status,
    errors,
    errorCode,
  );
}

export async function apiFetch(
  path: string,
  init: RequestInit = {},
) {
  const headers = new Headers(init.headers);
  const token = tokenStorage.get();

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  if (init.body && !(init.body instanceof FormData) && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  return fetch(getRequestUrl(path), {
    ...init,
    headers,
  });
}

export async function apiRequest<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const response = await apiFetch(path, init);
  const body: unknown = await readResponseBody(response);

  if (!response.ok) {
    throw createApiError(response, body);
  }

  return body as T;
}

export async function apiBlobRequest(
  path: string,
  init: RequestInit = {},
): Promise<Blob> {
  const response = await apiFetch(path, init);

  if (!response.ok) {
    throw createApiError(response, await readResponseBody(response));
  }

  return response.blob();
}

export function getErrorMessage(error: unknown) {
  if (error instanceof ApiError || error instanceof Error) {
    return error.message;
  }

  return "Đã xảy ra lỗi không mong muốn. Vui lòng thử lại.";
}
